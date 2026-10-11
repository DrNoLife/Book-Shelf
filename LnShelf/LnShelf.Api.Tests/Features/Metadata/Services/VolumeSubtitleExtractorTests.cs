namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// What a source's full record title leaves behind once the series and the number are taken
/// out of it. Every string below is a real Apple Books or Open Library title for a volume on
/// this shelf.
/// </summary>
public sealed class VolumeSubtitleExtractorTests
{
    [Theory]
    // Apple Books, which titles with a comma and an abbreviated marker.
    [InlineData("86—EIGHTY-SIX, Vol. 3: Run Through the Battlefront (Finish)", 3, "Run Through the Battlefront (Finish)")]
    [InlineData("86--EIGHTY-SIX, Vol. 4 Under Pressure", 4, "Under Pressure")]
    // Open Library, which spells the word out and separates with a colon.
    [InlineData("Overlord, Volume 3: The Bloody Valkyrie", 3, "The Bloody Valkyrie")]
    [InlineData("Overlord, Volume 15: The Half-Elf Demigod Part I", 15, "The Half-Elf Demigod Part I")]
    // A record whose separator arrived with a space in front of it, and a stray bracket the
    // source itself left behind. Neither is this class's to correct.
    [InlineData("86--EIGHTY-SIX, Vol. 5 : Death, Be Not Proud )", 5, "Death, Be Not Proud )")]
    // Open Library puts the medium marker *between* the number and the subtitle, where an
    // end-anchored strip could not reach it. Both of these reached live captions as
    // "(light novel): …" before the marker rule stopped depending on position.
    [InlineData("Saga of Tanya the Evil, Vol. 8 (light Novel): In Omnia Paratus", 8, "In Omnia Paratus")]
    [InlineData(
        "The Hero Laughs While Walking the Path of Vengeance a Second Time, Vol. 4 (light novel): "
        + "The Merchant, Mired in Greed",
        4,
        "The Merchant, Mired in Greed")]
    public void Extract_ARecordWithASubtitle_KeepsOnlyTheSubtitle(
        string recordTitle, int volumeNumber, string expected) =>
        VolumeSubtitleExtractor.Extract(recordTitle, volumeNumber).ShouldBe(expected);

    [Theory]
    [InlineData("That Time I Got Reincarnated as a Slime, Vol. 17", 17)]
    [InlineData("That Time I Got Reincarnated as a Slime, Vol. 1 (light novel)", 1)]
    [InlineData("86--EIGHTY-SIX, Vol. 14 (light novel)", 14)]
    [InlineData("86--EIGHTY-SIX, Vol. 6 (light Novel)", 6)]
    [InlineData("86--EIGHTY-SIX, Vol. 1 (manga)", 1)]
    [InlineData("Overlord, Vol. 1 (light novel)", 1)]
    // Open Library spelling the series title out and ending on the number itself.
    [InlineData("That Time I Got Reincarnated As a Slime 16", 16)]
    // The volume's own subtitle sits before the marker here, which makes it the *part's*
    // subtitle rather than the volume's — and the series is already tracked as "Part 1".
    [InlineData(
        "Ascendance of a Bookworm: I'll Do Anything to Become a Librarian! Part 1: "
        + "Daughter of a Soldier, Vol. 1",
        1)]
    // The leftover here is the series name in brackets, twice over — an annotation the record
    // made about itself, not a name for the volume. Left in, it captioned Slime volume 3 with
    // its own series title.
    [InlineData(
        "That Time I Got Reincarnated as a Slime, Vol. 3 (light novel) "
        + "(That Time I Got Reincarnated as a Slime (light novel))",
        3)]
    public void Extract_ARecordNamingNothingButTheVolume_IsEmptyRatherThanNull(
        string recordTitle, int volumeNumber) =>
        // Empty is the answer "named, and it has no subtitle", which is what stops
        // enrichment asking about this volume again every night (Volume.Title).
        VolumeSubtitleExtractor.Extract(recordTitle, volumeNumber).ShouldBe(string.Empty);

