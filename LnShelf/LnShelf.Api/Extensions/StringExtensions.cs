namespace LnShelf.Api.Extensions;

/// <summary>
/// The one place a blank string becomes null.
/// </summary>
// Cross-slice on purpose. Every dirty→clean mapper needs this rule, because every source
// sends null, "" and " " interchangeably for "no value", and a shape that stores " " reads
// as present everywhere downstream. It was copied privately into six classes before this
// existed, and the copies had already drifted — five trimmed, one did not — which is one
// judgement in six places waiting to disagree again.
public static class StringExtensions
{
    extension(string? source)
    {
        /// <returns>
        /// Null for null, empty or all-whitespace; otherwise the value with its
        /// surrounding whitespace removed.
        /// </returns>
        // Trimmed rather than returned as-is: a title that arrives as "Overlord " is the
        // same title, and leaving the space in makes it a different one to every
        // comparison, cache key and normaliser it later meets.
        public string? NullIfBlank() => string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    }
}
