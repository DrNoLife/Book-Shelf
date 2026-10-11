using LnShelf.Api.Features.Preferences.Data;
using LnShelf.Shared.Preferences;

namespace LnShelf.Api.Tests.Features.Preferences.Data;

/// <summary>
/// The preferences row keeps two ordered sets as comma-separated names. A reader that threw
/// on one unknown name would make the whole row unreadable after a rename.
/// </summary>
public sealed class EnumListConverterTests
{
    private static readonly EnumListConverter<LibraryFilter> Converter = new();

    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void Convert_ToTheColumnAndBack_KeepsTheValuesInOrder(LibraryFilter[] values, string text)
    {
        Converter.ConvertToProvider(values).ShouldBe(text);
        ((IReadOnlyList<LibraryFilter>)Converter.ConvertFromProvider(text)!).ShouldBe(values);
    }

    public static TheoryData<LibraryFilter[], string> RoundTrips => new()
    {
        { [LibraryFilter.Behind], "Behind" },
        { [LibraryFilter.Reading, LibraryFilter.Owned], "Reading,Owned" },
        { [], "" },
    };

    [Theory]
    [InlineData("Behind,Retired,Reading", new[] { LibraryFilter.Behind, LibraryFilter.Reading })]
    [InlineData(" Behind , ,Reading ", new[] { LibraryFilter.Behind, LibraryFilter.Reading })]
    [InlineData("3,Owned", new[] { LibraryFilter.Owned })]
    [InlineData("behind", new LibraryFilter[0])]
    public void FromText_NamesItDoesNotKnow_AreSkipped(string text, LibraryFilter[] expected) =>
        EnumListConverter<LibraryFilter>.FromText(text).ShouldBe(expected);

    [Fact]
    public void Comparer_AReorderedList_IsAChange()
    {
        var comparer = new EnumListComparer<LibrarySection>();
        IReadOnlyList<LibrarySection> original = [LibrarySection.ContinueReading, LibrarySection.Shelf];

        comparer.Equals(original, [LibrarySection.ContinueReading, LibrarySection.Shelf]).ShouldBeTrue();
        comparer.Equals(original, [LibrarySection.Shelf, LibrarySection.ContinueReading]).ShouldBeFalse();
        comparer.Snapshot(original).ShouldNotBeSameAs(original);
    }
}
