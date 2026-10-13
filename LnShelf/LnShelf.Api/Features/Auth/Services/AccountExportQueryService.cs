using LnShelf.Api.Features.Preferences.Mappers;

namespace LnShelf.Api.Features.Auth.Services;

/// <inheritdoc />
// The shelf and its activity log, imports, sync runs, push devices, dismissed suggestions and
// preferences are read
// under the owner filters, with no
// filtering by hand: what the filters leave is this user's data, which is what makes the
// export safe by construction. Only the three rows keyed by the user themselves (the account,
// its public link and its calendar link) are matched by id, the way CurrentUserQueryService
// and ShelfShareQueryService do it.
public sealed class AccountExportQueryService(
    LnShelfDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAccountExportQueryService
{
    public async Task<AccountExportResponse?> ExportAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var share = await dbContext.ShelfShares
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        var calendar = await dbContext.CalendarFeeds
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        var devices = await dbContext.PushSubscriptions
            .AsNoTracking()
            .OrderBy(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        var preferences = await dbContext.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var series = await dbContext.Series
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .OrderBy(candidate => candidate.TitleEnglish)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);

        var events = (await dbContext.ShelfEvents.AsNoTracking().ToListAsync(cancellationToken))
            .ToLookup(shelfEvent => shelfEvent.SeriesId);

        var batches = await dbContext.ImportBatches
            .AsNoTracking()
            .AsSplitQuery()
            .Include(batch => batch.Groups).ThenInclude(group => group.Rows)
            .OrderBy(batch => batch.UploadedAt)
            .ToListAsync(cancellationToken);

        var runs = await dbContext.SyncRuns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(run => run.Discrepancies).ThenInclude(discrepancy => discrepancy.Values)
            .OrderBy(run => run.StartedAt)
            .ToListAsync(cancellationToken);

        var dismissals = await dbContext.RecommendationDismissals
            .AsNoTracking()
            .OrderBy(dismissal => dismissal.DismissedAt)
            .ThenBy(dismissal => dismissal.AniListId)
            .ToListAsync(cancellationToken);

        return new AccountExportResponse
        {
            ExportedAt = timeProvider.GetUtcNow(),
            Account = AccountExportMapper.ToAccount(user),
            Sharing = AccountExportMapper.ToSharing(share),
            Calendar = AccountExportMapper.ToCalendar(calendar),
            NotificationDevices = devices.Select(AccountExportMapper.ToPushDevice).ToList(),

            // Never saved reads as the defaults, the same answer GET /api/preferences gives.
            Preferences = PreferencesMapper.ToResponse(preferences ?? new UserPreferences()),
            ActivitySeenAt = preferences?.ActivitySeenAt,
            Series = series.Select(candidate => AccountExportMapper.ToSeries(candidate, events[candidate.Id])).ToList(),
            ImportBatches = batches.Select(AccountExportMapper.ToImportBatch).ToList(),
            SyncRuns = runs.Select(AccountExportMapper.ToSyncRun).ToList(),
            RecommendationDismissals = dismissals.Select(AccountExportMapper.ToRecommendationDismissal).ToList(),
        };
    }
}
