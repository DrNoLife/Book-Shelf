namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The two halves of <c>enrichment-verify</c> that do not need the internet: what it counts
/// as filled in, and what it accepts as working.
/// </summary>
// The command itself is a live check by definition and cannot be asserted here. Its
// judgement can, and has to be: a threshold that only ever runs against the real internet
// is a threshold nobody can change with any confidence.
public sealed class VerificationVerdictTests
{
    private static readonly VerifiedImage AnyImage = new() { ByteCount = 1024, Width = 600, Height = 900 };

    [Fact]
    public void Count_VolumesWithEveryFieldFilled_CountsAllOfThem()
    {
        var volumes = Volumes(3, volume =>
        {
            volume.Title = "Run Through the Battlefront";
            volume.Description = "A blurb.";
            volume.CoverObjectKey = "verify/whatever";
            volume.ReleaseDate = new DateOnly(2024, 5, 1);
        });

        var counted = VerificationVerdict.Count(volumes, [AnyImage]);

        counted.Total.ShouldBe(3);
        counted.Matched.ShouldBe(3);
        counted.Titled.ShouldBe(3);
        counted.Described.ShouldBe(3);
        counted.Covered.ShouldBe(3);
        counted.Dated.ShouldBe(3);
    }

    [Fact]
    public void Count_AVolumeNamedWithNoSubtitle_IsMatchedButNotTitled()
    {
        // Volume.Title's three states, counted apart. Every volume of Slime is titled only
        // "…, Vol. N", so the catalogue reaching all of them stores "" for all of them —
        // which is a working pipeline, and would read as a total failure under one count.
        var counted = VerificationVerdict.Count(
            Volumes(3, volume => volume.Title = string.Empty), []);

        counted.Matched.ShouldBe(3);
        counted.Titled.ShouldBe(0);
    }

    [Fact]
    public void Count_AVolumeWithNothingOnIt_CountsTowardsTheTotalOnly()
    {
        var counted = VerificationVerdict.Count(Volumes(2, _ => { }), []);

        counted.Total.ShouldBe(2);
        counted.Matched.ShouldBe(0);
        counted.Titled.ShouldBe(0);
        counted.Described.ShouldBe(0);
        counted.Covered.ShouldBe(0);
        counted.Dated.ShouldBe(0);
    }

    [Fact]
    public void Count_CoversThatWereDecoded_AreReportedAsBytesAndTheLargest()
    {
        VerifiedImage[] covers =
        [
            new() { ByteCount = 100, Width = 600, Height = 900 },
            new() { ByteCount = 250, Width = 1400, Height = 2100 },
        ];

        var counted = VerificationVerdict.Count(Volumes(2, _ => { }), covers);

        // The evidence behind the "covered" count: a set column proves nothing on its own.
        counted.CoverByteCount.ShouldBe(350);
        counted.LargestCover!.Width.ShouldBe(1400);
    }

