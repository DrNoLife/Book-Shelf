namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which volumes the enrichment still asks about: the gate in front of every rate-limited
/// per-volume request.
/// </summary>
// A wrong answer here is silent both ways: a missed gap is a volume that never gets its date or
// cover, an extra one is a request a night spent on a finished volume, forever.
public sealed class VolumeGapsTests
{
    private const string EnglishIsbn = "9781975300302";

    [Theory]
    [InlineData("nothing missing", false)]
    [InlineData("not released", true)]
    [InlineData("no release date", true)]
    [InlineData("no cover", true)]
    [InlineData("no title", true)]
    [InlineData("no description", true)]
    [InlineData("an empty title", false)]
    [InlineData("an empty description", false)]
    public void NeedsAnything_OneFieldAtATime_IsTrueOnlyForARealGap(string gap, bool expected)
    {
        var volume = WithGap(gap);

        VolumeGaps.NeedsAnything(volume).ShouldBe(expected, gap);
    }

    [Fact]
    public void NeedsAnything_AnUnreleasedVolumeWithEverythingElse_IsStillAskedAbout()
    {
        // An announced date can still move, so a pre-order is never finished.
        var volume = WithGap("not released");

        volume.ReleaseDate.ShouldNotBeNull();
        VolumeGaps.NeedsAnything(volume).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("Act 1", false)]
    public void TitleUnresolved_OnlyNullIsAGap(string? title, bool expected)
    {
        // Empty is an answer ("named, and it has no subtitle"), the three-state rule
        // Volume.Title exists for.
        var volume = Complete();
        volume.Title = title;

        VolumeGaps.TitleUnresolved(volume).ShouldBe(expected);
    }

    [Theory]
    [InlineData("nothing missing", EnglishIsbn, false)]
    [InlineData("no release date", EnglishIsbn, true)]
    [InlineData("no cover", EnglishIsbn, true)]
    [InlineData("no title", EnglishIsbn, true)]
    [InlineData("no release date", null, false)]
    [InlineData("no release date", "9784040703725", false)]
    [InlineData("no release date", "9794040703720", false)]
    [InlineData("no release date", "4040703721", false)]
    [InlineData("no release date", "978-1-9753-0030-2", true)]
    // Open Library has never supplied a blurb for this shelf, so a missing description alone
    // is not worth a lookup.
    [InlineData("no description", EnglishIsbn, false)]
    public void WorthAskingByIsbn_ByGapAndIsbn_AsksOnlyForAnEnglishIsbnWithSomethingItCanFill(
        string gap, string? isbn, bool expected)
    {
        var volume = WithGap(gap);
        volume.Isbn = isbn;

        VolumeGaps.WorthAskingByIsbn(volume).ShouldBe(expected, $"{gap}, ISBN {isbn ?? "none"}");
    }

    private static Volume WithGap(string gap)
    {
        var volume = Complete();

        switch (gap)
        {
            case "nothing missing":
                break;
            case "not released":
                volume.IsReleased = false;
                break;
            case "no release date":
                volume.ReleaseDate = null;
                break;
            case "no cover":
                volume.CoverObjectKey = null;
                break;
            case "no title":
                volume.Title = null;
                break;
            case "no description":
                volume.Description = null;
                break;
            case "an empty title":
                volume.Title = string.Empty;
                break;
            case "an empty description":
                volume.Description = string.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(gap), gap, "No such gap.");
        }

        return volume;
    }

    private static Volume Complete()
    {
        var id = Guid.CreateVersion7();

        return new Volume
        {
            Id = id,
            Number = 1,
            IsReleased = true,
            ReleaseDate = new DateOnly(2017, 3, 21),
            CoverObjectKey = CoverObjectKey.ForVolumeSync(id, "OpenLibrary", ".jpg"),
            Title = "Volume 1",
            Description = "A blurb.",
            Isbn = EnglishIsbn,
        };
    }
}
