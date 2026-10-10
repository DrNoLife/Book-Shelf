namespace LnShelf.Api.Features.Sharing.Abstractions;

/// <summary>
/// The read-only shelf a visitor with the link sees (feature-spec §4.8).
/// </summary>
public interface ISharedShelfQueryService
{
    /// <returns>The shelf, or null when the token opens nothing.</returns>
    Task<SharedShelfResponse?> GetAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="objectKey"/> is the series art of a series visible on the shelf
    /// the token opens. The only covers the shared page may serve.
    /// </summary>
    Task<bool> IsCoverOnShelfAsync(string token, string objectKey, CancellationToken cancellationToken);
}
