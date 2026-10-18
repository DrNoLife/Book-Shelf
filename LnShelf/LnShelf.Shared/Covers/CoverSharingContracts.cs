namespace LnShelf.Shared.Covers;

/// <summary>Turns sharing of the signed-in user's uploaded covers on or off.</summary>
public sealed record SetCoverSharingRequest
{
    public required bool Enabled { get; init; }
}

/// <summary>The signed-in user's cover-sharing setting.</summary>
public sealed record CoverSharingResponse
{
    public required bool Enabled { get; init; }

    /// <summary>When consent was given; null while sharing is off.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>How many of the user's covers are shared right now.</summary>
    public required int SharedCount { get; init; }
}
