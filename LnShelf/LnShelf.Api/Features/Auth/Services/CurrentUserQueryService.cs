namespace LnShelf.Api.Features.Auth.Services;

/// <inheritdoc />
// Users carry no owner filter (they are the owners), so the id is matched by hand here, to
// the session's own user and nobody else's.
public sealed class CurrentUserQueryService(LnShelfDbContext dbContext, ICurrentUser currentUser)
    : ICurrentUserQueryService
{
    public async Task<CurrentUserResponse?> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Email,
                candidate.CreatedAt,
                candidate.AvatarOverrideObjectKey,
                candidate.ProviderAvatarObjectKey,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null
            ? null
            : new CurrentUserResponse
            {
                DisplayName = DisplayNameMapper.FromEmail(user.Email),
                Email = user.Email,
                CreatedAt = user.CreatedAt,

                // The upload wins over the provider's copy, as GET api/auth/me/avatar serves it.
                AvatarVersion = CoverObjectKey.AvatarVersionOf(user.AvatarOverrideObjectKey ?? user.ProviderAvatarObjectKey),
                HasUploadedAvatar = user.AvatarOverrideObjectKey is not null,
            };
    }
}
