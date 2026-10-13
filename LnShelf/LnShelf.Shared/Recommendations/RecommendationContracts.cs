using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Recommendations;

/// <summary>One suggested work, as "Readers also liked" and For you show it.</summary>
public sealed record RecommendationResponse
{
    /// <summary>The catalogue's id for the work; what Add and Not interested are keyed on.</summary>
    public required int AniListId { get; init; }

    /// <summary>The title to show. Never blank.</summary>
    public required string TitleEnglish { get; init; }

    public required Format Format { get; init; }

    /// <summary>The cover's object key once it is cached locally; null until then.</summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>The readers' net votes on the series this suggestion came from.</summary>
    public required int Votes { get; init; }

    /// <summary>For you only: how many other shelf series also led here. 0 on the strip.</summary>
    public int OtherSeedCount { get; init; }
}

/// <summary>The "Readers also liked" strip of one series.</summary>
public sealed record SeriesRecommendationsResponse
{
    public IReadOnlyList<RecommendationResponse> Works { get; init; } = [];
}

/// <summary>The For you page.</summary>
public sealed record ForYouResponse
{
    /// <summary>How many shelf series the suggestions were drawn from.</summary>
    public required int SeedCount { get; init; }

    public IReadOnlyList<ForYouRowResponse> Rows { get; init; } = [];
}

/// <summary>One "Because you read X" row of For you.</summary>
public sealed record ForYouRowResponse
{
    public required Guid BecauseOfSeriesId { get; init; }

    public required string BecauseOfTitle { get; init; }

    public required Format Format { get; init; }

    public required IReadOnlyList<RecommendationResponse> Works { get; init; }
}

/// <summary>Hides a work from For you. The title travels so the hidden list can name it.</summary>
public sealed record DismissRecommendationRequest
{
    public required string Title { get; init; }

    public required Format Format { get; init; }
}

/// <summary>One work hidden from For you with Not interested.</summary>
public sealed record RecommendationDismissalResponse
{
    public required int AniListId { get; init; }

    public required string Title { get; init; }

    public required Format Format { get; init; }

    public required DateTimeOffset DismissedAt { get; init; }
}
