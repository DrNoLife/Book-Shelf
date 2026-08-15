namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The guard that makes searching for a volume by name safe. Every title below is a real
/// Open Library title for a series on this shelf.
/// </summary>
public sealed class VolumeEditionMatcherTests
{
    [Theory]
    // The three shapes Open Library actually uses for this shelf's light novels.
    [InlineData("That Time I Got Reincarnated as a Slime", 16, "That Time I Got Reincarnated As a Slime 16")]
    [InlineData("That Time I Got Reincarnated as a Slime", 14,
        "That Time I Got Reincarnated As a Slime, Vol. 14 (light Novel)")]
    [InlineData("Overlord", 3, "Overlord, Volume 3")]

    // Punctuation varies wildly across records of one series — em dash, horizontal bar,
    // two hyphens — and TitleNormalizer folds all of them.
    [InlineData("86―EIGHTY-SIX", 4, "86--EIGHTY-SIX, Vol. 4 Under Pressure")]
    [InlineData("86―EIGHTY-SIX", 8, "86—EIGHTY-SIX, Vol. 8: Gun Smoke On The Water")]

    // A leading "The" that one side has and the other does not.
    [InlineData("The Saga of Tanya the Evil", 2, "Saga of Tanya the Evil, Vol. 2")]
    public void Matches_ARecordForTheRightVolume_IsAccepted(
        string seriesTitle,
        int volumeNumber,
        string editionTitle) =>
        VolumeEditionMatcher.Matches(seriesTitle, volumeNumber, Format.LightNovel, Edition(editionTitle))
            .ShouldBeTrue();

    [Theory]
    // The neighbouring volume, which a loose search returns constantly.
    [InlineData("That Time I Got Reincarnated as a Slime", 16, "That Time I Got Reincarnated As a Slime 15")]
    [InlineData("Overlord", 3, "Overlord, Volume 13")]

    // A different series entirely.
    [InlineData("That Time I Got Reincarnated as a Slime", 2, "Torture Princess: Fremd Torturchen, Vol. 2")]

    // A series whose title merely *starts with* this one's. The live version of this is the
    // Slime spin-off, and a containment test alone would take it.
    [InlineData("Overlord", 3, "Overlord's Unfinished Business, Vol. 3")]
    [InlineData("That Time I Got Reincarnated as a Slime", 3,
        "That Time I Got Reincarnated as a Slime: The Slime Diaries 3")]

    // No volume number at all: not evidence that it is volume 2.
    [InlineData("Torture Princess: Fremd Torturchen", 2, "Torture Princess")]
    public void Matches_ARecordThatIsNotThisVolume_IsRefused(
        string seriesTitle,
        int volumeNumber,
        string editionTitle) =>
        VolumeEditionMatcher.Matches(seriesTitle, volumeNumber, Format.LightNovel, Edition(editionTitle))
            .ShouldBeFalse();

    [Fact]
    public void Matches_ANumberInsideTheSeriesTitle_IsNotReadAsAVolume()
    {
        // The reason only the text *after* the series title is searched for a number:
        // "86―EIGHTY-SIX" carries an 86, and volume 86 does not exist.
        VolumeEditionMatcher
            .Matches("86―EIGHTY-SIX", 86, Format.LightNovel, Edition("86—EIGHTY-SIX, Vol. 4"))
            .ShouldBeFalse();
    }

    [Fact]
    public void Matches_TheMangaOfALightNovel_IsRefused()
    {
        // This shelf's worst case: the Slime light novel and the Slime manga share a title
        // and both number their volumes. Manga art on a light novel is the defect.
        VolumeEditionMatcher.Matches(
                "That Time I Got Reincarnated as a Slime",
                7,
                Format.LightNovel,
                Edition("That Time I Got Reincarnated as a Slime 7 (manga)"))
            .ShouldBeFalse();
    }

    [Fact]
    public void Matches_TheLightNovelOfAManga_IsRefusedToo()
    {
        VolumeEditionMatcher.Matches(
                "That Time I Got Reincarnated as a Slime",
                7,
                Format.Manga,
                Edition("That Time I Got Reincarnated As a Slime, Vol. 7 (light Novel)"))
            .ShouldBeFalse();
    }

    [Fact]
    public void Matches_ARecordWithNoTitle_IsRefused() =>
        VolumeEditionMatcher.Matches("Overlord", 3, Format.LightNovel, Edition(null)).ShouldBeFalse();

    [Fact]
    public void Matches_AYearInTheTitle_IsNotMistakenForAVolume()
    {
        // Whole tokens only: the 16 inside 2016 is not volume 16.
        VolumeEditionMatcher
            .Matches("Overlord", 16, Format.LightNovel, Edition("Overlord (2016 edition)"))
            .ShouldBeFalse();
    }

