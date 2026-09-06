using System.Reflection;
using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// What the series page writes, and what it does with the answer. Asserted against the
/// types and the source, which is this project's convention for client behaviour with no
/// browser in the test run (see ClientSource, StateFlagsAreReadTests).
/// </summary>
public sealed class SeriesWriteSurfaceTests
{
    private static readonly string StatePath =
        Path.Combine("Features", "Series", "SeriesState.cs");

    /// <summary>
    /// Every single-volume write answers with the series, not the row it touched.
    /// </summary>
    // The four endpoints changed shape together, so they are asserted together: one of
    // them left behind would put the old VolumeResponse back into the state container and
    // the counts would stop agreeing with the tiles again.
    [Theory]
    [InlineData(nameof(SeriesDetailClient.SetReadAsync))]
    [InlineData(nameof(SeriesDetailClient.SetOwnedAsync))]
    [InlineData(nameof(SeriesDetailClient.SetCoverAsync))]
    [InlineData(nameof(SeriesDetailClient.RemoveCoverAsync))]
    public void SeriesDetailClient_EverySingleVolumeWrite_AnswersWithTheWholeSeries(string methodName) =>
        typeof(SeriesDetailClient)
            .GetMethod(methodName)
            .ShouldNotBeNull()
            .ReturnType
            .ShouldBe(typeof(Task<SeriesDetailResponse>));

    /// <summary>
    /// The client no longer recomputes the server's counts.
    /// </summary>
    // Replace() held a second copy of SeriesDetailProjection.ToCounts — read, owned,
    // published, to buy, backlog, next-to-buy — in a second project, kept in step by hand.
    // CLAUDE.md puts derived state in queries; this is the assertion that it stays there.
    [Fact]
    public void SeriesState_TheClientSideCountRederivation_IsGone()
    {
        typeof(SeriesState)
            .GetMethod("Replace", BindingFlags.NonPublic | BindingFlags.Static)
            .ShouldBeNull();

        var source = ClientSource.ReadAllText(StatePath);

        foreach (var derived in new[] { "ToBuy =", "Backlog =", "NextToBuyNumber =", "Published =" })
        {
            source.ShouldNotContain(derived);
        }
    }

    /// <summary>
    /// The new-volume badge is cleared by visiting the series, and only then.
    /// </summary>
    [Fact]
    public void SeriesState_TheSeenCall_IsMadeOnlyForASeriesCarryingTheBadge()
    {
        var source = ClientSource.ReadAllText(StatePath);

        // The condition, and the await. A fire-and-forget here would race the navigation
        // away from the page and lose the acknowledgement.
        source.ShouldContain("if (Series is { HasNewVolume: true })");
        source.ShouldContain("await MarkSeenAsync(cancellationToken)");

        // And the flag is put down locally, so a second render cannot ask again.
        source.ShouldContain("Series = series with { HasNewVolume = false }");
    }

    /// <summary>A 404 means the series is gone, which is not a failure worth printing.</summary>
    [Fact]
    public void SeriesDetailClient_TheSeenCall_IgnoresAMissingSeries()
    {
        ClientSource
            .ReadAllText(Path.Combine("Features", "Series", "SeriesDetailClient.cs"))
            .ShouldContain("response.StatusCode is not HttpStatusCode.NotFound");

        typeof(SeriesDetailClient).GetMethod(nameof(SeriesDetailClient.MarkSeenAsync)).ShouldNotBeNull();
    }

    /// <summary>
    /// The two halves of PUT /state, which is a partial update: each control sends only
    /// what it changed.
    /// </summary>
    [Fact]
    public void SeriesState_TheStateWrites_EachSendOnlyTheirOwnField()
    {
        var source = ClientSource.ReadAllText(StatePath);

        // The status control leaves the note alone...
        source.ShouldContain("SetStateAsync(trackingStatus, notes: null, cancellationToken)");

        // ...and the note never carries null, because null there means "unchanged" and
        // the empty string is how a note is cleared.
        source.ShouldContain("SetStateAsync(trackingStatus: null, notes ?? string.Empty, cancellationToken)");
    }

    [Fact]
    public void TrackingStatusControl_OffersTheFourTrackingStatuses_AndSavesTheChosenOne()
    {
        var codeBehind = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "TrackingStatusControl.razor.cs"));

        foreach (var status in Enum.GetNames<TrackingStatus>())
        {
            codeBehind.ShouldContain($"nameof(TrackingStatus.{status})");
        }

        // Choosing what is already chosen writes nothing.
        codeBehind.ShouldContain("status != Series.TrackingStatus");
        codeBehind.ShouldContain("SeriesState.SetTrackingStatusAsync(status)");

        // Inert while a write is in flight, like every other control on the page.
        ClientSource
            .ReadAllText(Path.Combine("Features", "Series", "Components", "TrackingStatusControl.razor"))
            .ShouldContain("Disabled=\"SeriesState.IsSaving\"");
    }

    [Fact]
    public void SeriesNotes_SavesOnBlurAndControlEnter_AndCancelsOnEscape()
    {
        var markup = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "SeriesNotes.razor"));

        markup.ShouldContain("@onblur=\"OnBlurAsync\"");
        markup.ShouldContain("@onkeydown=\"OnKeyDownAsync\"");

        var codeBehind = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", "SeriesNotes.razor.cs"));

        codeBehind.ShouldContain("args.Key is \"Escape\"");
        codeBehind.ShouldContain("Key: \"Enter\", CtrlKey: true");

        // Escape leaves edit mode before the browser delivers the blur of the box it
        // removed; without the guard, cancelling would save.
        codeBehind.ShouldContain("IsEditing ? SaveAsync() : Task.CompletedTask");

        // An emptied box is an erase, not a no-op — SetNotesAsync sends "" for it.
        codeBehind.ShouldContain("notes == (Series.Notes ?? string.Empty)");
        codeBehind.ShouldContain("SeriesState.SetNotesAsync(notes)");
    }
}
