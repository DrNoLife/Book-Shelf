using LnShelf.Shared.Preferences;

namespace LnShelf.Api.Tests.Contracts;

/// <summary>
/// The two normalisers the server and the client share: what a set of status filters means,
/// and an order that always holds every library section exactly once.
/// </summary>
public sealed class LibraryPreferenceNormalizationTests
{
    public static TheoryData<LibraryFilter[], LibraryFilter[]> FilterSets => new()
    {
        { [LibraryFilter.Behind], [LibraryFilter.Behind] },

        // Repeats dropped, the enum's order kept.
        { [LibraryFilter.Reading, LibraryFilter.Owned, LibraryFilter.Reading], [LibraryFilter.Owned, LibraryFilter.Reading] },

        // All is exclusive, and nothing chosen filters nothing.
        { [LibraryFilter.Behind, LibraryFilter.All], [LibraryFilter.All] },
        { [], [LibraryFilter.All] },

        // A value no member names is not a filter.
        { [(LibraryFilter)999, LibraryFilter.Dropped], [LibraryFilter.Dropped] },
    };

    [Theory]
    [MemberData(nameof(FilterSets))]
    public void LibraryFilterSetNormalize_AnySelection_IsTheCanonicalSet(LibraryFilter[] filters, LibraryFilter[] expected) =>
        LibraryFilterSet.Normalize(filters).ShouldBe(expected);

    [Fact]
    public void LibraryFilterSetNormalize_Null_IsEverything() =>
        LibraryFilterSet.Normalize(null).ShouldBe([LibraryFilter.All]);

    public static TheoryData<LibrarySection[], LibrarySection[]> Orders => new()
    {
        { [LibrarySection.Shelf, LibrarySection.ContinueReading, LibrarySection.OutNow], [LibrarySection.Shelf, LibrarySection.ContinueReading, LibrarySection.OutNow] },

        // Missing sections are appended in the default order.
        { [LibrarySection.Shelf], [LibrarySection.Shelf, LibrarySection.ContinueReading, LibrarySection.OutNow] },
        { [], [LibrarySection.ContinueReading, LibrarySection.OutNow, LibrarySection.Shelf] },

        // Repeats and unknown values dropped.
        { [LibrarySection.OutNow, (LibrarySection)42, LibrarySection.OutNow], [LibrarySection.OutNow, LibrarySection.ContinueReading, LibrarySection.Shelf] },
    };

    [Theory]
    [MemberData(nameof(Orders))]
    public void LibrarySectionsNormalize_AnyOrder_HoldsEverySectionOnce(LibrarySection[] order, LibrarySection[] expected) =>
        LibrarySections.Normalize(order).ShouldBe(expected);

    [Fact]
    public void LibrarySectionsNormalize_Null_IsTheDefault() =>
        LibrarySections.Normalize(null).ShouldBe(LibrarySections.Default);
}
