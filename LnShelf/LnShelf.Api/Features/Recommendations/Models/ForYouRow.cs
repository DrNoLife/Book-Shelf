namespace LnShelf.Api.Features.Recommendations.Models;

/// <summary>One suggestion in a row, with the score that ranked it.</summary>
/// <param name="OtherSeedCount">How many other positive seeds also led to this work.</param>
public sealed record ForYouPick(RecommendedWork Work, double Score, int OtherSeedCount);

/// <summary>One "Because you read X" row.</summary>
public sealed record ForYouRow(RecommendationSeed Seed, IReadOnlyList<ForYouPick> Picks);
