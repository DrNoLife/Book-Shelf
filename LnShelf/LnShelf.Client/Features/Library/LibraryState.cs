namespace LnShelf.Client.Features.Library;

/// <summary>
/// The library view's state container.
/// </summary>
// The named seam: the grid and the sidebar both render from one fetch instead of
// issuing their own requests, which is what lets the sidebar show counts across
// the whole format scope while the grid shows only the filtered subset.
public sealed class LibraryState(
    HttpClient httpClient,
    LibraryFilterMemory filterMemory,
    PreferencesState preferences)
{
    /// <summary>Null is the "All" tab.</summary>
    public Format? SelectedFormat { get; private set; }

    /// <summary>
    /// The status filters, normalised (<see cref="LibraryFilterSet"/>): a series shows when it
    /// fits any of them. Behind until <see cref="RestoreFiltersAsync"/> has run.
    /// </summary>
    public IReadOnlyList<LibraryFilter> SelectedFilters { get; private set; } = LibraryFilterSet.Default;

    /// <summary>
    /// The reader's default set, from their preferences: where "Clear all" goes back to.
    /// </summary>
    // Read through rather than copied, so "Save as default" moves Clear all's target at once.
    public IReadOnlyList<LibraryFilter> DefaultFilters => preferences.DefaultLibraryFilters;

    private bool _hasRestoredFilters;

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
        _hasRestoredFilters = true;
        await RememberFiltersAsync();
        await ReloadAsync(refreshOverview: true, cancellationToken);
    }

    /// <summary>
    /// Opens the shelf the way this browser left it, or on the reader's default when it
    /// remembers nothing (<see cref="LibraryFilterChoice.Resolve"/>). Once per session: after
    /// that the state container already holds the current choice. Call it once the
    /// preferences have loaded, or the default it falls back to is only the built-in one.
    /// </summary>
    public async Task RestoreFiltersAsync()
    {
        if (_hasRestoredFilters)
        {
            return;
        }

        _hasRestoredFilters = true;
        var choice = LibraryFilterChoice.Resolve(await filterMemory.ReadAsync(), DefaultFilters);
        SelectedFilters = choice.Statuses;
        SelectedFormat = choice.Format;
    }

    /// <summary>A removable chip under the search bar.</summary>
    public Task RemoveFilterAsync(LibraryFilter filter, CancellationToken cancellationToken = default) =>
        SelectFiltersAsync(LibraryFilterSelection.Remove(SelectedFilters, filter), cancellationToken);

    /// <summary>A sidebar shelf: that one filter and nothing else.</summary>
    public Task ShowOnlyAsync(LibraryFilter filter, CancellationToken cancellationToken = default) =>
        SelectFiltersAsync([filter], cancellationToken);

    /// <summary>The status chips in the filter panel: the whole new selection, remembered.</summary>
    public async Task SelectFiltersAsync(IReadOnlyList<LibraryFilter> filters, CancellationToken cancellationToken = default)
    {
        var normalized = LibraryFilterSet.Normalize(filters);
        if (normalized.SequenceEqual(SelectedFilters))
        {
            return;
        }

        SelectedFilters = normalized;

        // A choice made before the library page first opened (a sidebar shelf on another page)
        // is the reader's latest word, so the remembered filters must not be restored over it.
        _hasRestoredFilters = true;
        await RememberFiltersAsync();
        await ReloadAsync(refreshOverview: false, cancellationToken);
    }

    /// <summary>
    /// "Clear all": the status filters back to the reader's default, the format and the title
    /// filter off, in one reload rather than one per filter. This browser forgets what it
    /// remembered, so the next visit opens on the default too.
    /// </summary>
    // Back to the default rather than to All: the default is the reader's own answer to "what
    // should the shelf show", and with All one tap away in the panel, a Clear all that
    // discarded it would be the less useful of the two.
    public async Task ClearFiltersAsync(CancellationToken cancellationToken = default)
    {
        var isFormatChanging = SelectedFormat is not null;
        var isReloadNeeded = isFormatChanging || !SelectedFilters.SequenceEqual(DefaultFilters);

        SelectedFilters = DefaultFilters;
        SelectedFormat = null;
        TextFilter = string.Empty;
        _hasRestoredFilters = true;
        _visibleCards = null;
        await filterMemory.ForgetAsync();

        if (isReloadNeeded)
        {
            await ReloadAsync(refreshOverview: isFormatChanging, cancellationToken);
        }
        else
        {
            Changed?.Invoke();
        }
    }

    private Task RememberFiltersAsync() =>
        filterMemory.RememberAsync(new LibraryFilterChoice(SelectedFilters, SelectedFormat));

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
            var gridRequest = FetchAsync(SelectedFilters, token);

            // The grid's own fetch doubles as the overview when no status filter applies.
            var overviewRequest = LibraryFilterSet.IsEverything(SelectedFilters)
                ? gridRequest
                : refreshOverview || Overview is null ? FetchAsync(LibraryFilterSet.Everything, token) : null;

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

    /// <summary>Several filters travel as a repeated parameter: <c>filter=Behind&amp;filter=Reading</c>.</summary>
    private Task<LibraryResponse?> FetchAsync(IReadOnlyList<LibraryFilter> filters, CancellationToken cancellationToken)
    {
        var query = string.Join('&', filters.Select(filter => $"filter={filter}"));
        var route = SelectedFormat is null
            ? $"api/library?{query}"
            : $"api/library?{query}&format={SelectedFormat}";

        return httpClient.GetFromJsonAsync<LibraryResponse>(route, cancellationToken);
    }
}
