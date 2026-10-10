namespace LnShelf.Api.Features.Sharing.Abstractions;

/// <summary>
/// The signed-in user's own sharing setting.
/// </summary>
public interface IShelfShareQueryService
{
    Task<ShelfSharingResponse> GetAsync(CancellationToken cancellationToken);
}