    [Fact]
    public void Extract_TheSameVolumeSpelledDifferentlyByEachSource_GivesTheSameSubtitle()
    {
        // The reason the series title is not consulted at all: Apple sends "86--EIGHTY-SIX",
        // the shelf holds "86―EIGHTY-SIX" and Open Library has "86—EIGHTY‐SIX" with a
        // non-breaking hyphen. Anchoring on the number sidesteps every one of them.
        const string Expected = "Run Through the Battlefront (Start)";

        VolumeSubtitleExtractor.Extract("86--EIGHTY-SIX, Vol. 2: Run Through the Battlefront (Start)", 2)
            .ShouldBe(Expected);
        VolumeSubtitleExtractor.Extract("86—EIGHTY‐SIX, Vol. 2: Run Through the Battlefront (Start)", 2)
            .ShouldBe(Expected);
    }

    [Fact]
    public void Extract_ATitleWithNoMarkerAtAll_IsKeptWhole()
    {
        // Open Library carries editions titled with nothing but the volume's own name. There
        // is no prefix to cut away, so cutting anything would be inventing a rule.
        VolumeSubtitleExtractor.Extract("Run Through the Battlefield", 2)
            .ShouldBe("Run Through the Battlefield");
    }

    [Theory]
    [InlineData("Torture Princess: Fremd Torturchen, Vol. 7.5 (light novel)", "Torture Princess: Fremd Torturchen, Vol. 7.5")]
    [InlineData("Torture Princess: Fremd Torturchen, Vol. 7. 5", "Torture Princess: Fremd Torturchen, Vol. 7. 5")]
    public void Extract_ABonusVolumeAskedForAsTheWholeNumber_IsNotCutAtTheFraction(
        string recordTitle, string expected) =>
        // "Vol. 7.5" names no volume 7, so there is no marker to cut after. Cut at the 7, the
        // bonus volume's record captioned volume 7 ".5".
        VolumeSubtitleExtractor.Extract(recordTitle, 7).ShouldBe(expected);

    [Theory]
    // The number arrives as text because an attribute cannot carry a decimal.
    [InlineData("Torture Princess: Fremd Torturchen, Vol. 7.5 (light novel)", "7.5", "")]
    [InlineData("Torture Princess: Fremd Torturchen, Vol. 7. 5", "7.5", "")]
    [InlineData("Classroom of the Elite, Vol. 11.5: Short Stories", "11.5", "Short Stories")]
    public void Extract_ABonusVolume_CutsAfterItsWholeNumber(string recordTitle, string volumeNumber, string expected) =>
        VolumeSubtitleExtractor.Extract(recordTitle, decimal.Parse(volumeNumber, CultureInfo.InvariantCulture))
            .ShouldBe(expected);

    [Fact]
    public void Extract_ANumberInsideALargerOne_IsNotMistakenForTheMarker()
    {
        // "Saving 80,000 Gold in Another World for my Retirement" is a real series title, and
        // a bare-number search that accepted the thousands separator would cut it at "80".
        VolumeSubtitleExtractor.Extract("Saving 80,000 Gold in Another World for my Retirement", 80)
            .ShouldBe("Saving 80,000 Gold in Another World for my Retirement");
    }

    [Fact]
    public void Extract_AMarkerForADifferentVolume_IsNotUsed()
    {
        // The number is the anchor, so a record that names some other volume is not cut at
        // its marker — nothing here claims to be volume 3.
        VolumeSubtitleExtractor.Extract("86--EIGHTY-SIX, Vol. 8 (light novel)", 3)
            .ShouldBe("86--EIGHTY-SIX, Vol. 8");
    }

    [Fact]
    public void Extract_ASubtitleEndingOnTheSameNumber_IsCutAtTheMarkerRatherThanTheLastMatch()
    {
        // Constructed: no title on this shelf carries the number twice. The rule still has
        // to be written one way round, and anchoring on the last occurrence would leave this
        // volume with no subtitle at all instead of its own.
        VolumeSubtitleExtractor.Extract("Overlord, Vol. 1: The Undead King, Part 1", 1)
            .ShouldBe("The Undead King, Part 1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_ARecordWithNoTitle_IsEmpty(string? recordTitle) =>
        VolumeSubtitleExtractor.Extract(recordTitle, 1).ShouldBe(string.Empty);
}
