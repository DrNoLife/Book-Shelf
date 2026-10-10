namespace LnShelf.Api.Features.Sharing.Abstractions;

/// <summary>
/// Turns a shared shelf's token into the user who shared it (feature-spec §4.8).
/// </summary>
public interface IShelfShareResolver
{
    /// <returns>
    /// The owner, or null for a malformed, unknown or revoked token. The three are
    /// indistinguishable on purpose.
    /// </returns>
    Task<Guid?> FindOwnerAsync(string token, CancellationToken cancellationToken);
}
