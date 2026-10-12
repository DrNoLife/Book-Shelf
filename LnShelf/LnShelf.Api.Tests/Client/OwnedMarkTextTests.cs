using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Select mode's "Mark N owned" with an edition beside it: what the button says, and when it
/// has nothing left to do.
/// </summary>
public sealed class OwnedMarkTextTests
{
    public static TheoryData<bool[], OwnedFormat?[], OwnedFormat, bool> Selections => new()
    {
        // Owned, edition as recorded, the edition chosen → already done?
        { [true, true], [OwnedFormat.Digital, OwnedFormat.Digital], OwnedFormat.Digital, true },
        { [true, true], [OwnedFormat.Physical, OwnedFormat.Digital], OwnedFormat.Digital, false },
        { [true, false], [OwnedFormat.Digital, null], OwnedFormat.Digital, false },

        // An owned volume with no recorded edition is physical, as the API and the panel say.
        { [true], [null], OwnedFormat.Physical, true },
        { [true], [null], OwnedFormat.Digital, false },

        // Nothing selected is not "all done".
        { [], [], OwnedFormat.Physical, false },
    };

    [Theory]
    [MemberData(nameof(Selections))]
    public void AreAllOwnedAs_ASelection_IsDoneOnlyWhenEveryVolumeHasThatEdition(
        bool[] owned, OwnedFormat?[] formats, OwnedFormat chosen, bool expected)
    {
        var volumes = owned.Select((isOwned, index) => Volume(index + 1, isOwned, formats[index])).ToList();

        OwnedMarkText.AreAllOwnedAs(volumes, chosen).ShouldBe(expected);
    }

    [Theory]
    [InlineData(3, false, OwnedFormat.Digital, "Mark 3 owned · Digital")]
    [InlineData(3, false, OwnedFormat.Physical, "Mark 3 owned · Physical")]
    [InlineData(3, true, OwnedFormat.Digital, "All 3 owned as Digital")]
    [InlineData(1, true, OwnedFormat.Physical, "Already owned as Physical")]
    public void Label_ASelection_SaysWhatThePressWrites(
        int count, bool allOwnedAlready, OwnedFormat format, string expected) =>
        OwnedMarkText.Label(count, allOwnedAlready, format).ShouldBe(expected);

    /// <summary>The edition is a choice for one bulk mark, not a preference that lingers.</summary>
    [Fact]
    public void OwnedFormat_SelectModeReopened_StartsAtPhysicalAgain()
    {
        var selection = new VolumeSelectionState();
        selection.ToggleSelectMode();
        selection.ChooseOwnedFormat(OwnedFormat.Digital);

        selection.ToggleSelectMode();
        selection.ToggleSelectMode();

        selection.OwnedFormat.ShouldBe(OwnedFormat.Physical);
    }

    [Fact]
    public void OwnedFormat_AnotherSeries_StartsAtPhysical()
    {
        var selection = new VolumeSelectionState();
        selection.ToggleSelectMode();
        selection.ChooseOwnedFormat(OwnedFormat.Digital);

        selection.Reset();

        selection.OwnedFormat.ShouldBe(OwnedFormat.Physical);
    }

    private static VolumeResponse Volume(int number, bool owned, OwnedFormat? format) => new()
    {
        VolumeId = Guid.CreateVersion7(),
        Number = number,
        IsOut = true,
        ReadStatus = ReadStatus.Unread,
        Owned = owned,
        OwnedFormat = format,
        IsLocalOnly = false,
    };
}
