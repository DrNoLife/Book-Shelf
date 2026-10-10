using LnShelf.Api.Features.Covers.Services;
using Microsoft.Extensions.Hosting;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// The folder store against a real temporary folder: the shared contract, plus what only a
/// filesystem can get wrong — keys that would leave the folder, staging files, and content
/// types that have to come from the bytes.
/// </summary>
public sealed class FileSystemCoverStoreTests : CoverStoreContractTests, IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lnshelf-covers-{Guid.CreateVersion7():N}");

    public FileSystemCoverStoreTests() => Store = StoreAt(_folder);

    protected override ICoverStore Store { get; }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.webp")]
    [InlineData("anilist/../../outside.webp")]
    [InlineData("/etc/passwd")]
    [InlineData("anilist//1.webp")]
    [InlineData("anilist/./1.webp")]
    [InlineData("anilist/")]
    [InlineData(".tmp/staged")]
    [InlineData("anilist/.hidden")]
    [InlineData("anilist\\1.webp")]
    public async Task SaveAsync_KeyThatCouldLeaveOrAliasTheFolder_IsRefused(string key)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => Store.SaveAsync(key, Webp(), "image/webp", CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_SiblingFolderWithTheSamePrefix_IsNeverReached()
    {
        // "covers2" starts with "covers"; the guard compares with the separator, so a key can
        // never resolve into the neighbour.
        var sibling = _folder + "2";

        await Should.ThrowAsync<ArgumentException>(
            () => Store.SaveAsync($"../{Path.GetFileName(sibling)}/x.webp", Webp(), "image/webp", CancellationToken.None));
        Directory.Exists(sibling).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_RootConfiguredWithATrailingSeparator_StillWorks()
    {
        var store = StoreAt(_folder + Path.DirectorySeparatorChar);

        await store.SaveAsync("anilist/1.webp", Webp(), "image/webp", CancellationToken.None);

        File.Exists(Path.Combine(_folder, "anilist", "1.webp")).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_ContentThatIsNotTheDeclaredType_IsRefused()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        await Should.ThrowAsync<ArgumentException>(
            () => Store.SaveAsync("anilist/1.webp", png, "image/webp", CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(
            () => Store.SaveAsync("anilist/1.webp", "not an image"u8.ToArray(), "image/webp", CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_KeyEndingInJpgHoldingWebp_ReportsWebp()
    {
        await Store.SaveAsync("anilist/85470.jpg", Webp(), "image/webp", CancellationToken.None);

        (await Store.ReadAsync("anilist/85470.jpg", CancellationToken.None))!.ContentType.ShouldBe("image/webp");
    }

    [Fact]
    public async Task ListKeysAsync_LeftoverStagingFile_IsNeverListed()
    {
        await Store.SaveAsync("anilist/1.webp", Webp(), "image/webp", CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(_folder, ".tmp", "interrupted"), Webp());

        var keys = await Store.ListKeysAsync(string.Empty, CancellationToken.None).ToListAsync();

        keys.ShouldBe(["anilist/1.webp"]);
    }

    [Fact]
    public async Task SaveAsync_Completed_LeavesNothingStaged()
    {
        await Store.SaveAsync("anilist/1.webp", Webp(), "image/webp", CancellationToken.None);

        Directory.EnumerateFileSystemEntries(Path.Combine(_folder, ".tmp")).ShouldBeEmpty();
    }

    internal static FileSystemCoverStore StoreAt(string folder)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(Path.GetTempPath());

        var root = new CoverStoreRoot(Options.Create(new FileSystemCoverStoreOptions { RootPath = folder }), environment);

        return new FileSystemCoverStore(root);
    }
}
