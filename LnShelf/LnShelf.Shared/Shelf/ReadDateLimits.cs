namespace LnShelf.Shared.Shelf;

/// <summary>
/// The range a read date may fall in, shared so the date input's <c>min</c> and the API's
/// refusal cannot drift apart. The upper bound is "today", which each side computes for itself.
/// </summary>
public static class ReadDateLimits
{
    /// <summary>Anything earlier is a typo, not a reading.</summary>
    public static readonly DateOnly Earliest = new(1900, 1, 1);
}
