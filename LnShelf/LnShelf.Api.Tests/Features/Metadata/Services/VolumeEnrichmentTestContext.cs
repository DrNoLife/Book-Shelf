namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The shelf rows and the source doubles the enrichment test files all arrange.
/// </summary>
// The companion to VolumeEnrichmentPipeline, which builds the service: this builds what the
// service is handed. Four files were each carrying their own copy of the same seven
// substitutes, so a seam growing a member meant editing four identical blocks.
//
// Imported with `using static`, so a test still reads Series(Volume(3, ...)) rather than
// naming this class. Where a file's own version of a builder differs — Overlord's AniList
// id, a store record that carries artwork — the file keeps a one-line forwarder that names
// its own facts and delegates the shape here.
internal static class VolumeEnrichmentTestContext
{
    public static LookupBudget Budget(int maximum) => new(maximum);

    public static SyncRun Run(DateOnly today) => new()
    {
        Id = Guid.CreateVersion7(),
        StartedAt = new DateTimeOffset(today, TimeOnly.MinValue, TimeSpan.Zero),
    };

    /// <param name="alternativeTitles">
    /// The other names a source files this series under — what the catalogue pass arbitrates
    /// between. Empty is the ordinary AniList-added series.
    /// </param>
    public static Series Series(
        string titleEnglish,
        Volume[] volumes,
        int? aniListId = null,
        long? mangaUpdatesId = null,
        string[]? alternativeTitles = null,
        PublicationStatus publicationStatus = PublicationStatus.Ongoing)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            TitleEnglish = titleEnglish,
            AlternativeTitles = alternativeTitles ?? [],
            Format = Format.LightNovel,
            PublicationStatus = publicationStatus,
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(volume);
        }

        return series;
    }

    /// <summary>
    /// A volume as a MangaUpdates-added series arrives: in print on the count's word, with no
    /// ISBN, no date and no name. An ISBN is the AniList- or import-added case.
    /// </summary>
    public static Volume Volume(int number, string? isbn = null, bool isReleased = true) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        Isbn = isbn,
        IsReleased = isReleased,
    };

    /// <summary>A store catalogue that knows nothing, so a file can stay about the by-ISBN path.</summary>
    // It still costs the one request the catalogue pass always spends, which is why budget
    // assertions in those files start from one rather than from zero.
    public static IVolumeCatalogueSource NoCatalogue() => Catalogue();

    /// <summary>A store that answers with the same records whatever title it is asked about.</summary>
    public static IVolumeCatalogueSource Catalogue(params CatalogueVolume[] records)
    {
        var source = Substitute.For<IVolumeCatalogueSource>();
        source.SourceName.Returns("AppleBooks");
        source.GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns(records);

        return source;
    }

    /// <summary>A store that answers differently per title — what arbitration is about.</summary>
    public static IVolumeCatalogueSource CatalogueByTitle(Dictionary<string, CatalogueVolume[]> answers)
    {
        var source = Substitute.For<IVolumeCatalogueSource>();
        source.SourceName.Returns("AppleBooks");
        source.GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns(call => answers.TryGetValue(call.ArgAt<string>(0), out var records) ? records : []);

        return source;
    }

    /// <summary>A search that offers nothing, so a file can stay about the direct lookup.</summary>
    public static IIsbnCandidateSource NoCandidates()
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(IsbnCandidates.None);

        return source;
    }

    public static IIsbnCandidateSource Candidates(params string[] isbns)
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IsbnCandidates { WorksFound = 1, Isbns = isbns });

        return source;
    }

    /// <summary>The by-ISBN source knows nothing, so a file can stay about the catalogue pass.</summary>
    public static IVolumeEnrichmentSource NoEnrichment() => EnrichmentSource(null);

    /// <summary>A by-ISBN source that answers with the same record whatever ISBN it is given.</summary>
    public static IVolumeEnrichmentSource EnrichmentSource(VolumeEnrichment? enrichment)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(enrichment);

        return source;
    }

    /// <summary>
    /// A by-ISBN source that answers per ISBN, and with nothing for any ISBN not named —
    /// which is how a held ISBN that the catalogue never indexed is written.
    /// </summary>
    public static IVolumeEnrichmentSource EnrichmentSourcePerIsbn(
        params (string Isbn, VolumeEnrichment? Edition)[] editions)
    {
        var source = EnrichmentSource(null);

        foreach (var (isbn, edition) in editions)
        {
            source.GetByIsbnAsync(isbn, Arg.Any<CancellationToken>()).Returns(edition);
        }

        return source;
    }

    /// <summary>A cover cache that answers under whichever source name it was called with.</summary>
    public static IVolumeCoverCache CoverCache()
    {
        var cache = Substitute.For<IVolumeCoverCache>();
        cache.EnsureCachedAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => $"volume/{callInfo.ArgAt<string>(1).ToLowerInvariant()}/abc.jpg");

        return cache;
    }
}
