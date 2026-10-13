namespace LnShelf.Client.Features.Recommendations;

/// <summary>The words of the recommendation strip and For you, kept pure so they can be tested.</summary>
public static class RecommendationText
{
    public const string NothingToGoOn =
        "Nothing to go on yet. Finish a volume or mark a series Completed, and suggestions show up here.";

    public const string NothingNew = "Readers of your series haven't voted for anything you don't already have.";

    public const string LoadFailed = "Couldn't get suggestions right now. Try again in a minute.";

    public static string AlsoLikedHeading(string title) => $"Readers of {title} also liked";

    public static string BecauseOfHeading(string title) => $"Because you read {title}";

    public static string Votes(int count) => count == 1 ? "1 vote" : $"{count} votes";

    public static string? OtherSeeds(int count) => count <= 0 ? null : $"+ {count} more of yours";

    public static string SeedLine(int count) =>
        count == 1 ? "From 1 series you've read" : $"From {count} series you've read";

    public static string Hidden(string title) => $"{title} won't be suggested again.";

    public static string DismissedSummary(int count) => $"Not interested ({count})";

    public static string NotInterestedLabel(string title) => $"Not interested in {title}";

    public static string ShowAgainLabel(string title) => $"Show {title} again";
}
