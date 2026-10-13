namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The one user-state write sync makes: the new-volume badge, raised and never cleared.
/// </summary>
public sealed class NewVolumeFlagTests
{
    [Fact]
    public void Raise_ASeriesSomebodyTracks_SetsTheBadge()
    {
        var series = Series();
        series.UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading };

        NewVolumeFlag.Raise(series);

        series.UserState.HasNewVolume.ShouldBeTrue();
    }

    [Fact]
    public void Raise_NoUserState_IsANoOpAndCreatesNone()
    {
        // Creating the row is the Shelf slice's job, not sync's.
        var series = Series();

        NewVolumeFlag.Raise(series);

        series.UserState.ShouldBeNull();
    }

    private static Series Series() => new()
    {
        Id = Guid.CreateVersion7(),
        TitleEnglish = "Overlord",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
    };
}
