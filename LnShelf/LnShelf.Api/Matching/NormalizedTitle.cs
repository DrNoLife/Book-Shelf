namespace LnShelf.Api.Matching;

/// <summary>
/// A title already folded by <see cref="TitleNormalizer"/>.
/// </summary>
// A type rather than a plain string, so a caller that has done the folding once can pass
// the result around without any chance of a raw title arriving where a folded one is meant.
// The reason it exists at all is the catalogue loop: a store answers with up to 200 records
// per title tried, and up to ten titles are tried per series, so folding the series title
// per record was thousands of FormKD decompositions a night for one answer that never
// changes.
public readonly record struct NormalizedTitle(string Value)
{
    public static NormalizedTitle From(string? title) => new(TitleNormalizer.Normalize(title));

    /// <summary>Empty for a title that folded away to nothing, which matches nothing.</summary>
    public bool IsEmpty => Value.Length is 0;

    public override string ToString() => Value;
}
