using LnShelf.Client.Features.Library;

namespace LnShelf.Client.Features.Search.Components;

public partial class ManualSeriesForm
{
    /// <summary>What was searched for, offered as the title when the form opens.</summary>
    [Parameter]
    public string? SuggestedTitle { get; set; }

    /// <summary>The search's format filter, offered as the format; web novel when there is none.</summary>
    [Parameter]
    public Format? SuggestedFormat { get; set; }

    [Inject]
    public required SeriesAddClient AddClient { get; set; }

    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private static readonly IReadOnlyList<SegmentedOption<Format>> FormatOptions =
    [
        new(Format.WebNovel, "WN", "Web novel"),
        new(Format.LightNovel, "LN", "Light novel"),
        new(Format.Manga, "Manga", "Manga"),
    ];

    private ElementReference _openButton;
    private ElementReference _titleInput;
    private FocusTarget _pendingFocus;
    private bool _isOpen;
    private bool _isSaving;
    private string _title = string.Empty;
    private string? _author;
    private int? _volumeCount;
    private Format _format = Format.WebNovel;
    private string? _message;

    private bool CanSubmit => !string.IsNullOrWhiteSpace(_title);

    private void Open()
    {
        _title = SuggestedTitle?.Trim() ?? string.Empty;
        _format = SuggestedFormat ?? Format.WebNovel;
        _author = null;
        _volumeCount = null;
        _message = null;
        _isOpen = true;
        _pendingFocus = FocusTarget.Title;
    }

    private void Close()
    {
        _isOpen = false;
        _pendingFocus = FocusTarget.OpenButton;
    }

    // After the render, because the element focus moves to only exists once it has happened.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var target = _pendingFocus;
        _pendingFocus = FocusTarget.None;

        switch (target)
        {
            case FocusTarget.Title:
                await _titleInput.FocusAsync();
                break;
            case FocusTarget.OpenButton:
                await _openButton.FocusAsync();
                break;
        }
    }

    /// <summary>Adds it, then opens its page, which is where its volumes and cover are set.</summary>
    private async Task OnSubmitAsync()
    {
        if (!CanSubmit || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _message = null;

        try
        {
            var outcome = await AddClient.AddManualAsync(
                new AddManualSeriesRequest
                {
                    TitleEnglish = _title.Trim(),
                    Format = _format,
                    Author = string.IsNullOrWhiteSpace(_author) ? null : _author.Trim(),
                    VolumeCount = _volumeCount,
                },
                CancellationToken.None);

            if (outcome.Added is not { } added)
            {
                _message = outcome.Message;
                return;
            }

            // The library's cached response would not have it yet, as SearchPage does for a search add.
            if (LibraryState.Library is not null)
            {
                await LibraryState.LoadAsync();
            }

            Navigation.NavigateTo($"series/{added.SeriesId}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private enum FocusTarget
    {
        None,
        Title,
        OpenButton,
    }
}
