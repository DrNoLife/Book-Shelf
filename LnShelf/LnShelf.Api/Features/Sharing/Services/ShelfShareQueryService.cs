using LnShelf.Api.Features.Sharing.Abstractions;

namespace LnShelf.Api.Features.Sharing.Services;

/// <inheritdoc />
// A primary-key lookup of the current user's own credential row, the way
// AccountDeletionService finds its user: ShelfShare is keyed by the user and has nothing else
// to look it up by. This is not hand-written owner filtering of shelf data.
public sealed class ShelfShareQueryService(LnShelfDbContext dbContext, ICurrentUser currentUser)
    : IShelfShareQueryService
{
    public async Task<ShelfSharingResponse> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var token = await dbContext.ShelfShares
            .AsNoTracking()
            .Where(share => share.UserId == userId)
            .Select(share => share.Token)
            .SingleOrDefaultAsync(cancellationToken);

        return new ShelfSharingResponse { IsPublic = token is not null, Token = token };
    }
}
