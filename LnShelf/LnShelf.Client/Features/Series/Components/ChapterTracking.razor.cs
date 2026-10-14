namespace LnShelf.Client.Features.Series.Components;

public partial class ChapterTracking
{
    private static readonly IReadOnlyList<SegmentedOption<ProgressUnit>> UnitOptions =
    [
        new(ProgressUnit.Volumes, "Volumes", "Count this series in volumes"),
        new(ProgressUnit.Chapters, "Chapters", "Count this series in chapters"),
    ];

    /// <summary>A manga or web novel; the panel only places this component for those.</summary>
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    /// <summary>What the input holds right now, or null when it has not been touched.</summary>
    private string? _draft;

    private ElementReference _input;

    private bool _isCommitting;

    private string? _error;

    // "Ch. 101 of 232 · Vol 12" once a chapter map places the chapter.
    private string PositionText => Series.LastChapterRead is { } lastRead
        ? ChapterText.WithVolume(ChapterText.Position(lastRead, Series.LatestChapter), lastRead, Series.Volumes)
        : "No chapter recorded yet";

    private string? ToReadText => ChapterText.ToRead(Series.LatestChapter, Series.LastChapterRead);

    private string NextChapterText => ChapterInput.Next(Series.LastChapterRead).ToVolumeText();

    private async Task OnUnitChosenAsync(ProgressUnit progressUnit)
    {
        if (progressUnit == Series.ProgressUnit)
        {
            return;
        }

        _error = null;
        _draft = null;
        await SeriesState.SetProgressUnitAsync(progressUnit);
    }

    // Saved at once: one press is one chapter read, and the answer redraws the figure above.
    private async Task AddOneAsync()
    {
        _draft = null;
        _error = null;

        if (!await SeriesState.SetLastChapterReadAsync(ChapterInput.Next(Series.LastChapterRead)))
        {
            _error = SeriesState.Message;
        }
    }

    // Only remembers what the input holds; saving on every keystroke would store "1" and "14"
    // on the way to 142.
    private void OnInput(ChangeEventArgs args) => _draft = args.Value?.ToString();

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await CommitAsync();
        }
    }

    // Saves a readable number that differs from the stored one. An empty or unreadable draft is
    // never a save: the stored number goes back, and switching to Volumes is how a reader stops.
    private async Task CommitAsync()
    {
        if (_isCommitting || _draft is null)
        {
            return;
        }

        var chapter = ChapterInput.Parse(_draft);
        var typed = _draft;
        _draft = null;

        if (chapter is null)
        {
            _error = string.IsNullOrWhiteSpace(typed) ? null : "Type a chapter number, like 142 or 10.5.";
            await ShowStoredAsync();
            return;
        }

        if (chapter == Series.LastChapterRead)
        {
            _error = null;
            return;
        }

        // Checked here, beside the input, rather than left to the state: the page's message
        // line is far above this panel on a phone.
        if (ChapterProgress.ProblemWith(chapter.Value) is { } problem)
        {
            _error = problem;
            await ShowStoredAsync();
            return;
        }

        _isCommitting = true;

        try
        {
            if (await SeriesState.SetLastChapterReadAsync(chapter))
            {
                _error = null;
            }
            else
            {
                _error = SeriesState.Message;
                await ShowStoredAsync();
            }
        }
        finally
        {
            _isCommitting = false;
        }
    }

    // The rendered value attribute did not change, so Blazor would leave the DOM showing what
    // the reader typed; write the stored number back by hand, which keeps focus too.
    private async Task ShowStoredAsync() =>
        await JavaScript.InvokeVoidAsync("lnshelf.setInputValue", _input, ChapterInput.Format(Series.LastChapterRead));
}
