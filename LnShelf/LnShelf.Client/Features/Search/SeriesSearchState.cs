namespace LnShelf.Client.Features.Search;

/// <summary>
/// The add-series flow's state container: one search, the format filter over it, the works
/// it groups into, and the add calls.
/// </summary>
// Behind the Search & add page. The page stays presentational (blazor.md) —
// everything non-visual lives here, which is why the provisional panel this
// replaced could be deleted without touching a line of it.
public sealed class SeriesSearchState(HttpClient httpClient, SeriesAddClient addClient)
{
    /// <summary>
    /// How long an add runs before the page says why it is slow. A typical add answers well
    /// inside this; one that queues behind the metadata sources' rate limits takes far longer.
    /// </summary>
    public static readonly TimeSpan SlowAddAfter = TimeSpan.FromSeconds(4);

    /// <summary>
    /// The line beside a slow add. Honest about the wait: it is the add pipeline's lookups,
    /// not the app hanging.
    /// </summary>
    public const string SlowAddNote = "Looking up volumes and release dates. This can take a moment.";

    private readonly TrackedSeriesIndex _tracked = new();

    // Stopped when the add ends, so a fast add never shows the "this can take a moment" line.
    private readonly SlowOperationWatch _slowAddWatch = new(SlowAddAfter);

    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<SeriesSearchResultResponse> Results { get; private set; } = [];

    /// <summary>Null is "All".</summary>
    public Format? FormatFilter { get; private set; }

    /// <summary>The results the format filter lets through, grouped into works.</summary>
    public IReadOnlyList<SearchWork> Works { get; private set; } = [];

    /// <summary>How many results the format filter lets through.</summary>
    public int VisibleResultCount { get; private set; }

    /// <summary>Which source answered the last search. The page names it.</summary>
    public MetadataSourceName Source { get; private set; } = MetadataSourceName.AniList;

    /// <summary>
    /// One quiet line, set only when the usual source could not be reached and another one
    /// answered instead. Null on a normal search.
    /// </summary>
    public string? DegradedNote { get; private set; }

    /// <summary>
    /// The two are separate because the page renders them differently: "Searching…"
    /// replaces the result list, and an add must not make the list it was pressed in
    /// disappear until the POST comes back.
    /// </summary>
    public bool IsSearching { get; private set; }

    public bool IsAdding { get; private set; }

    /// <summary>True while either call is in flight — what a control is disabled on.</summary>
    public bool IsBusy => IsSearching || IsAdding;

    /// <summary>
    /// The result whose add is in flight: the row's, the preview's, or the one an add-all has
    /// reached. Its controls show the busy state; every other add is merely disabled.
    /// </summary>
    public SeriesSearchResultResponse? AddingResult { get; private set; }

    /// <summary>True once the add in flight has run past <see cref="SlowAddAfter"/>.</summary>
    public bool IsAddTakingLong => _slowAddWatch.IsSlow;

    /// <summary>"Adding Overlord…", for the page's status line; null when nothing is being added.</summary>
    public string? AddingAnnouncement { get; private set; }

    /// <summary>"Adding 2 of 5…" while an add-all runs; null otherwise.</summary>
    public string? BatchProgress { get; private set; }

    /// <summary>What just happened, in one line, for the page to print.</summary>
    public string? Message { get; private set; }

    public bool HasSearched { get; private set; }

    public event Action? Changed;

    public bool IsOnShelf(SeriesSearchResultResponse result) => _tracked.SeriesIdOf(result) is not null;

    // By reference, as the page's selection is: two results can be equal as records and
    // still be two rows.
    public bool IsAddingResult(SeriesSearchResultResponse result) => ReferenceEquals(AddingResult, result);

    /// <summary>The series page a tracked result opens. Null when it is not on the shelf.</summary>
    public Guid? SeriesIdOf(SeriesSearchResultResponse result) => _tracked.SeriesIdOf(result);

    /// <summary>How many results are of one format; null counts them all.</summary>
    public int CountOf(Format? format) =>
        format is null ? Results.Count : Results.Count(result => result.Format == format);

    public void SetFormatFilter(Format? format)
    {
        FormatFilter = format;
        RegroupResults();
        Changed?.Invoke();
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            SetResults([]);
            HasSearched = false;
            Message = null;
            DegradedNote = null;
            Changed?.Invoke();
            return;
        }

