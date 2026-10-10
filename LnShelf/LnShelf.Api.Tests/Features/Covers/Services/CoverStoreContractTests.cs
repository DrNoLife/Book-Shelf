namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// What every <see cref="ICoverStore"/> promises, written once. An implementation's test class
/// inherits this and supplies the store, so a new store is held to exactly the contract the
/// callers (the fetcher, uploads, deletes, the orphan sweep) were written against.
/// </summary>
public abstract class CoverStoreContractTests
{
    private static readonly string[] SeededKeys =
        ["anilist/1.webp", "override/volume/one/a.webp", "override/volume/two/b.webp", "volume/applebooks/3.webp"];

    protected abstract ICoverStore Store { get; }

    public static TheoryData<string, string[]> Prefixes => new()
    {
        { "override/volume/", ["override/volume/one/a.webp", "override/volume/two/b.webp"] },

        // A prefix that stops mid-segment matches as a string, as S3 does.
        { "override/vol", ["override/volume/one/a.webp", "override/volume/two/b.webp"] },
        { "", SeededKeys },
        { "nothing/", [] },
    };

    /// <summary>A minimal webp: enough of the RIFF header for the format to be recognised.</summary>
    protected static byte[] Webp(byte marker = 1) =>
        [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20, marker, marker];

    [Fact]
    public async Task SaveAsync_ThenReadAsync_ReturnsTheSameBytesAndType()
    {
        var content = Webp();

        await Store.SaveAsync("anilist/85470.webp", content, "image/webp", CancellationToken.None);
        var read = await Store.ReadAsync("anilist/85470.webp", CancellationToken.None);

        read.ShouldNotBeNull();
        read.Content.ShouldBe(content);
        read.ContentType.ShouldBe("image/webp");
    }

    [Fact]
    public async Task ReadAsync_MissingKey_ReturnsNullAndExistsIsFalse()
    {
        (await Store.ReadAsync("anilist/missing.webp", CancellationToken.None)).ShouldBeNull();
        (await Store.ExistsAsync("anilist/missing.webp", CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_AfterSave_IsTrue()
    {
        await Store.SaveAsync("volume/applebooks/1.webp", Webp(), "image/webp", CancellationToken.None);

        (await Store.ExistsAsync("volume/applebooks/1.webp", CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_ExistingKey_ReplacesTheContent()
    {
        await Store.SaveAsync("anilist/1.webp", Webp(1), "image/webp", CancellationToken.None);
        await Store.SaveAsync("anilist/1.webp", Webp(2), "image/webp", CancellationToken.None);

        (await Store.ReadAsync("anilist/1.webp", CancellationToken.None))!.Content.ShouldBe(Webp(2));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheCoverAndIsSilentWhenRepeated()
    {
        await Store.SaveAsync("override/volume/a/b.webp", Webp(), "image/webp", CancellationToken.None);

        await Store.DeleteAsync("override/volume/a/b.webp", CancellationToken.None);
        await Store.DeleteAsync("override/volume/a/b.webp", CancellationToken.None);
        await Store.DeleteAsync("never/saved.webp", CancellationToken.None);

        (await Store.ExistsAsync("override/volume/a/b.webp", CancellationToken.None)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Prefixes))]
    public async Task ListKeysAsync_Prefix_ReturnsEveryKeyUnderItHoweverDeep(string prefix, string[] expected)
    {
        foreach (var key in SeededKeys)
        {
            await Store.SaveAsync(key, Webp(), "image/webp", CancellationToken.None);
        }

        var keys = await Store.ListKeysAsync(prefix, CancellationToken.None).ToListAsync();

        keys.Order(StringComparer.Ordinal).ShouldBe(expected);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWritersOnOneKey_LeaveOneCompleteCover()
    {
        var candidates = Enumerable.Range(1, 8).Select(marker => Webp((byte)marker)).ToList();

        await Task.WhenAll(candidates.Select(
            content => Store.SaveAsync("anilist/race.webp", content, "image/webp", CancellationToken.None)));

        var read = await Store.ReadAsync("anilist/race.webp", CancellationToken.None);
        read.ShouldNotBeNull();
        candidates.ShouldContain(content => content.SequenceEqual(read.Content));
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_Throws()
    {
        await Store.SaveAsync("anilist/1.webp", Webp(), "image/webp", CancellationToken.None);

        await Should.ThrowAsync<OperationCanceledException>(
            () => Store.ReadAsync("anilist/1.webp", new CancellationToken(canceled: true)));
    }
}
