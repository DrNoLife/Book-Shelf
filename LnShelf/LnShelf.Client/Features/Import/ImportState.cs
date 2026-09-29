using System.Net.Http.Headers;

namespace LnShelf.Client.Features.Import;

/// <summary>
/// The import flow's state container: upload, the review queue, one decision per
/// series, then commit.
/// </summary>
// Same shape as LibraryState, and for the same reason: the page, the group cards
// and the commit bar all render from one fetch, so a decision made in a card
// updates the counts in the header without a second round trip.
public sealed class ImportState(HttpClient httpClient)
{
    /// <summary>The API's own limit; rejecting locally saves a pointless upload.</summary>
    private const long MaximumUploadBytes = 8 * 1024 * 1024;

    public ImportQueueResponse? Queue { get; private set; }
    public IReadOnlyList<ImportBatchResponse> RecentBatches { get; private set; } = [];
    public ImportCommitResponse? CommitResult { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>What the API said, when it refused something.</summary>
    public string? Message { get; private set; }

    /// <summary>Set while a long upload is running, so the page can say so.</summary>
    public bool IsMatching { get; private set; }

    public event Action? Changed;

    public async Task LoadRecentBatchesAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            RecentBatches = await httpClient.GetFromJsonAsync<IReadOnlyList<ImportBatchResponse>>(
                                "api/import/batches?limit=5", cancellationToken) ?? [];
        });
    }

    public async Task UploadAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Size > MaximumUploadBytes)
        {
            Message = $"{file.Name} is {file.Size / 1024} KB; the limit is {MaximumUploadBytes / 1024} KB.";
            Changed?.Invoke();
            return;
        }

        IsMatching = true;

        // In a finally: anything RunAsync does not handle would otherwise leave the page
        // saying "matching" for good.
        try
        {
            await RunAsync(async () =>
            {
                using var form = new MultipartFormDataContent();
                using var fileContent = new StreamContent(file.OpenReadStream(MaximumUploadBytes, cancellationToken));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

                // The field name the API binds its IFormFile parameter from.
                form.Add(fileContent, "file", file.Name);

                using var response = await httpClient.PostAsync("api/import", form, cancellationToken);

                if (!await SucceededAsync(response, cancellationToken))
                {
                    return;
                }

                var batch = await response.Content.ReadFromJsonAsync<ImportBatchResponse>(cancellationToken);
                if (batch is not null)
                {
                    await OpenBatchInternalAsync(batch.BatchId, cancellationToken);
                }
            });
        }
        finally
        {
            IsMatching = false;
            Changed?.Invoke();
        }
    }

    public Task OpenBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        RunAsync(() => OpenBatchInternalAsync(batchId, cancellationToken));

    /// <summary>One decision about one series: this candidate, or skip it.</summary>
    public Task ResolveAsync(
        Guid groupId,
        int? aniListId,
        bool skip,
        bool treatReadAsOwned,
        CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var request = new ResolveImportGroupRequest
            {
                AniListId = aniListId,
                Skip = skip,
                TreatReadAsOwned = treatReadAsOwned,
            };

            using var response = await httpClient.PutAsJsonAsync(
                $"api/import/groups/{groupId}", request, cancellationToken);

            if (!await SucceededAsync(response, cancellationToken))
            {
                return;
            }

            // Re-read the whole queue rather than patching one card: the batch's
            // counts and the commit bar depend on it, and one extra local request
            // is cheaper than two sources of truth.
            if (Queue is not null)
            {
                await OpenBatchInternalAsync(Queue.Batch.BatchId, cancellationToken);
            }
        });

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            if (Queue is null)
            {
                return;
            }

            var batchId = Queue.Batch.BatchId;
            using var response = await httpClient.PostAsync($"api/import/{batchId}/commit", null, cancellationToken);

            if (!await SucceededAsync(response, cancellationToken))
            {
                return;
            }

            CommitResult = await response.Content.ReadFromJsonAsync<ImportCommitResponse>(cancellationToken);
            await OpenBatchInternalAsync(batchId, cancellationToken);
        });

    /// <summary>The review queue's manual fallback.</summary>
    public async Task<IReadOnlyList<ImportCandidateResponse>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<ImportCandidateResponse>>(
                       $"api/import/candidates?q={Uri.EscapeDataString(query.Trim())}", cancellationToken) ?? [];
        }
        catch (HttpRequestException exception)
        {
            Message = $"Search failed: {exception.Message}";
            Changed?.Invoke();
            return [];
        }
    }

    private async Task OpenBatchInternalAsync(Guid batchId, CancellationToken cancellationToken) =>
        Queue = await httpClient.GetFromJsonAsync<ImportQueueResponse>(
            $"api/import/{batchId}/queue", cancellationToken);

    /// <summary>
    /// The busy flag, the error message and the change notification in one place,
    /// so no caller can forget one of the three.
    /// </summary>
    private async Task RunAsync(Func<Task> operation)
    {
        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            await operation();
        }
        catch (HttpRequestException exception)
        {
            // Rendering the failure is handling it; the browser cannot fix a dead
            // API or an AniList outage.
            Message = exception.Message;
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            // HttpClient reports its own timeout as a cancellation. The API may well still
            // be matching, and a finished batch shows up under recent imports.
            Message = "The API took too long to answer. An import may still finish on the "
                + "server — reload in a minute and look for it under recent imports.";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Turns a refusal into the message the API sent. The API answers with
    /// ProblemDetails, whose Detail is written to be read by a person.
    /// </summary>
    private async Task<bool> SucceededAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        // Not every failure carries a ProblemDetails body — a 413 from the server's own
        // request-size limit does not — so the status code is the fallback.
        var problem = await response.ReadProblemAsync(cancellationToken);
        Message = problem ?? $"The API answered {(int)response.StatusCode} {response.StatusCode}.";
        return false;
    }
}