        IsSearching = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            // GetAsync rather than GetFromJsonAsync, which throws on a failure status and
            // discards the body with it. The body is the useful half: the API answers 503
            // with the source's own words — "The AniList API has been temporarily disabled
            // due to severe stability issues" — and printing
            // "net_http_message_not_success_statuscode_reason" instead is what this replaces.
            using var response = await httpClient.GetAsync(
                $"api/search?q={Uri.EscapeDataString(query)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await FailAsync(response, cancellationToken);
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<SeriesSearchResponse>(cancellationToken);

            await LoadTrackedIdsAsync(cancellationToken);

            SetResults(payload?.Results ?? []);
            Source = payload?.Source ?? MetadataSourceName.AniList;
            DegradedNote = payload?.DegradedNote;
            HasSearched = true;
        }
        catch (HttpRequestException exception)
        {
            // No answer at all — the API itself is unreachable. Rendering the failure is
            // handling it; there is nothing else the browser can do about it.
            SetResults([]);
            DegradedNote = null;
            HasSearched = true;
            Message = $"Search failed: {exception.Message}";
        }
        finally
        {
            IsSearching = false;
            Changed?.Invoke();
        }
    }

    /// <param name="start">The preview's "Start with"; null adds the series untouched.</param>
    /// <returns>Null when the add failed; the page reads <see cref="Message"/> then.</returns>
    public async Task<AddSeriesResponse?> AddAsync(
        SeriesSearchResultResponse result,
        StartingVolumes? start = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        StartAdding();
        Message = null;
        MarkAdding(result, $"Adding {result.TitleEnglish}…");
        Changed?.Invoke();

        try
        {
            return await AddOneAsync(result, start, cancellationToken);
        }
        finally
        {
            StopAdding();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// "Add all 5 parts": adds every entry not yet on the shelf, one at a time and in order,
    /// and stops at the first that fails.
    /// </summary>
    /// <returns>How many were added.</returns>
    // One at a time rather than in parallel: each add costs the source a request and the API
    // a rate-limit token, and stopping at the first failure only means something if the ones
    // after it have not been sent yet.
    public async Task<int> AddAllAsync(
        IReadOnlyList<SearchWorkEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var pending = entries.Where(entry => !IsOnShelf(entry.Result)).ToList();
        var added = 0;

        StartAdding();
        Message = null;

        try
        {
            foreach (var entry in pending)
            {
                BatchProgress = $"Adding {added + 1} of {pending.Count}…";
                MarkAdding(entry.Result, $"Adding {entry.Result.TitleEnglish}, {added + 1} of {pending.Count}…");
                Changed?.Invoke();

                if (await AddOneAsync(entry.Result, start: null, cancellationToken) is null)
                {
                    Message = added == 0
                        ? $"{Message} Nothing else was added."
                        : $"{Message} Stopped there: {added} of {pending.Count} were added before it.";

                    return added;
                }

                added++;
            }

            Message = $"Added all {added}.";

            return added;
        }
        finally
        {
            StopAdding();
            BatchProgress = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Re-reads the shelf so results kept from an earlier visit do not say "On shelf" for a
    /// series that has since been deleted.
    /// </summary>
    public async Task RefreshTrackedIdsAsync(CancellationToken cancellationToken = default)
    {
        await LoadTrackedIdsAsync(cancellationToken);
        Changed?.Invoke();
    }

    private void StartAdding()
    {
        IsAdding = true;
        _slowAddWatch.Start(becameSlow: () => Changed?.Invoke());
    }

    private void MarkAdding(SeriesSearchResultResponse result, string announcement)
    {
        AddingResult = result;
        AddingAnnouncement = announcement;
    }

    private void StopAdding()
    {
        _slowAddWatch.Stop();

        IsAdding = false;
        AddingResult = null;
        AddingAnnouncement = null;
    }

    private async Task<AddSeriesResponse?> AddOneAsync(
        SeriesSearchResultResponse result,
        StartingVolumes? start,
        CancellationToken cancellationToken)
    {
        var outcome = await addClient.AddAsync(result, start, cancellationToken);
        Message = outcome.Message;

        // Remember it locally so the row flips at once, without another round trip.
        if (outcome.Added is { } added)
        {
            _tracked.Remember(result, added.SeriesId);
        }

        return outcome.Added;
    }

    private void SetResults(IReadOnlyList<SeriesSearchResultResponse> results)
    {
        Results = results;
        RegroupResults();
    }

    // Grouped once per change rather than on every render: the page re-renders on each
    // stepper press, and the grouping parses every title.
    private void RegroupResults()
    {
        IReadOnlyList<SeriesSearchResultResponse> visible = FormatFilter is { } format
            ? [.. Results.Where(result => result.Format == format)]
            : Results;

        VisibleResultCount = visible.Count;
        Works = SearchResultGrouper.Group(visible);
    }

    private async Task LoadTrackedIdsAsync(CancellationToken cancellationToken)
    {
        // Losing the marker is not worth losing the search: on failure the rows simply
        // offer Add, and the idempotent add still answers "already on the shelf".
        try
        {
            var tracked = await httpClient.GetFromJsonAsync<TrackedSeriesIdsResponse>(
                "api/series/tracked", cancellationToken);

            _tracked.Load(tracked);
        }
        catch (HttpRequestException)
        {
            // Deliberately empty: see above.
        }
    }

    private async Task FailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        SetResults([]);
        DegradedNote = null;
        HasSearched = true;

        var problem = await response.ReadProblemAsync(cancellationToken);

        Message = problem is null
            ? $"Search failed: {(int)response.StatusCode} {response.StatusCode}."
            : $"Search failed: {problem}";
    }
}
