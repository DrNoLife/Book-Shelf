namespace LnShelf.Client.Features.Library;

/// <summary>
/// The library view's state container.
/// </summary>
// The named seam: the grid and the sidebar both render from one fetch instead of
// issuing their own requests, which is what lets the sidebar show counts across
// the whole format scope while the grid shows only the filtered subset.
public sealed class LibraryState(HttpClient httpClient)
{
    /// <summary>Null is the "All" tab.</summary>
    public Format? SelectedFormat { get; private set; }

    /// <summary>Behind is the opinionated default (feature-spec §4.6).</summary>
    public LibraryFilter SelectedFilter { get; private set; } = LibraryFilter.Behind;

    /// <summary>
    /// Type-to-filter text. Applied in the browser, not on the server: the whole
    /// format scope is already loaded, so narrowing it is a local operation and a
    /// keystroke should not cost a round trip.
    /// </summary>
    public string TextFilter { get; private set; } = string.Empty;

    public LibrarySort Sort { get; private set; } = LibrarySort.MostBehind;

    public LibraryResponse? Library { get; private set; }

    /// <summary>
    /// The whole format scope with no status filter — what "Continue reading" and
    /// "Out now" draw from, so they do not empty out when a status chip narrows the grid.
    /// </summary>
    // A second fetch rather than client-side filtering: the status filters are the
    // server's (LibraryCardFilters), and mirroring them here would be two definitions
    // of "behind". The format scope is shared, so it is refetched only when that changes.
    public LibraryResponse? Overview { get; private set; }

    public bool LoadFailed { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// The filtered, sorted set, computed once per change rather than once per read.
    /// </summary>
    // The grid reads VisibleCards three times per render, so re-filtering and re-sorting
    // inside the getter meant three passes over the whole shelf per keystroke in the
    // filter box. Every setter below invalidates it; nothing else may.
    private IReadOnlyList<LibraryCardResponse>? _visibleCards;

    /// <summary>
    /// What the grid actually renders: the server's filtered set, narrowed by the
    /// filter text and ordered by the chosen sort.
    /// </summary>
    public IReadOnlyList<LibraryCardResponse> VisibleCards => _visibleCards ??= Filtered();

    private IReadOnlyList<LibraryCardResponse> Filtered()
    {
        if (Library is null)
        {
            return [];
        }

        IEnumerable<LibraryCardResponse> cards = Library.Cards;

        if (!string.IsNullOrWhiteSpace(TextFilter))
        {
            var needle = TextFilter.Trim();
            cards = cards.Where(card => Matches(card, needle));
        }

        // MostBehind is the server's own ordering, so it is left alone rather than
        // re-derived here from a number the client would have to recompute.
        return
        [
            .. Sort switch
            {
                LibrarySort.Alphabetical => cards.OrderBy(card => card.TitleEnglish, StringComparer.OrdinalIgnoreCase),
                LibrarySort.RecentlyUpdated => cards.OrderByDescending(card => card.LastSyncedAt ?? DateTimeOffset.MinValue),
                LibrarySort.LastRead => cards.OrderByDescending(card => card.LastReadAt ?? DateOnly.MinValue),
                _ => cards,
            },
        ];
    }

    /// <summary>
    /// Matches the romaji title too, so typing "Yakumo" finds a series whose English
    /// title is something else entirely.
    /// </summary>
    private static bool Matches(LibraryCardResponse card, string needle) =>
        card.TitleEnglish.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || (card.TitleRomaji?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);

    public void SetTextFilter(string? text)
    {
        var value = text ?? string.Empty;
        if (value == TextFilter)
        {
            return;
        }

        TextFilter = value;
        _visibleCards = null;
        Changed?.Invoke();
    }

    public void SetSort(LibrarySort sort)
    {
        if (sort == Sort)
        {
            return;
        }

        Sort = sort;
        _visibleCards = null;
        Changed?.Invoke();
    }

    /// <summary>Every visit to the page starts here, so the overview is refreshed too.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(refreshOverview: true, cancellationToken);

    public async Task SelectFormatAsync(Format? format, CancellationToken cancellationToken = default)
    {
        if (SelectedFormat == format)
        {
            return;
        }

        SelectedFormat = format;
        await ReloadAsync(refreshOverview: true, cancellationToken);
    }

    public async Task SelectFilterAsync(LibraryFilter filter, CancellationToken cancellationToken = default)
    {
        if (SelectedFilter == filter)
        {
            return;
        }

        SelectedFilter = filter;
        await ReloadAsync(refreshOverview: false, cancellationToken);
    }

    /// <summary>The token of the reload in flight, cancelled when a newer one starts.</summary>
    // Clicking Manga then All in quick succession starts two fetches, and nothing made
    // them land in order: the Manga response arriving second left Manga's cards under
    // the All tab. Each reload now cancels the one before, so only the latest can land.
    private CancellationTokenSource? _reloadCancellation;

    private async Task ReloadAsync(bool refreshOverview, CancellationToken cancellationToken)
    {
        if (_reloadCancellation is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        var reload = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _reloadCancellation = reload;

        // Captured now: the next reload disposes this source, and reading Token off a
        // disposed source throws where the token itself still answers.
        var token = reload.Token;

        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            var gridRequest = FetchAsync(SelectedFilter, token);

            // The grid's own fetch doubles as the overview when no status filter applies.
            var overviewRequest = SelectedFilter == LibraryFilter.All
                ? gridRequest
                : refreshOverview || Overview is null ? FetchAsync(LibraryFilter.All, token) : null;

            var library = await gridRequest;
            var overview = overviewRequest is null ? Overview : await overviewRequest;

            // A response that finished just as it was superseded is still stale.
            token.ThrowIfCancellationRequested();
            Library = library;
            Overview = overview;
            _visibleCards = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded, or the caller gave up. The newer reload owns the page now.
            return;
        }
        catch (HttpRequestException)
        {
            // Rendering "could not reach the API" is handling it; there is
            // nothing else the client can do about a dead API.
            LoadFailed = true;
        }

        Changed?.Invoke();
    }

    private Task<LibraryResponse?> FetchAsync(LibraryFilter filter, CancellationToken cancellationToken)
    {
        var route = SelectedFormat is null
            ? $"api/library?filter={filter}"
            : $"api/library?filter={filter}&format={SelectedFormat}";

        return httpClient.GetFromJsonAsync<LibraryResponse>(route, cancellationToken);
    }
}
