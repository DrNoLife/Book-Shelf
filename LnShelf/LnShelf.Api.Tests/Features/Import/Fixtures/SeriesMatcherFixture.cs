namespace LnShelf.Api.Tests.Features.Import.Fixtures;

/// <summary>
/// A matcher over a fixed set of search hits, with no network anywhere near it.
/// </summary>
// Shared by the two matcher test classes — the title decisions and the credit
// tie-break — because the same three builders in two files is two chances for the
// candidate shape to drift apart.
internal static class SeriesMatcherFixture
{
    private static readonly ImportOptions DefaultOptions = new();

    public static AniListSeriesMatcher CreateMatcher(params SeriesSearchResult[] results) =>
        CreateMatcher(DefaultOptions, results);

    public static AniListSeriesMatcher CreateMatcher(ImportOptions options, params SeriesSearchResult[] results)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.SearchAsync(Arg.Any<string>(), Arg.Any<Format?>(), Arg.Any<CancellationToken>())
            .Returns(results);

        return new AniListSeriesMatcher(
            metadataSource,
            Options.Create(options),
            NullLogger<AniListSeriesMatcher>.Instance);
    }

    public static SeriesSearchResult CreateResult(
        int aniListId,
        string titleEnglish,
        Format format,
        int? volumeCount,
        string? author = null,
        string? illustrator = null) => new()
        {
            Source = MetadataSourceName.AniList,
            SourceId = aniListId,
            TitleEnglish = titleEnglish,
            Format = format,
            PublicationStatus = PublicationStatus.Ongoing,
            VolumeCount = volumeCount,
            Author = author,
            Illustrator = illustrator,
        };
}
