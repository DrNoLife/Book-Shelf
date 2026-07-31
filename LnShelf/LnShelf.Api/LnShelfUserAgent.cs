namespace LnShelf.Api;

/// <summary>
/// The User-Agent every outbound request carries.
/// </summary>
// Being identifiable is part of being a polite API citizen (CLAUDE.md), and it is
// not optional in practice: AniList answers 403 to some default client
// user-agents, so an anonymous request is a request that may simply not work.
public static class LnShelfUserAgent
{
    public const string Value = "LnShelf/1.0 (self-hosted shelf tracker)";

    /// <summary>
    /// The contactable form, for sources whose docs ask for one.
    /// </summary>
    // Open Library asks callers to identify themselves with a way to reach them, and a
    // donated catalogue is entitled to know who is reading it. The address comes from
    // configuration rather than from here, so it is changeable without a rebuild.
    public static string WithContact(string? contactEmail) =>
        string.IsNullOrWhiteSpace(contactEmail)
            ? Value
            : $"LnShelf/1.0 (contact: {contactEmail.Trim()})";
}
