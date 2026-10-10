using LnShelf.Client.Features.Search;

namespace LnShelf.Client.Features.Search.Components;

public partial class SearchEntryRow
{
    [Parameter]
    public required SearchWorkEntry Entry { get; set; }

    /// <summary>
    /// Appends " · LN" or " · Manga" to the name: under All, "Part 1" alone does not say
    /// which of the two series it is.
    /// </summary>
    [Parameter]
    public bool ShowFormat { get; set; }

    /// <summary>The row the preview is showing.</summary>
    [Parameter]
    public bool IsSelected { get; set; }

    /// <summary>Already tracked: the row says so instead of offering Add again.</summary>
    [Parameter]
    public bool IsOnShelf { get; set; }

    /// <summary>A search or an add is in flight; Add waits for it.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback<SearchWorkEntry> Selected { get; set; }

    [Parameter]
    public EventCallback<SearchWorkEntry> Added { get; set; }

    private string Label => ShowFormat && !Entry.IsNamedByFormat
        ? $"{Entry.Name} · {SearchWorkText.ShortName(Entry.Result.Format)}"
        : Entry.Name;

    /// <summary>
    /// What the two buttons are called to a screen reader: the full title and the format,
    /// because "Part 1" or "Manga" alone does not say which series it is once read out of
    /// its card.
    /// </summary>
    private string AccessibleName =>
        $"{Entry.Result.TitleEnglish}, {SearchWorkText.LongName(Entry.Result.Format).ToLowerInvariant()}";

    /// <summary>AniList publishes no total for most ongoing series; a dash says so quietly.</summary>
    private string VolumeText => Entry.Result.VolumeCount switch
    {
        null => "—",
        1 => "1 vol",
        var count => $"{count} vols",
    };

    /// <summary>The phone's one line of facts: "3 vols · 2015 · Completed".</summary>
    private string FactsLine
    {
        get
        {
            var volumes = Entry.Result.VolumeCount is null ? "Vols unknown" : VolumeText;
            List<string> parts = [volumes];

            if (Entry.Result.StartYear is { } startYear)
            {
                parts.Add(startYear.ToString(CultureInfo.InvariantCulture));
            }

            parts.Add(Entry.Result.PublicationStatus.Words);

            return string.Join(" · ", parts);
        }
    }

    private Task OnSelectedAsync() => Selected.InvokeAsync(Entry);

    private Task OnAddedAsync() => Added.InvokeAsync(Entry);
}
