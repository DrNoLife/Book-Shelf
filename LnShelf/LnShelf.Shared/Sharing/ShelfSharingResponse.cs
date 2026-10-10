namespace LnShelf.Shared.Sharing;

/// <summary>
/// The signed-in user's own sharing setting (feature-spec §4.8).
/// </summary>
// The token, not a URL: the client builds the link from its own base address, so the API
// never has to know the hostname it is published under.
public sealed record ShelfSharingResponse
{
    public required bool IsPublic { get; init; }

    /// <summary>The link's token while the shelf is public; null while it is private.</summary>
    public string? Token { get; init; }
}

/// <summary>Private or public. Public issues a link; private deletes it.</summary>
public sealed record UpdateShelfSharingRequest
{
    public required bool IsPublic { get; init; }
}
