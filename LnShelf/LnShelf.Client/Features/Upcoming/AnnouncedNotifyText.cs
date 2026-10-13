namespace LnShelf.Client.Features.Upcoming;

/// <summary>What the Announced line's "notify me when dated" toggle and its quiet note say.</summary>
public static class AnnouncedNotifyText
{
    /// <summary>The toggle's label: the action while off, the promise while on.</summary>
    public static string Label(bool on) => on ? "We'll tell you when dated" : "Notify me when dated";

    /// <summary>
    /// Shown instead of a toggle on an unmuted series, where the digest already carries the
    /// date when it arrives — a button there would change nothing.
    /// </summary>
    public const string AlreadyToldNote = "You'll be told when it's dated";
}
