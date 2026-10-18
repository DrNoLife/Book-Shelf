namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// One shared cover the user chose never to be offered, by its content hash.
/// </summary>
// By hash rather than by row: the same image shared by several users, or published again
// later, stays hidden. A root of its own (IOwned) because it belongs to the hider, not to
// any shelf item.
public sealed class CommunityCoverHide : IOwned
{
    public Guid Id { get; set; }

    /// <summary>The user this belongs to (IOwned). Stamped on save.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the hidden image.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTimeOffset HiddenAt { get; set; }
}
