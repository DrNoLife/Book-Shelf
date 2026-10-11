namespace LnShelf.Client;

/// <summary>
/// The "Vol 12 on Saxo" link, built client-side from title plus volume number.
/// </summary>
// One home for it because two surfaces now print it — the series hero's call to
// action and the library rows view's next-volume column — and they must not drift
// into two different queries. No affiliate plumbing behind it; it is a search URL.
public static class SaxoSearch
{
    public static string UrlFor(string title, decimal volumeNumber) =>
        "https://www.saxo.com/dk/products/search?query="
        + Uri.EscapeDataString($"{title} {volumeNumber.ToVolumeText()}");
}
