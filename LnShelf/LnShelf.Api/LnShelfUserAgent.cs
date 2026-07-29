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
}
