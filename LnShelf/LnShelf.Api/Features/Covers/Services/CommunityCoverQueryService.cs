namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
// community_cover is not owner-filtered, so what keeps one user's reads to what is meant for
// them is the match key: a volume's own series ids, format and number, never a free query.
// The users table is unfiltered too, which is how a sharer's name is found.
public sealed class CommunityCoverQueryService(
    LnShelfDbContext dbContext,
    ICoverStore coverStore,
    ICurrentUser currentUser) : ICommunityCoverQueryService
{
    public async Task<CommunityCoversResponse?> ListForVolumeAsync(Guid volumeId, CancellationToken cancellationToken)
    {
        // Under the owner filter another user's volume does not exist, so it is a null here.
        var volume = await dbContext.Volumes
            .AsNoTracking()
            .Include(candidate => candidate.Series)
            .Include(candidate => candidate.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == volumeId, cancellationToken);

        if (volume?.Series is not { } series)
        {
            return null;
        }

        // Under the owner filter: these are the hashes this user hid.
        var hidden = await dbContext.CommunityCoverHides
            .AsNoTracking()
            .Select(hide => hide.ContentHash)
            .ToListAsync(cancellationToken);

        var rows = await dbContext.CommunityCovers
            .AsNoTracking()
            .MatchingOthers(series, volume, currentUser.Id)
            .Where(cover => !hidden.Contains(cover.ContentHash))
            .ToListAsync(cancellationToken);

        var currentHash = await HashOfOwnOverrideAsync(volume, cancellationToken);
        var names = await NamesOfAsync(rows, cancellationToken);

        // Most shared first, then whoever had it earliest.
        var groups = rows
            .GroupBy(row => row.ContentHash)
            .Select(group => group.OrderBy(row => row.PublishedAt).ToList())
            .OrderByDescending(group => group.Select(row => row.UploaderUserId).Distinct().Count())
            .ThenBy(group => group[0].PublishedAt)
            .ToList();

        var candidates = new List<CommunityCoverCandidate>(groups.Count);

        foreach (var group in groups)
        {
            // A group whose every object is gone cannot be shown or picked: dropped, not an error.
            if (!await AnyObjectExistsAsync(group, cancellationToken))
            {
                continue;
            }

            var uploaders = group.Select(row => row.UploaderUserId).Distinct().ToList();

            candidates.Add(new CommunityCoverCandidate
            {
                ContentHash = group[0].ContentHash,
                SharedBy = uploaders.Select(uploaderId => names.GetValueOrDefault(uploaderId, UnknownSharerName)).ToList(),
                UsedByCount = uploaders.Count,
                IsCurrent = currentHash == group[0].ContentHash,
            });
        }

        return new CommunityCoversResponse { Candidates = candidates };
    }

    public async Task<CoverSharingResponse> GetSharingAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var since = await dbContext.UserPreferences
            .AsNoTracking()
            .Select(preferences => preferences.CoverSharingSince)
            .FirstOrDefaultAsync(cancellationToken);

        var sharedCount = await dbContext.CommunityCovers
            .CountAsync(cover => cover.UploaderUserId == userId, cancellationToken);

        return new CoverSharingResponse { Enabled = since is not null, Since = since, SharedCount = sharedCount };
    }

    public async Task<CoverContent?> ReadImageAsync(string contentHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var rows = await dbContext.CommunityCovers
            .AsNoTracking()
            .Where(cover => cover.ContentHash == contentHash)
            .OrderBy(cover => cover.PublishedAt)
            .ToListAsync(cancellationToken);

        return await ReadFirstAsync(rows, cancellationToken);
    }

    public async Task<CoverContent?> FindMatchingAsync(
        Series series,
        Volume volume,
        string contentHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var rows = await dbContext.CommunityCovers
            .AsNoTracking()
            .MatchingOthers(series, volume, currentUser.Id)
            .Where(cover => cover.ContentHash == contentHash)
            .OrderBy(cover => cover.PublishedAt)
            .ToListAsync(cancellationToken);

        return await ReadFirstAsync(rows, cancellationToken);
    }

    private async Task<CoverContent?> ReadFirstAsync(
        IReadOnlyList<CommunityCover> rows,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            if (await coverStore.ReadAsync(row.ObjectKey, cancellationToken) is { } content)
            {
                return content;
            }
        }

        return null;
    }

    private async Task<bool> AnyObjectExistsAsync(
        IReadOnlyList<CommunityCover> group,
        CancellationToken cancellationToken)
    {
        foreach (var row in group)
        {
            if (await coverStore.ExistsAsync(row.ObjectKey, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The hash of the user's own cover for the volume, read once; null without one.</summary>
    private async Task<string?> HashOfOwnOverrideAsync(Volume volume, CancellationToken cancellationToken)
    {
        if (volume.UserState?.CoverOverrideObjectKey is not { } objectKey)
        {
            return null;
        }

        var content = await coverStore.ReadAsync(objectKey, cancellationToken);
        return content is null ? null : CoverContentHash.Compute(content.Content);
    }

    private async Task<Dictionary<Guid, string>> NamesOfAsync(
        IReadOnlyList<CommunityCover> rows,
        CancellationToken cancellationToken)
    {
        var uploaderIds = rows.Select(row => row.UploaderUserId).Distinct().ToList();

        var emails = await dbContext.Users
            .AsNoTracking()
            .Where(user => uploaderIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(cancellationToken);

        return emails.ToDictionary(user => user.Id, user => SharedNameOf(user.Email));
    }

    // FromEmail falls back to the whole address when it finds no letters, which is fine on the
    // user's own profile but would hand another user's email to everyone sharing a volume.
    private static string SharedNameOf(string email)
    {
        var name = DisplayNameMapper.FromEmail(email);
        return name.Contains('@', StringComparison.Ordinal) ? UnknownSharerName : name;
    }

    private const string UnknownSharerName = "Someone";
}
