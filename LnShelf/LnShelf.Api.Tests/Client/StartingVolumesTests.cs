using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The preview's "Start with" steppers: owned never below read, nothing past the volume count,
/// and the shared limit when the source has no count.
/// </summary>
public sealed class StartingVolumesTests
{
    [Fact]
    public void For_AnySeries_StartsAtZeroAndZero()
    {
        var start = StartingVolumes.For(Result(volumes: 5));

        start.Read.ShouldBe(0);
        start.Owned.ShouldBe(0);
        start.Maximum.ShouldBe(5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void For_NoVolumeCount_UsesTheSharedLimit(int? volumes) =>
        StartingVolumes.For(Result(volumes)).Maximum.ShouldBe(AddSeriesRequest.InitialCountLimitWithoutVolumeCount);

    [Fact]
    public void WithMoreRead_RaisesOwnedWithIt()
    {
        var start = StartingVolumes.For(Result(volumes: 5)).WithMoreRead().WithMoreRead();

        start.Read.ShouldBe(2);
        start.Owned.ShouldBe(2);
    }

    [Fact]
    public void WithMoreRead_OwnedAlreadyAhead_LeavesOwnedAlone()
    {
        var start = StartingVolumes.For(Result(volumes: 5)).WithMoreOwned().WithMoreOwned().WithMoreOwned().WithMoreRead();

        start.Read.ShouldBe(1);
        start.Owned.ShouldBe(3);
    }

    [Fact]
    public void WithFewerOwned_StopsAtRead()
    {
        var start = StartingVolumes.For(Result(volumes: 5)).WithMoreRead().WithMoreRead().WithFewerOwned();

        start.Owned.ShouldBe(2);
        start.CanOwnFewer.ShouldBeFalse();
    }

    [Fact]
    public void WithFewerRead_LeavesOwnedWhereItWas()
    {
        var start = StartingVolumes.For(Result(volumes: 5)).WithMoreRead().WithMoreRead().WithFewerRead();

        start.Read.ShouldBe(1);
        start.Owned.ShouldBe(2);
    }

    [Fact]
    public void Steppers_StopAtTheEnds()
    {
        var start = StartingVolumes.For(Result(volumes: 2));

        start.WithFewerRead().ShouldBeSameAs(start);
        start.WithFewerOwned().ShouldBeSameAs(start);

        var full = start.WithMoreRead().WithMoreRead().WithMoreRead();
        full.Read.ShouldBe(2);
        full.Owned.ShouldBe(2);
        full.CanReadMore.ShouldBeFalse();
        full.CanOwnMore.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 0, "New to it? Leave both at 0 — it goes on Plan to start.")]
    [InlineData(0, 1, "Volume 1 is filled in for you. Fine-tune later.")]
    [InlineData(2, 3, "Volumes 1–3 are filled in for you. Fine-tune later.")]
    public void Hint_SaysWhatAddIsAboutToDo(int read, int owned, string expected)
    {
        var start = StartingVolumes.For(Result(volumes: 10));
        for (var step = 0; step < owned; step++) start = start.WithMoreOwned();
        for (var step = 0; step < read; step++) start = start.WithMoreRead();

        start.Hint.ShouldBe(expected);
    }

    private static SeriesSearchResultResponse Result(int? volumes) => new()
    {
        Source = MetadataSourceName.AniList,
        AniListId = 1,
        TitleEnglish = "Overlord",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        VolumeCount = volumes,
    };
}
