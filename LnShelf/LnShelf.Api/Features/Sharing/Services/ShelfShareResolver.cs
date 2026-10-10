using LnShelf.Api.Features.Sharing.Abstractions;

namespace LnShelf.Api.Features.Sharing.Services;

/// <inheritdoc />
// The only read of shelf_share by token. The table is not owner-filtered (ShelfShare says
// why), so this needs no IgnoreQueryFilters: it works in an anonymous scope by design.
// Not cached, so a revoked or replaced link is dead from the very next request.
public sealed class ShelfShareResolver(LnShelfDbContext dbContext) : IShelfShareResolver
{
    public async Task<Guid?> FindOwnerAsync(string token, CancellationToken cancellationToken)
    {
        if (!ShareToken.IsWellFormed(token))
        {
            return null;
        }

        return await dbContext.ShelfShares
            .AsNoTracking()
            .Where(share => share.Token == token)
            .Select(share => (Guid?)share.UserId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
