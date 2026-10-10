using LnShelf.Api.Tests.Features.Import.Fixtures;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Which importer reads the uploaded file, decided by the file itself.
/// </summary>
// The seam's own test: two implementations registered under IShelfImporter, and
// the upload picking one of them. Before this, a second registration silently
// replaced the first — the interface said "a Goodreads export is a new
// implementation" and the wiring made it a replacement.
public sealed class ImportSourceResolverTests
{
    [Fact]
    public async Task ResolveAsync_AStoryGraphExport_PicksTheStoryGraphImporter()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        await using var export = StoryGraphExportFixture.OpenRead();

        var importer = await resolver.ResolveAsync(export, CancellationToken.None);

        importer.SourceName.ShouldBe("StoryGraph");
    }

    [Fact]
    public async Task ResolveAsync_AGoodreadsExport_PicksTheGoodreadsImporter()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        await using var export = GoodreadsExportFixture.OpenRead();

        var importer = await resolver.ResolveAsync(export, CancellationToken.None);

        importer.SourceName.ShouldBe("Goodreads");
    }

    [Fact]
    public async Task ResolveAsync_AnyExport_LeavesTheStreamAtTheStartForTheImporter()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var builder = ImportPipelineHarness.CreateBuilder(dbContext);
        var resolver = ImportPipelineHarness.CreateResolver(builder);

        await using var export = GoodreadsExportFixture.OpenRead();

        var importer = await resolver.ResolveAsync(export, CancellationToken.None);

        // Sniffing consumed the header row; the importer has to see it again or the
        // first column name becomes the first book.
        export.Position.ShouldBe(0);

        var batch = await importer.CreateBatchAsync(
            GoodreadsExportFixture.FileName, export, CancellationToken.None);

        batch.RowCount.ShouldBe(GoodreadsExportFixture.ExpectedBookCount);
    }

    [Theory]

    // A spreadsheet that is not a library export at all.
    [InlineData("Name,Quantity,Price")]

    // A MyAnimeList export: a real thing, and not one of the two implemented.
    [InlineData("anime_id,series_title,my_status,my_score")]

    // Half a StoryGraph header. One shared column name must not claim the file.
    [InlineData("Title,Authors,Read Status,Owned?")]

    // Half a Goodreads header, likewise.
    [InlineData("Book Id,Title,Author,ISBN13,Date Read")]

    [InlineData("")]
    public async Task ResolveAsync_AHeaderNoImporterKnows_IsRefusedAndNamesWhatIsSupported(string headerLine)
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(headerLine + "\n"));

        var exception = await Should.ThrowAsync<InvalidInputException>(
            async () => await resolver.ResolveAsync(stream, CancellationToken.None));

        // The refusal is the only place the user learns which exports work, so it
        // has to name them rather than say "unsupported file".
        exception.Message.ShouldContain("StoryGraph");
        exception.Message.ShouldContain("Goodreads");
    }

    [Fact]
    public async Task ResolveAsync_AHeaderTwoImportersClaim_IsRefusedRatherThanGuessed()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        // A file carrying both sources' marker columns. Nothing exports this, but
        // the rules could one day overlap — and staging someone's history through
        // the wrong reader is worse than refusing it.
        const string headerLine = "Book Id,Title,Read Status,Dates Read,Exclusive Shelf";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(headerLine + "\n"));

        var exception = await Should.ThrowAsync<InvalidInputException>(
            async () => await resolver.ResolveAsync(stream, CancellationToken.None));

        exception.Message.ShouldContain("more than one");
    }

    [Fact]
    public async Task ResolveAsync_AFileWithAByteOrderMark_StillRecognisesTheFirstColumn()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        // StoryGraph writes a UTF-8 BOM. It sits in front of the first column name,
        // and sniffing on the raw bytes would never match a header that starts with
        // the marker column.
        using var stream = new MemoryStream(
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("Read Status,Dates Read,Title\n")]);

        var importer = await resolver.ResolveAsync(stream, CancellationToken.None);

        importer.SourceName.ShouldBe("StoryGraph");
    }

    [Theory]
    [InlineData("Title,Authors,Read Status,Last Date Read,Dates Read,Owned?", "StoryGraph")]
    [InlineData("Book Id,Title,Author,Exclusive Shelf,Owned Copies", "Goodreads")]
    public async Task ResolveAsync_AHeaderOnlyOneImporterKnows_PicksThatOne(
        string headerLine, string expectedSourceName)
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(headerLine + "\n"));

        var importer = await resolver.ResolveAsync(stream, CancellationToken.None);

        importer.SourceName.ShouldBe(expectedSourceName);
    }

    [Fact]
    public async Task ResolveAsync_ANonSeekableStream_IsRefusedAsInvalidInputRatherThanA500()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var resolver = ImportPipelineHarness.CreateResolver(ImportPipelineHarness.CreateBuilder(dbContext));

        // ASP.NET Core buffers an upload before this is ever reached, so a non-seekable
        // stream here means a caller outside that buffering — a defect in the request,
        // not the server. That has to surface as a 400, so this pins the exception type.
        using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes("Title,Authors\n"));

        var exception = await Should.ThrowAsync<InvalidInputException>(
            async () => await resolver.ResolveAsync(stream, CancellationToken.None));

        exception.Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A stream that reports it cannot seek, for the one path that refuses it.</summary>
    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
