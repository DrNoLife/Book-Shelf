using LnShelf.Api.Features.Sharing.Abstractions;

namespace LnShelf.Api.Features.Sharing.Services;

/// <inheritdoc />
// The row is the setting: present is public, absent is private (feature-spec §4.8). Private
// deletes it rather than flagging it, so turning sharing off kills every copy of the link for
// good instead of keeping a token that a later "Public" would bring back to life.
//
// The token is never logged: it is the whole access control of the shared page.
public sealed class ShelfShareCommandService(
    LnShelfDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ShelfShareCommandService> logger) : IShelfShareCommandService
{
    public async Task<ShelfSharingResponse> SetVisibilityAsync(bool isPublic, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var share = await dbContext.ShelfShares.FindAsync([userId], cancellationToken);

        if (isPublic && share is null)
        {
            share = new ShelfShare { UserId = userId, Token = ShareToken.Create(), CreatedAt = timeProvider.GetUtcNow() };
            dbContext.ShelfShares.Add(share);
        }
        else if (!isPublic && share is not null)
        {
            dbContext.ShelfShares.Remove(share);
            share = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Shelf sharing for {UserId} set to {Visibility}", userId, isPublic ? "Public" : "Private");

        return ToResponse(share);
    }

    public async Task<ShelfSharingResponse> RegenerateAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var share = await dbContext.ShelfShares.FindAsync([userId], cancellationToken)
                    ?? throw new ConflictException("Make the shelf public first.");

        share.Token = ShareToken.Create();
        share.CreatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Shelf link for {UserId} regenerated", userId);

        return ToResponse(share);
    }

    private static ShelfSharingResponse ToResponse(ShelfShare? share) =>
        new() { IsPublic = share is not null, Token = share?.Token };
}
