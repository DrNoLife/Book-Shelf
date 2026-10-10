using System.Net;
using System.Text.Json;
using LnShelf.Client.Features.Import;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The sidebar's "N to review" pill: series still waiting for a decision, counted across the
/// batches that have not been applied to the shelf yet.
/// </summary>
public sealed class ImportStateReviewCountTests
{
    [Fact]
    public async Task PendingReviewCount_OnlyBatchesNotYetCommitted_AreCounted()
    {
        using var handler = new JsonHandler(
            [
                CreateBatch(needsReview: 3),
                CreateBatch(needsReview: 2, committed: true),
                CreateBatch(needsReview: 1),
            ]);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };
        var state = new ImportState(client);

        await state.RefreshReviewCountAsync();

        state.PendingReviewCount.ShouldBe(4);
        state.HasLoadedBatches.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshReviewCountAsync_TheApiIsDown_ShowsNoCountAndNoMessage()
    {
        using var handler = new JsonHandler([], HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };
        var state = new ImportState(client);

        await state.RefreshReviewCountAsync();

        // An error on a page the user has not opened would be worse than a missing count.
        state.PendingReviewCount.ShouldBe(0);
        state.Message.ShouldBeNull();
        state.HasLoadedBatches.ShouldBeTrue();
    }

    private static ImportBatchResponse CreateBatch(int needsReview, bool committed = false) =>
        new()
        {
            BatchId = Guid.CreateVersion7(),
            FileName = "export.csv",
            UploadedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
            RowCount = 10,
            GroupCount = 5,
            AutoMatched = 5 - needsReview,
            NeedsReview = needsReview,
            Resolved = 0,
            Skipped = 0,
            Committed = committed ? 5 : 0,
            CommittedAt = committed ? new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero) : null,
        };

    /// <summary>Answers every request with one canned list of batches, or a bare status.</summary>
    private sealed class JsonHandler(IReadOnlyList<ImportBatchResponse> batches, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(batches, JsonSerializerOptions.Web),
                    Encoding.UTF8,
                    "application/json"),
            });
    }
}
