using LnShelf.Client.Components;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// A cover that cannot load gives way to the next candidate, then to the placeholder: never
/// the browser's broken-image icon (design.md — Cover art). The stepping rules behind
/// CoverImage, asserted without a browser; the end-to-end journeys assert the browser side.
/// </summary>
public sealed class CoverCandidatesTests
{
    [Fact]
    public void Current_NothingOffered_IsNull()
    {
        new CoverCandidates().Current.ShouldBeNull();
    }

    public static TheoryData<string?[], string[]> OfferCases => new()
    {
        { ["a"], ["a"] },
        { ["a", "b"], ["a", "b"] },

        // A missing key needs no guard at the call site.
        { [null, "b"], ["b"] },
        { ["", "  ", null], [] },

        // The same URL twice is one candidate: a failed load is not retried.
        { ["a", "a", "b"], ["a", "b"] },
    };

    [Theory]
    [MemberData(nameof(OfferCases))]
    public void Fail_EachCandidateInTurn_StepsThroughThemInOrderThenRunsOut(string?[] offered, string[] expectedOrder)
    {
        var candidates = new CoverCandidates();
        candidates.Offer(offered);

        foreach (var expected in expectedOrder)
        {
            candidates.Current.ShouldBe(expected);
            candidates.Fail(expected).ShouldBeTrue();
        }

        candidates.Current.ShouldBeNull();
    }

    [Fact]
    public void Offer_AnEqualListAfterAFailure_KeepsThePosition()
    {
        // A parent re-renders with a fresh list instance each time; a cover that already
        // failed must not come back, or the image would flicker between broken and fallback.
        var candidates = new CoverCandidates();
        candidates.Offer(["override", "synced"]);
        candidates.Fail("override");

        candidates.Offer(["override", "synced"]);

        candidates.Current.ShouldBe("synced");
    }

    [Fact]
    public void Offer_ADifferentList_StartsAgainFromItsFirst()
    {
        // A new cover uploaded, or another volume's sheet opened: the new list gets its own try.
        var candidates = new CoverCandidates();
        candidates.Offer(["override", "synced"]);
        candidates.Fail("override");
        candidates.Fail("synced");

        candidates.Offer(["override?v=2", "synced"]);

        candidates.Current.ShouldBe("override?v=2");
    }

    [Fact]
    public void Fail_ASourceNoLongerShowing_ChangesNothing()
    {
        // A late error event from the source the image already left must not skip the one
        // now loading.
        var candidates = new CoverCandidates();
        candidates.Offer(["override", "synced"]);
        candidates.Fail("override");

        candidates.Fail("override").ShouldBeFalse();

        candidates.Current.ShouldBe("synced");
    }

    [Fact]
    public void Fail_AfterEveryCandidateFailed_StaysOnThePlaceholder()
    {
        var candidates = new CoverCandidates();
        candidates.Offer(["only"]);
        candidates.Fail("only");

        candidates.Fail("only").ShouldBeFalse();

        candidates.Current.ShouldBeNull();
    }
}
