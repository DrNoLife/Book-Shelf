namespace LnShelf.Shared.Covers;

/// <summary>
/// The covers other users have shared for one volume (feature-spec §4.3).
/// </summary>
public sealed record CommunityCoversResponse
{
    public required IReadOnlyList<CommunityCoverCandidate> Candidates { get; init; }
}

/// <summary>One shared cover, as every user who shares it sees it once.</summary>
public sealed record CommunityCoverCandidate
{
    /// <summary>Lowercase hex SHA-256 of the image; also the key of its image route.</summary>
    public required string ContentHash { get; init; }

    /// <summary>The names of the users who share this exact image, earliest first.</summary>
    public required IReadOnlyList<string> SharedBy { get; init; }

    /// <summary>How many distinct users currently have this image as their cover.</summary>
    public required int UsedByCount { get; init; }

    /// <summary>Whether the signed-in user's own cover for this volume is this image.</summary>
    public required bool IsCurrent { get; init; }
}

/// <summary>Picks a shared cover as the volume's own.</summary>
public sealed record PickCommunityCoverRequest
{
    public required string ContentHash { get; init; }
}
