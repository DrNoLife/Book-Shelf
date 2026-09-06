namespace LnShelf.Client.Features.Import;

/// <summary>One decision about one group, as a review card reports it upward.</summary>
public sealed record ImportDecision(Guid GroupId, int? AniListId, bool Skip, bool TreatReadAsOwned);