    [Theory]
    // The marker really is what separates them on this shelf. Both of these are real Open
    // Library titles for Slime volumes, and only the first is the light novel.
    [InlineData("That Time I Got Reincarnated As a Slime, Vol. 16 (light Novel)", true)]
    [InlineData("That Time I Got Reincarnated As a Slime 18", false)]
    public void ConfirmsMedium_ALightNovelSeries_NeedsTheMarkerSaidOutLoud(
        string editionTitle,
        bool expected) =>
        VolumeEditionMatcher.ConfirmsMedium(Format.LightNovel, Edition(editionTitle)).ShouldBe(expected);

    [Fact]
    public void ConfirmsMedium_AMangaSeries_NeedsItsOwnMarker()
    {
        VolumeEditionMatcher.ConfirmsMedium(Format.Manga, Edition("Berserk, Vol. 3 (manga)")).ShouldBeTrue();
        VolumeEditionMatcher.ConfirmsMedium(Format.Manga, Edition("Berserk, Vol. 3")).ShouldBeFalse();
    }

    [Fact]
    public void ConfirmsMedium_ARecordWithNoTitle_ConfirmsNothing() =>
        VolumeEditionMatcher.ConfirmsMedium(Format.LightNovel, Edition(null)).ShouldBeFalse();

    [Theory]
    // Apple Books' own titles, verbatim from a captured search. The store writes the
    // series name with two hyphens where the shelf holds a horizontal bar, and
    // TitleNormalizer folds both.
    [InlineData("86―EIGHTY-SIX", "86--EIGHTY-SIX, Vol. 8 (light novel)", 8)]
    [InlineData("86―EIGHTY-SIX", "86--EIGHTY-SIX, Vol. 14 (light novel)", 14)]
    [InlineData("That Time I Got Reincarnated as a Slime",
        "That Time I Got Reincarnated as a Slime, Vol. 16 (light novel)", 16)]
    public void TryMatchVolumeNumber_AStoreTitle_ReadsItsVolumeNumber(
        string seriesTitle,
        string recordTitle,
        int expected)
    {
        VolumeEditionMatcher
            .TryMatchVolumeNumber(seriesTitle, Format.LightNovel, recordTitle, out var volumeNumber)
            .ShouldBeTrue();

        volumeNumber.ShouldBe(expected);
    }

    [Theory]
    // The Slime Diaries trap, in the store's own wording this time: a spin-off whose title
    // starts with the tracked series' title and then numbers itself from one.
    [InlineData("That Time I Got Reincarnated as a Slime",
        "That Time I Got Reincarnated as a Slime: The Slime Diaries, Vol. 3 (light novel)")]

    // The manga of the same volume of the same series, one row away in a real response.
    [InlineData("86―EIGHTY-SIX", "86--EIGHTY-SIX, Vol. 1 (manga)")]

    // A German-market edition, which the store returns alongside the English ones. It has
    // no number in the three tokens after the series title — "deutsche light novel" gets
    // there first.
    [InlineData("86―EIGHTY-SIX", "86 – EIGHTY-SIX (deutsche Light Novel): Band 1")]

    // A different series entirely, which the store returns for a two-word query.
    [InlineData("86―EIGHTY-SIX",
        "Saving 80,000 Gold in Another World for my Retirement Volume 6 (light novel)")]
    public void TryMatchVolumeNumber_ARecordThatIsNotThisSeriesVolume_ReadsNothing(
        string seriesTitle,
        string recordTitle) =>
        VolumeEditionMatcher
            .TryMatchVolumeNumber(seriesTitle, Format.LightNovel, recordTitle, out _)
            .ShouldBeFalse();

    [Fact]
    public void TryMatchVolumeNumber_ASpinOffNumberedFromOne_IsIndistinguishableAndSaysSo()
    {
        // Flagged rather than hidden. "Alter.1" is a real Apple listing for a 86 side
        // story, and it does put a 1 immediately after the series title — which is exactly
        // what a genuine volume 1 looks like, so the matcher accepts it.
        //
        // What saves volume 1 is that the catalogue pass takes the first confirmed record
        // per number and Apple ranks the numbered volumes above the side stories. That is a
        // dependency on the store's ordering, and it is written down here so the next
        // change to that pass knows it exists.
        VolumeEditionMatcher
            .TryMatchVolumeNumber(
                "86―EIGHTY-SIX", Format.LightNovel, "86--EIGHTY-SIX Alter.1 (light novel)", out var number)
            .ShouldBeTrue();

        number.ShouldBe(1);
    }

    [Theory]
    [InlineData("86--EIGHTY-SIX, Vol. 8 (light novel)", true)]
    [InlineData("86--EIGHTY-SIX, Vol. 1 (manga)", false)]
    [InlineData("86--EIGHTY-SIX: Operation High School", false)]
    public void ConfirmsMedium_AStoreTitle_NeedsTheMarkerSaidOutLoud(
        string recordTitle,
        bool expected) =>
        VolumeEditionMatcher.ConfirmsMedium(Format.LightNovel, recordTitle).ShouldBe(expected);

    private static VolumeEnrichment Edition(string? title) => new()
    {
        SourceRecordKey = "/books/OL1M",
        Title = title,
        Publisher = "Yen On",
        Language = EditionLanguage.English,
    };
}
