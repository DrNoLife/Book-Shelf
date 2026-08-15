using LnShelf.Api.Tests.Client;

namespace LnShelf.Api.Tests.Features.Shelf.Mappers;

/// <summary>
/// The volume title's route from the shelf to the caption, and the property the whole
/// feature turns on: a volume with no title renders nothing at all.
/// </summary>
// Asserted against the projection and the markup rather than through a rendered
// component, which is this project's existing convention for client behaviour with no
// browser in the test run (see LibraryGridDensityStylesheetTests, ClientSource).
public sealed class VolumeTitleCaptionTests
{
    private static readonly DateOnly Today = new(2026, 7, 31);

    [Fact]
    public void ToVolume_AMixOfTitledAndUntitledVolumes_CarriesEachOnesOwnAnswer()
    {
        // The shape the shelf actually has after one enrichment run: some volumes resolved
        // to a record with a subtitle, some were named and have none of their own, some
        // nothing has reached yet. All three in one series.
        var series = Series(
            Volume(1, title: "The Undead King"),
            Volume(2, title: null),
            Volume(3, title: "The Bloody Valkyrie"),
            Volume(4, title: string.Empty));

        var volumes = SeriesDetailProjection.ToDetail(series, Today).Volumes;

        volumes.Select(volume => volume.Title).ShouldBe(
        [
            "The Undead King",
            null,
            "The Bloody Valkyrie",
            null,
        ]);
    }

    [Fact]
    public void ToVolume_AnUnenrichedVolume_HasNoTitleRatherThanAnEmptyOne()
    {
        // Null, not "" and not "Volume 2": the caption prints what it is given, and an
        // invented title is the one thing this feature must never produce.
        SeriesDetailProjection.ToVolume(Volume(2, title: null), Today).Title.ShouldBeNull();
    }

    [Fact]
    public void ToVolume_AVolumeNamedWithNoSubtitle_ReachesTheClientAsNoTitleAtAll()
    {
        // The three-state column collapses to two on the wire. "" is a fact enrichment
        // needs — "asked, and there is no subtitle" — and a caption has nothing to do with
        // it, so the client sees the same null it sees for an unreached volume.
        SeriesDetailProjection.ToVolume(Volume(2, title: string.Empty), Today).Title.ShouldBeNull();
    }

    [Fact]
    public void VolumeTileMarkup_TheCaption_PrintsNothingExtraWhenThereIsNoTitle()
    {
        var markup = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "VolumeTile.razor"));

        // The caption is built from CaptionParts, which yields the title only when there
        // is one — there is no placeholder branch and no separator printed on its own.
        markup.ShouldContain("foreach (var part in CaptionParts)");
        markup.ShouldNotContain("Untitled");
    }

    [Fact]
    public void VolumeTileCaption_ItsHeightIsReserved_SoAMixedRowDoesNotJump()
    {
        var stylesheet = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "VolumeTile.razor.css"));

        // design.md's own rule for the caption line: its height is reserved, so a row
        // mixing captioned and plain tiles keeps its tops and its bottom strips aligned.
        // Turning titles on must therefore move nothing.
        stylesheet.ShouldContain("min-height");
    }

    [Fact]
    public void VolumeListMarkup_ATitle_IsPrintedOnlyWhenThereIsOneAndTitlesAreOn()
    {
        var markup = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "VolumeList.razor"));

        // Both conditions, and nothing in the else: no dash, no em dash, no placeholder.
        markup.ShouldContain("ShowTitles && !string.IsNullOrWhiteSpace(volume.Title)");
        markup.ShouldContain("row__title");
    }

    [Fact]
    public void VolumeListStylesheet_TheTitledVariant_WidensTheHeaderAndTheRowsTogether()
    {
        var stylesheet = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "VolumeList.razor.css"));

        // The header and the rows share one grid definition; changing one alone would put
        // a label over the wrong column. Phase 3.4 found that trap once already.
        stylesheet.ShouldContain(".list--titled .list__head");
        stylesheet.ShouldContain(".list--titled .row");

        // Ellipsised, never wrapped: a row is one line.
        stylesheet.ShouldContain("text-overflow: ellipsis");
    }

    [Fact]
    public void VolumeGridMarkup_TheToggle_IsOfferedOnlyWhereAVolumeHasATitle()
    {
        var markup = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "VolumeGrid.razor"));

        // A control that does nothing on most of the shelf is worse than no control
        // (the phase 3.3.1 control audit).
        markup.ShouldContain("@if (HasAnyTitle)");

        // And the tile only ever receives a title when the preference is on, so the
        // toggle cannot be out of step with what is drawn.
        markup.ShouldContain("Preferences.ShowVolumeTitles ? volume.Title : null");
    }

    private static Series Series(params Volume[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(volume);
        }

        return series;
    }

    private static Volume Volume(int number, string? title) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        Title = title,
        IsReleased = true,
    };
}
