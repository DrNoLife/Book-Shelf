using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>The work header's count line, long on a wide screen and short on a phone.</summary>
public sealed class SearchWorkTextTests
{
    [Fact]
    public void Summary_TwoFormats_CountsEachInFormatOrder()
    {
        var work = Work(Format.Manga, Format.LightNovel, Format.LightNovel);

        SearchWorkText.Summary(work).ShouldBe("2 light novel series · 1 manga series");
        SearchWorkText.ShortSummary(work).ShouldBe("2 LN · 1 manga series");
    }

    [Fact]
    public void Summary_OneFormat_NamesOnlyThatOne()
    {
        var work = Work(Format.WebNovel);

        SearchWorkText.Summary(work).ShouldBe("1 web novel series");
        // One format has room for the long form on a phone too, as the mobile design shows it.
        SearchWorkText.ShortSummary(work).ShouldBe("1 web novel series");
    }

    private static SearchWork Work(params Format[] formats) => new()
    {
        Title = "Overlord",
        MainStory =
        [
            .. formats.Select((format, index) => new SearchWorkEntry
            {
                Name = "Overlord",
                Result = new SeriesSearchResultResponse
                {
                    Source = MetadataSourceName.AniList,
                    AniListId = index + 1,
                    TitleEnglish = "Overlord",
                    Format = format,
                    PublicationStatus = PublicationStatus.Ongoing,
                },
            }),
        ],
        SideStories = [],
    };
}