    [Fact]
    public void Judge_EverySeriesFieldPresentAndVolumesWellAboveTheBars_Passes()
    {
        VerificationVerdict.Judge(CompleteSeriesLevel(), VolumeLevel(10, 10, 10, 10, 10))
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "A blurb.", true)]
    [InlineData("A title", null, true)]
    [InlineData("A title", "A blurb.", false)]
    public void Judge_AnyMissingSeriesField_Fails(string? title, string? description, bool hasCover)
    {
        var seriesLevel = new SeriesLevelVerification
        {
            Title = title,
            Description = description,
            Cover = hasCover ? AnyImage : null,
        };

        // The series level is all-or-nothing on purpose: three fields from one request is
        // not a place where a partial answer is normal.
        VerificationVerdict.Judge(seriesLevel, VolumeLevel(10, 10, 10, 10, 10))
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(7, 10, 10, 10)]  // matched below 80%
    [InlineData(10, 7, 10, 10)]  // described below 80%
    [InlineData(10, 10, 7, 10)]  // covered below 80%
    [InlineData(10, 10, 10, 6)]  // dated below 70%
    public void Judge_AnyVolumeShareBelowItsBar_Fails(int matched, int described, int covered, int dated)
    {
        VerificationVerdict.Judge(
                CompleteSeriesLevel(), VolumeLevel(10, matched, described, covered, dated))
            .ShouldBeFalse();
    }

    [Fact]
    public void Judge_EveryVolumeMatchedAndNoneOfThemSubtitled_Passes()
    {
        // The Slime case, and the reason "titled" stopped being a threshold: the catalogue
        // named all ten and the publisher gave none of them a subtitle. Nothing is wrong.
        var volumeLevel = VolumeLevel(10, 10, 10, 10, 10) with { Titled = 0 };

        VerificationVerdict.Judge(CompleteSeriesLevel(), volumeLevel).ShouldBeTrue();
    }

    [Theory]
    [InlineData(10, 8, 8, 8, 7)]     // exactly 80/80/80/70
    [InlineData(5, 4, 4, 4, 4)]      // 80% of five is four, and four passes
    [InlineData(14, 14, 14, 14, 13)] // the measured shape of all three canaries today
    public void Judge_ExactlyOnTheBar_Passes(int total, int matched, int described, int covered, int dated)
    {
        // Integer arithmetic decides this, so "exactly on the threshold" has one answer
        // rather than a rounding argument.
        VerificationVerdict.Judge(
                CompleteSeriesLevel(), VolumeLevel(total, matched, described, covered, dated))
            .ShouldBeTrue();
    }

    [Fact]
    public void Judge_ASeriesThatWasNeverResolved_Fails()
    {
        VerificationVerdict.Judge(null, VolumeLevel(10, 10, 10, 10, 10)).ShouldBeFalse();
    }

    [Fact]
    public void Judge_NoVolumesAtAll_Fails()
    {
        // Zero of zero is not a hundred per cent. A series nothing could be counted for is
        // the loudest failure this command has.
        VerificationVerdict.Judge(CompleteSeriesLevel(), VolumeLevel(0, 0, 0, 0, 0))
            .ShouldBeFalse();
    }

    [Fact]
    public void Passed_OneSeriesFailing_FailsTheWholeRun()
    {
        var report = new EnrichmentVerificationReport
        {
            Format = Format.LightNovel,
            Series = [Series(passed: true), Series(passed: false), Series(passed: true)],
        };

        report.Passed.ShouldBeFalse();
    }

    [Fact]
    public void Passed_NothingChecked_IsNotAPass()
    {
        // "Nothing was checked" must never be reported in the same word as "it works".
        new EnrichmentVerificationReport { Format = Format.LightNovel, Series = [] }
            .Passed.ShouldBeFalse();
    }

    private static SeriesLevelVerification CompleteSeriesLevel() => new()
    {
        Title = "The Saga of Tanya the Evil",
        Description = "A blurb long enough to be a blurb.",
        Cover = AnyImage,
    };

    /// <summary>
    /// Subtitles default to the matched count, so a case that says nothing about them is
    /// judged on the numbers it does state.
    /// </summary>
    private static VolumeLevelVerification VolumeLevel(
        int total, int matched, int described, int covered, int dated) => new()
        {
            Total = total,
            Matched = matched,
            Titled = matched,
            Described = described,
            Covered = covered,
            Dated = dated,
            CoverByteCount = 0,
        };

    private static SeriesVerification Series(bool passed) => new()
    {
        SearchTerm = "anything",
        Format = Format.LightNovel,
        VolumeCountGround = "14 from the release source",
        Passed = passed,
    };

    private static List<Volume> Volumes(int count, Action<Volume> fill)
    {
        var volumes = new List<Volume>(count);

        for (var number = 1; number <= count; number++)
        {
            var volume = new Volume { Id = Guid.CreateVersion7(), Number = number };
            fill(volume);
            volumes.Add(volume);
        }

        return volumes;
    }
}
