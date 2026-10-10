using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// "Add all 5 parts (33 vols)": offered under one format, for two or more main-story entries
/// not yet on the shelf, and counting only those.
/// </summary>
public sealed class AddAllOfferTests
{
    [Fact]
    public void For_OneFormatAndNothingOnTheShelf_OffersEveryPartWithTheVolumeTotal()
    {
        var work = Work(Part(1, volumes: 3), Part(2, volumes: 4), Part(3, volumes: 5));

        var offer = AddAllOffer.For(work, Format.LightNovel, _ => false).ShouldNotBeNull();

        offer.Label.ShouldBe("Add all 3 parts (12 vols)");
        offer.ShortLabel.ShouldBe("Add all 3");
        offer.Entries.Select(entry => entry.PartNumber).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void For_AllFormats_OffersNothing() =>
        AddAllOffer.For(Work(Part(1), Part(2)), formatFilter: null, _ => false).ShouldBeNull();

    [Fact]
    public void For_OnlyOneLeftToAdd_OffersNothing()
    {
        var work = Work(Part(1), Part(2));
        var onShelf = work.MainStory[0].Result;

        AddAllOffer.For(work, Format.LightNovel, result => result == onShelf).ShouldBeNull();
    }

    [Fact]
    public void For_SomeAlreadyOnTheShelf_OffersTheOthersOnly()
    {
        var work = Work(Part(1, volumes: 3), Part(2, volumes: 4), Part(3, volumes: 5));
        var onShelf = work.MainStory[0].Result;

        var offer = AddAllOffer.For(work, Format.LightNovel, result => result == onShelf).ShouldNotBeNull();

        offer.Label.ShouldBe("Add the other 2 parts (9 vols)");
        offer.Entries.Select(entry => entry.PartNumber).ShouldBe([2, 3]);
    }

    [Theory]
    [InlineData(3, null, "Add all 2 parts (3+ vols)")]
    [InlineData(null, null, "Add all 2 parts")]
    public void For_UnknownVolumeCounts_SaysSo(int? first, int? second, string expected) =>
        AddAllOffer.For(Work(Part(1, first), Part(2, second)), Format.LightNovel, _ => false)
            .ShouldNotBeNull().Label.ShouldBe(expected);

    private static SearchWork Work(params SearchWorkEntry[] mainStory) => new()
    {
        Title = "Ascendance of a Bookworm",
        MainStory = mainStory,
        SideStories = [],
    };

    private static SearchWorkEntry Part(int number, int? volumes = null) => new()
    {
        Name = $"Part {number}",
        PartNumber = number,
        Result = new SeriesSearchResultResponse
        {
            Source = MetadataSourceName.AniList,
            AniListId = number,
            TitleEnglish = $"Ascendance of a Bookworm: Part {number}",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Completed,
            VolumeCount = volumes,
        },
    };
}
