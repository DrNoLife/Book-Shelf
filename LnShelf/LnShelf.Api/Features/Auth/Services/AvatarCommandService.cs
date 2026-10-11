namespace LnShelf.Api.Features.Auth.Services;

/// <inheritdoc />
// The user table carries no owner filter (it is what the filters are about), so the upload and
// removal match the session's own id by hand, the way CurrentUserQueryService reads it. Every
// picture is written to the store first and its key saved second, and the key it replaces is
// deleted last: until the new key is saved, the old object is what the avatar points at.
public sealed class AvatarCommandService(
    LnShelfDbContext dbContext,
    ICurrentUser currentUser,
    ICoverStore coverStore,
    CoverNormalizer normalizer,
    IProviderAvatarFetcher providerAvatarFetcher,
    IOptions<AvatarOptions> options,
    ILogger<AvatarCommandService> logger) : IAvatarCommandService
{
    public async Task RefreshProviderCopyAsync(User user, string? pictureUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // No claim: the provider sent no picture this time, which says nothing about the one
        // already copied, so that copy stays. A URL too long for its column is treated alike.
        if (pictureUrl is not { Length: > 0 and <= User.MaximumPictureUrlLength }
            || !Uri.TryCreate(pictureUrl, UriKind.Absolute, out var pictureUri))
        {
            return;
        }

        try
        {
            if (await HasCurrentCopyAsync(user, pictureUrl, cancellationToken))
            {
                return;
            }

            var picture = await providerAvatarFetcher.FetchAsync(pictureUri, cancellationToken);
            if (picture is null)
            {
                return;
            }

            var objectKey = CoverObjectKey.ForAvatar(user.Id, Guid.CreateVersion7());
            await coverStore.SaveAsync(objectKey, picture, CoverNormalizer.ContentType, cancellationToken);

            var replacedObjectKey = user.ProviderAvatarObjectKey;
            user.ProviderPictureUrl = pictureUrl;
            user.ProviderAvatarObjectKey = objectKey;
            await dbContext.SaveChangesAsync(cancellationToken);

            await coverStore.DeleteUnreferencedAsync(replacedObjectKey, logger, cancellationToken);

            logger.LogInformation("Copied the provider picture of {UserId} to {ObjectKey}", user.Id, objectKey);
        }
        // The caller's cancellation only: HttpClient's timeout also surfaces as a cancellation,
        // and a slow picture is exactly the hiccup that must not end a sign-in.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Deliberately broad, like CoverFetcher's: an avatar is decoration, the sign-in is
            // not, and the next sign-in tries again.
            logger.LogWarning(exception, "Could not copy the provider picture of {UserId}", user.Id);
        }
    }

    public async Task<bool> SetOverrideAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        if (content.Length == 0)
        {
            throw new InvalidInputException("That file is empty.");
        }

        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        // Normalised before anything is stored, so an upload that is not a decodable image is
        // refused rather than served back as one later.
        var normalized = await normalizer.NormalizeAsync(content, options.Value.MaximumEdge, cancellationToken)
            ?? throw new InvalidInputException("That file is not an image the shelf can read.");

        var objectKey = CoverObjectKey.ForAvatar(user.Id, Guid.CreateVersion7());
        await coverStore.SaveAsync(objectKey, normalized, CoverNormalizer.ContentType, cancellationToken);

        var replacedObjectKey = user.AvatarOverrideObjectKey;
        user.AvatarOverrideObjectKey = objectKey;
        await dbContext.SaveChangesAsync(cancellationToken);

        await coverStore.DeleteUnreferencedAsync(replacedObjectKey, logger, cancellationToken);

        logger.LogInformation("{UserId} uploaded an avatar at {ObjectKey}", user.Id, objectKey);
        return true;
    }

    public async Task<bool> RemoveOverrideAsync(CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (user.AvatarOverrideObjectKey is { } removedObjectKey)
        {
            user.AvatarOverrideObjectKey = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            await coverStore.DeleteUnreferencedAsync(removedObjectKey, logger, cancellationToken);
        }

        logger.LogInformation("{UserId} is back to the provider picture", user.Id);
        return true;
    }

    // Fetched once per picture, not once per sign-in: the same URL with its copy still in the
    // store is the common case, and costs one file check. A copy that has gone missing (a
    // restore without the covers, a sweep racing a sign-in) is fetched again.
    private async Task<bool> HasCurrentCopyAsync(User user, string pictureUrl, CancellationToken cancellationToken) =>
        string.Equals(user.ProviderPictureUrl, pictureUrl, StringComparison.Ordinal)
        && user.ProviderAvatarObjectKey is { } objectKey
        && await coverStore.ExistsAsync(objectKey, cancellationToken);

    private Task<User?> FindCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        return dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
