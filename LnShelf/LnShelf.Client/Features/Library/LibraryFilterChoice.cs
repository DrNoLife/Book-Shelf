namespace LnShelf.Client.Features.Library;

/// <summary>
/// What the shelf is narrowed to: the status filters (normalised, see
/// <see cref="LibraryFilterSet"/>) and the format, null for every format.
/// </summary>
public sealed record LibraryFilterChoice(IReadOnlyList<LibraryFilter> Statuses, Format? Format)
{
    /// <summary>
    /// Which filters the shelf opens on. The filters this browser remembers win; without them,
    /// the reader's default from their preferences, across every format.
    /// </summary>
    // The default arrives already holding Behind when the preferences could not be loaded
    // (PreferencesState starts there), which is the third and last step of the precedence.
    public static LibraryFilterChoice Resolve(LibraryFilterChoice? remembered, IReadOnlyList<LibraryFilter> defaultStatuses) =>
        remembered ?? new LibraryFilterChoice(LibraryFilterSet.Normalize(defaultStatuses), Format: null);
}
