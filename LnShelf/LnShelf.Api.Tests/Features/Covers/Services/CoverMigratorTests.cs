using LnShelf.Api.Features.Covers.Services;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// The one-off that moves every cover out of MinIO. It must be safe to run twice and must never
/// count a cover as moved that did not arrive intact.
/// </summary>
public sealed class CoverMigratorTests : IDisposable
{
    private static readonly byte[] Cover =
        [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20, 7, 7];

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lnshelf-migrate-{Guid.CreateVersion7():N}");
    private readonly FileSystemCoverStore _target;
    private readonly CoverMigrator _migrator = new(NullLogger<CoverMigrator>.Instance);

    public CoverMigratorTests() => _target = FileSystemCoverStoreTests.StoreAt(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_CoversInTheSource_AreCopiedAndVerified()
    {
        var source = SourceHolding("anilist/1.jpg", "override/volume/a/b.webp");

        var report = await _migrator.RunAsync(source, _target, dryRun: false, CancellationToken.None);

        report.ShouldBe(new CoverMigrationReport(Scanned: 2, Copied: 2, Skipped: 0, Failed: 0));
        (await _target.ReadAsync("override/volume/a/b.webp", CancellationToken.None))!.Content.ShouldBe(Cover);
    }

    [Fact]
    public async Task RunAsync_SecondRun_SkipsWhatIsAlreadyThere()
    {
        var source = SourceHolding("anilist/1.jpg");
        await _migrator.RunAsync(source, _target, dryRun: false, CancellationToken.None);

        var report = await _migrator.RunAsync(source, _target, dryRun: false, CancellationToken.None);

        report.ShouldBe(new CoverMigrationReport(Scanned: 1, Copied: 0, Skipped: 1, Failed: 0));
    }

    [Fact]
    public async Task RunAsync_DryRun_CountsAndWritesNothing()
    {
        var report = await _migrator.RunAsync(SourceHolding("anilist/1.jpg"), _target, dryRun: true, CancellationToken.None);

        report.ShouldBe(new CoverMigrationReport(Scanned: 1, Copied: 1, Skipped: 0, Failed: 0));
        (await _target.ExistsAsync("anilist/1.jpg", CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_ACoverThatIsNotAnImage_IsCountedFailedAndTheRestStillMove()
    {
        var source = SourceHolding("anilist/broken.jpg", "anilist/1.jpg");
        source.ReadAsync("anilist/broken.jpg", Arg.Any<CancellationToken>())
            .Returns(new CoverContent("<html>not found</html>"u8.ToArray(), "image/jpeg"));

        var report = await _migrator.RunAsync(source, _target, dryRun: false, CancellationToken.None);

        report.ShouldBe(new CoverMigrationReport(Scanned: 2, Copied: 1, Skipped: 0, Failed: 1));
        (await _target.ExistsAsync("anilist/1.jpg", CancellationToken.None)).ShouldBeTrue();
    }

    // Every cover reads back labelled image/jpeg while the bytes are webp: the old store's label
    // is not what the copy goes by.
    private static ICoverStore SourceHolding(params string[] keys)
    {
        var source = Substitute.For<ICoverStore>();
        source.ListKeysAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(keys.ToAsyncEnumerable());
        source.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new CoverContent(Cover, "image/jpeg"));

        return source;
    }
}
