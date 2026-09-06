namespace LnShelf.Client;

/// <summary>
/// The two date forms the shelf prints, in one place.
/// </summary>
// "d MMM yyyy" was written out in five files, which is five chances for one of them to
// drift into a different month abbreviation or a slash form. The culture is invariant
// on purpose: the shelf's dates read the same wherever the browser happens to be set.
public static class DateText
{
    extension(DateOnly date)
    {
        /// <summary>"12 Sep 2026" — everywhere there is room for the whole truth.</summary>
        public string Full => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        /// <summary>"12 Sep" — the caption form, where the year would not fit.</summary>
        public string Short => date.ToString("d MMM", CultureInfo.InvariantCulture);
    }
}
