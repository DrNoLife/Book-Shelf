using LnShelf.Api.Features.Shelf.Models;
using ShelfSeries = LnShelf.Api.Features.Shelf.Models.Series;

namespace LnShelf.EndToEnd.Tests.Journeys.Recommendations;

// For you and "Readers also liked", from the shelf of one light novel the reader has read.
// The fake source answers from the captured AniList payload, so the top title is read from
// that payload instead of being typed here. Add is not driven: the fake metadata source knows
// only the novel itself, so adding a recommended work could not complete.
public sealed class ForYouTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "Mushoku Tensei: Jobless Reincarnation";

    private static readonly string TopTitle =
        FixtureRecommendationSource.For(FixtureSeriesMetadataSource.MushokuTenseiNovelId).Works[0].Title;

    [Fact]
    public async Task ForYou_AReadLightNovel_ShowsWhatItsReadersLiked_AndNotInterestedHidesItUntilShownAgain()
    {
        await SeedReadNovelAsync();

        await Page.GotoAsync("/for-you");

        var row = Page.GetByRole(AriaRole.Region, new() { Name = $"Because you read {SeriesTitle}" });
        await Assertions.Expect(row.GetByText(TopTitle, new() { Exact = true })).ToBeVisibleAsync();

        await row.GetByRole(AriaRole.Button, new() { Name = $"Not interested in {TopTitle}", Exact = true }).ClickAsync();

        await Assertions.Expect(row.GetByText(TopTitle, new() { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByRole(AriaRole.Status).GetByText($"{TopTitle} won't be suggested again."))
            .ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Assertions.Expect(row).ToBeVisibleAsync();
        await Assertions.Expect(row.GetByText(TopTitle, new() { Exact = true })).ToHaveCountAsync(0);

        await Page.GetByText("Not interested (1)", new() { Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = $"Show {TopTitle} again", Exact = true }).ClickAsync();

        await Assertions.Expect(row.GetByText(TopTitle, new() { Exact = true })).ToBeVisibleAsync();

        await BrokenImages.ShouldHaveNoneAsync(Page);
    }

    [Fact]
    public async Task SeriesPage_ALinkedSeries_ShowsReadersAlsoLiked()
    {
        await SeedReadNovelAsync();

        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");

        var strip = Page.GetByRole(AriaRole.Region, new() { Name = $"Readers of {SeriesTitle} also liked" });
        await Assertions.Expect(strip.GetByText(TopTitle, new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText(SeriesTitle, new() { Exact = true })).ToHaveCountAsync(0);

        await BrokenImages.ShouldHaveNoneAsync(Page);
    }

    // A novel the reader has read from: the seed For you needs, reading with something read.
    private Task SeedReadNovelAsync() =>
        Fixture.SeedAsync(async dbContext =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            dbContext.Series.Add(new ShelfSeries
            {
                Id = Guid.CreateVersion7(),
                AniListId = FixtureSeriesMetadataSource.MushokuTenseiNovelId,
                TitleEnglish = SeriesTitle,
                Format = Format.LightNovel,
                UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading, LastReadAt = today },
                Volumes =
                [
                    new Volume
                    {
                        Id = Guid.CreateVersion7(),
                        Number = 1,
                        IsReleased = true,
                        UserState = new VolumeUserState { ReadStatus = ReadStatus.Read, ReadAt = today },
                    },
                ],
            });

            await dbContext.SaveChangesAsync();
        });
}
