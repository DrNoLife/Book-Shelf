namespace LnShelf.Api.Tests.Features.Covers.Models;

/// <summary>
/// <see cref="CoverObjectKey.IsWellFormed"/> is the path-traversal guard on the cover
/// route, and that route is a catch-all: whatever is in the URL after /api/covers/
/// arrives here as a string and is otherwise handed straight to the object store.
/// </summary>
// Worth full coverage under testing.md's "where bugs are silent and data is at stake":
// a hole here is a file-read primitive, and it is not the kind of bug using the app
// would ever surface.
public sealed class CoverObjectKeyTests
{
    /// <summary>Keys the app itself produces, which must all still be served.</summary>
    [Theory]
    [InlineData("anilist/85470.jpg")]
    [InlineData("mangaupdates/17992364412.jpg")]
    [InlineData("override/volume/0198a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b.jpg")]
    [InlineData("volume/openlibrary/0198a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b.webp")]
    [InlineData("a")]
    public void IsWellFormed_AKeyTheAppItselfBuilds_IsAccepted(string objectKey) =>
        CoverObjectKey.IsWellFormed(objectKey).ShouldBeTrue();

    /// <summary>
    /// Everything that could climb out of the prefix, or smuggle something through the
    /// store, is refused outright rather than normalised — normalising attacker input is
    /// how traversal bugs survive.
    /// </summary>
    [Theory]
    // Traversal, plain and encoded-looking. The route hands over a decoded segment, so
    // ".." is what actually arrives.
    [InlineData("../../etc/passwd")]
    [InlineData("anilist/../../../etc/passwd")]
    [InlineData("anilist/..")]
    [InlineData("..")]

    // A rooted key would escape the bucket prefix entirely.
    [InlineData("/etc/passwd")]
    [InlineData("/anilist/85470.jpg")]

    // Backslashes: a Windows separator the store would not treat as one, and the reason
    // "..\\" must not sneak past a check that only knows about "/".
    [InlineData("anilist\\85470.jpg")]
    [InlineData("..\\..\\windows\\system32")]

    // Nothing at all.
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]

    // Characters outside the allow-list, each of which means something to some layer:
    // a query string, a fragment, a null byte, a space, a quote, a wildcard.
    [InlineData("anilist/85470.jpg?raw=1")]
    [InlineData("anilist/85470.jpg#x")]
    [InlineData("anilist/85470.jpg\0")]
    [InlineData("anilist/85 470.jpg")]
    [InlineData("anilist/85470.jpg\"")]
    [InlineData("anilist/*")]
    [InlineData("anilist/85470.jpg;")]
    [InlineData("anilist/%2e%2e/secret.jpg")]
    public void IsWellFormed_AnythingThatCouldClimbOutOfThePrefix_IsRefused(string? objectKey) =>
        CoverObjectKey.IsWellFormed(objectKey).ShouldBeFalse();

    [Fact]
    public void IsWellFormed_AKeyLongerThanTheColumn_IsRefused()
    {
        // 300 is the column width, so a longer key could never have been stored and is
        // therefore never a key this shelf wrote.
        CoverObjectKey.IsWellFormed(new string('a', 300)).ShouldBeTrue();
        CoverObjectKey.IsWellFormed(new string('a', 301)).ShouldBeFalse();
    }

    /// <summary>
    /// Every key the builders produce has to pass the guard that later serves it —
    /// otherwise the shelf stores covers it cannot show.
    /// </summary>
    [Fact]
    public void EveryBuiltKey_IsAKeyTheGuardAccepts()
    {
        var volumeId = Guid.CreateVersion7();

        CoverObjectKey.IsWellFormed(
            CoverObjectKey.ForSeriesSource(MetadataSourceName.AniList, 85470, ".jpg")).ShouldBeTrue();
        CoverObjectKey.IsWellFormed(
            CoverObjectKey.ForSeriesSource(MetadataSourceName.MangaUpdates, 17992364412, ".jpg"))
            .ShouldBeTrue();
        CoverObjectKey.IsWellFormed(
            CoverObjectKey.ForVolumeOverride(volumeId, ".jpg")).ShouldBeTrue();
        CoverObjectKey.IsWellFormed(
            CoverObjectKey.ForVolumeSync(volumeId, "Open Library", ".webp")).ShouldBeTrue();
    }

    /// <summary>
    /// A source name with a space or a dot in it would otherwise build a key the guard
    /// refuses to serve.
    /// </summary>
    [Fact]
    public void ForVolumeSync_ASourceNameWithPunctuation_IsSluggedIntoTheAllowedSet()
    {
        var key = CoverObjectKey.ForVolumeSync(Guid.CreateVersion7(), "Apple Books", ".jpg");

        key.ShouldStartWith("volume/apple-books/");
        CoverObjectKey.IsWellFormed(key).ShouldBeTrue();
    }

    /// <summary>
    /// The AniList key this produces is the one the store already holds, so making
    /// ForSeriesSource the general form orphaned nothing.
    /// </summary>
    [Fact]
    public void ForSeriesSource_AniList_KeepsTheKeyShapeAlreadyInTheStore() =>
        CoverObjectKey.ForSeriesSource(MetadataSourceName.AniList, 85470, ".jpg")
            .ShouldBe("anilist/85470.jpg");

    [Theory]
    [InlineData("https://cdn.anilist.co/media/85470.jpg", ".jpg")]
    [InlineData("https://cdn.anilist.co/media/85470.jpeg", ".jpeg")]
    [InlineData("https://cdn.anilist.co/media/85470.png", ".png")]
    [InlineData("https://cdn.anilist.co/media/85470.webp", ".webp")]
    [InlineData("https://cdn.anilist.co/media/85470.JPG", ".jpg")]

    // Anything else falls back to jpeg — AniList's usual, and the one thing the store
    // must not do is invent an extension the guard would then refuse.
    [InlineData("https://cdn.anilist.co/media/85470", ".jpg")]
    [InlineData("https://cdn.anilist.co/media/85470.gif", ".jpg")]
    [InlineData("https://cdn.anilist.co/media/85470.svg", ".jpg")]
    public void ExtensionFor_ASourceUrl_IsAnAllowedExtension(string sourceUrl, string expected) =>
        CoverObjectKey.ExtensionFor(new Uri(sourceUrl)).ShouldBe(expected);
}
