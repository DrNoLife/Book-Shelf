using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Features.Sharing.Mappers;

namespace LnShelf.Api.Features.Sharing.Services;

/// <inheritdoc />
// The one place a request reads another user's shelf (feature-spec §4.8). It never lifts the
// owner filters: it finds the owner from the token, then reads in a scope of its own that acts
// as that owner, so the filters do the isolating exactly as they do for the nightly sync.
//
// A new scope rather than ActAs on the request's own ICurrentUser: CurrentUser is scoped and
// mutable, so acting as the owner there would make everything after it in this request run as
// the owner. The child scope also brings its own DbContext, so nothing the token lookup
// tracked is carried over.
public sealed class SharedShelfQueryService(
    IShelfShareResolver resolver,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ISharedShelfQueryService
{
    public async Task<SharedShelfResponse?> GetAsync(string token, CancellationToken cancellationToken)
    {
        if (await resolver.FindOwnerAsync(token, cancellationToken) is not { } ownerId)
        {
            return null;
        }

        var today = timeProvider.Today();

        var cards = await ReadAsOwnerAsync(
            ownerId,
            ownerContext => ownerContext.Series
                .AsNoTracking()
                .Where(SharedSeriesCardProjection.IsVisible)
                .Select(SharedSeriesCardProjection.ToCard(today))
                .ToListAsync(cancellationToken));

        return SharedShelfMapper.ToResponse(cards);
    }

    public async Task<bool> IsCoverOnShelfAsync(string token, string objectKey, CancellationToken cancellationToken)
    {
        if (await resolver.FindOwnerAsync(token, cancellationToken) is not { } ownerId)
        {
            return false;
        }

        return await ReadAsOwnerAsync(
            ownerId,
            ownerContext => ownerContext.Series
                .Where(SharedSeriesCardProjection.IsVisible)
                .AnyAsync(series => series.CoverObjectKey == objectKey, cancellationToken));
    }

    // The ActAs call of the request path, in one place.
    private async Task<T> ReadAsOwnerAsync<T>(Guid ownerId, Func<LnShelfDbContext, Task<T>> read)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(ownerId);

        return await read(scope.ServiceProvider.GetRequiredService<LnShelfDbContext>());
    }
}
