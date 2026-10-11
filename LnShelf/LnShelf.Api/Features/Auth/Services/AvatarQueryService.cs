namespace LnShelf.Api.Features.Auth.Services;

/// <inheritdoc />
// Matched to the session's own id by hand, like CurrentUserQueryService: the user table has no
// owner filter, and this is the only way an avatar leaves the store.
public sealed class AvatarQueryService(LnShelfDbContext dbContext, ICurrentUser currentUser, ICoverStore coverStore)
    : IAvatarQueryService
{
    public async Task<AvatarImage?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var objectKey = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.AvatarOverrideObjectKey ?? user.ProviderAvatarObjectKey)
            .SingleOrDefaultAsync(cancellationToken);

        if (objectKey is null || await coverStore.ReadAsync(objectKey, cancellationToken) is not { } picture)
        {
            return null;
        }

        return new AvatarImage(picture.Content, picture.ContentType, CoverObjectKey.AvatarVersionOf(objectKey)!);
    }
}
