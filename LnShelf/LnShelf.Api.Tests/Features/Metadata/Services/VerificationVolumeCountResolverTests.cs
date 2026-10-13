using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The last-resort count for a verification run: the store's highest volume that is out.
/// </summary>
public sealed class VerificationVolumeCountResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResolveAsync_NoSourceStatesACount_UsesTheStoresHighestReleasedVolume()
    {
        // 86 on Apple Books: Vol. 8 is out, Vol. 14 is a pre-order. Checking 14 volumes would
        // report every per-volume source as missing a book that does not exist yet.
        var releaseSourceMatcher = Substitute.For<IReleaseSourceMatcher>();
        releaseSourceMatcher
            .MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseSourceMatch { Outcome = ReleaseMatchOutcome.NoCandidate, Explanation = "nothing found" });

        var catalogueSource = Substitute.For<IVolumeCatalogueSource>();
        catalogueSource.GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns(AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.EightySixSearch));

        var resolver = new VerificationVolumeCountResolver(
            releaseSourceMatcher,
            catalogueSource,
            new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance),
            new FixedTimeProvider(Now));

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "86―EIGHTY-SIX",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        var resolved = new ResolvedSeries
        {
            Source = MetadataSourceName.AniList,
            SourceId = 101172,
            TitleEnglish = series.TitleEnglish,
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            ListedVolumeCount = 0,
        };

        var count = await resolver.ResolveAsync(series, resolved, CancellationToken.None);

        count.Volumes.ShouldBe(8);
        count.Ground.ShouldContain("highest released");
        count.FromReleaseSource.ShouldBeFalse();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
