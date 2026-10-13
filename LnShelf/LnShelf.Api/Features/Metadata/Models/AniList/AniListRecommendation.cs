namespace LnShelf.Api.Features.Metadata.Models.AniList;

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListRecommendationConnection
{
    public List<AniListRecommendation>? Nodes { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListRecommendation
{
    /// <summary>The net of readers' up- and down-votes for the pairing.</summary>
    public int? Rating { get; set; }

    /// <summary>Null for a deleted entry.</summary>
    public AniListMedia? MediaRecommendation { get; set; }
}
