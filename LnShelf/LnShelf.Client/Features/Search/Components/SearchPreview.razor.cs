namespace LnShelf.Client.Features.Search.Components;

public partial class SearchPreview
{
    [Parameter]
    public required SeriesSearchResultResponse Result { get; set; }

    /// <summary>The shelf's series when the result is already on it; null offers the add instead.</summary>
    [Parameter]
    public Guid? SeriesId { get; set; }

    /// <summary>A search or an add is in flight; Add waits for it.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>This series is the one being added, from here or from its row.</summary>
    [Parameter]
    public bool IsAdding { get; set; }

    /// <summary>The add has run long enough to say why; only read while <see cref="IsAdding"/>.</summary>
    [Parameter]
    public bool IsTakingLong { get; set; }

    /// <summary>"Add to shelf", with the start the reader set.</summary>
    [Parameter]
    public EventCallback<StartingVolumes> AddRequested { get; set; }

    private SeriesSearchResultResponse? _shownResult;
    private StartingVolumes _start = null!;

    /// <summary>"3 vols · 2015 · Completed".</summary>
    private string Facts
    {
        get
        {
            List<string> parts =
            [
                Result.VolumeCount switch
                {
                    null => "Vols unknown",
                    1 => "1 vol",
                    var count => $"{count} vols",
                },
            ];

            if (Result.StartYear is { } startYear)
            {
                parts.Add(startYear.ToString(CultureInfo.InvariantCulture));
            }

            parts.Add(Result.PublicationStatus.Words);

            return string.Join(" · ", parts);
        }
    }

    /// <summary>The romaji and native titles, leaving out any that only repeat the English one.</summary>
    private string? AlternativeTitles
    {
        get
        {
            var english = SearchTitleNormalizer.Normalize(Result.TitleEnglish);
            var others = new[] { Result.TitleRomaji, Result.TitleNative }
                .Where(title => SearchTitleNormalizer.Normalize(title) is { Length: > 0 } normalized && normalized != english)
                .ToList();

            return others.Count == 0 ? null : string.Join(" · ", others);
        }
    }

    // A different series starts from zero again: the counts set for one series mean nothing
    // for the next.
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_shownResult, Result))
        {
            _shownResult = Result;
            _start = StartingVolumes.For(Result);
        }
    }

    private Task OnAddAsync() => AddRequested.InvokeAsync(_start);
}
