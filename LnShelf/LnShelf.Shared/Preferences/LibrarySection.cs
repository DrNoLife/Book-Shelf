namespace LnShelf.Shared.Preferences;

/// <summary>
/// The three sections of the library page under its header, which the reader can put in
/// any order (<see cref="LibrarySections"/>).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LibrarySection>))]
public enum LibrarySection
{
    /// <summary>"Continue reading" on a desktop, the "Up next" carousel on a phone.</summary>
    ContinueReading,

    /// <summary>"Out now, not on your shelf".</summary>
    OutNow,

    /// <summary>The shelf itself: the toolbar and the grid.</summary>
    Shelf,
}
