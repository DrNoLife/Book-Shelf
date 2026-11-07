using LnShelf.Client.Features.Import;
using Microsoft.AspNetCore.Components.Forms;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The "N added" summary belongs to the batch it committed. Opening or uploading another
/// batch takes it off the page, or it would sit above a queue it says nothing about.
/// </summary>
public sealed class ImportStateCommitResultTests
{
    private static readonly Guid CommittedBatch = Guid.CreateVersion7();
    private static readonly Guid OtherBatch = Guid.CreateVersion7();

    [Fact]
    public async Task OpenBatchAsync_AfterACommit_ClearsTheCommitSummary()
    {
        var state = await CommittedStateAsync();

        await state.OpenBatchAsync(OtherBatch);

        state.CommitResult.ShouldBeNull();
        state.Queue.ShouldNotBeNull().Batch.BatchId.ShouldBe(OtherBatch);
    }

    [Fact]
    public async Task UploadAsync_AfterACommit_ClearsTheCommitSummary()
    {
        var state = await CommittedStateAsync();
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns("export.csv");
        file.Size.Returns(3);
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream([1, 2, 3]));

        await state.UploadAsync(file);

        state.CommitResult.ShouldBeNull();
        state.Queue.ShouldNotBeNull().Batch.BatchId.ShouldBe(OtherBatch);
    }

    [Fact]
    public async Task CommitAsync_ReReadsItsOwnBatch_KeepsTheCommitSummary()
    {
        var state = await CommittedStateAsync();

        state.CommitResult.ShouldNotBeNull().BatchId.ShouldBe(CommittedBatch);
    }

    private static async Task<ImportState> CommittedStateAsync()
    {
        var handler = new ScriptedHttpHandler(Answer);
        var state = new ImportState(new HttpClient(handler) { BaseAddress = new Uri("http://api/") });

        await state.OpenBatchAsync(CommittedBatch);
        await state.CommitAsync();
        state.CommitResult.ShouldNotBeNull();

        return state;
    }

    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri.ShouldNotBeNull().AbsolutePath;

        return path switch
        {
            _ when path == $"/api/import/{CommittedBatch}/commit" => ScriptedHttpHandler.Json(new ImportCommitResponse
            {
                BatchId = CommittedBatch,
                SeriesCreated = 2,
                SeriesUpdated = 0,
                LocalOnlyVolumesCreated = 0,
                VolumeStatesWritten = 4,
                GroupsCommitted = 2,
                GroupsSkipped = 0,
            }),
            _ when path == $"/api/import/{CommittedBatch}/queue" => ScriptedHttpHandler.Json(Queue(CommittedBatch)),
            "/api/import" => ScriptedHttpHandler.Json(Batch(OtherBatch)),
            _ => ScriptedHttpHandler.Json(Queue(OtherBatch)),
        };
    }

    private static ImportQueueResponse Queue(Guid batchId) => new() { Batch = Batch(batchId), Groups = [] };

    private static ImportBatchResponse Batch(Guid batchId) => new()
    {
        BatchId = batchId,
        FileName = "export.csv",
        UploadedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
        RowCount = 2,
        GroupCount = 2,
        AutoMatched = 2,
        NeedsReview = 0,
        Resolved = 0,
        Skipped = 0,
        Committed = 0,
    };
}
