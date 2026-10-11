using LnShelf.Client.Features.Profile;
using LnShelf.Shared.Preferences;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The profile's section-order list: move up and move down swap a section with its
/// neighbour, and a move past either end changes nothing.
/// </summary>
public sealed class LibrarySectionMovesTests
{
    private static readonly LibrarySection[] Default =
        [LibrarySection.ContinueReading, LibrarySection.OutNow, LibrarySection.Shelf];

    public static TheoryData<LibrarySection, int, LibrarySection[]> Moves => new()
    {
        { LibrarySection.Shelf, -1, [LibrarySection.ContinueReading, LibrarySection.Shelf, LibrarySection.OutNow] },
        { LibrarySection.ContinueReading, 1, [LibrarySection.OutNow, LibrarySection.ContinueReading, LibrarySection.Shelf] },

        // Past either end: unchanged.
        { LibrarySection.ContinueReading, -1, [LibrarySection.ContinueReading, LibrarySection.OutNow, LibrarySection.Shelf] },
        { LibrarySection.Shelf, 1, [LibrarySection.ContinueReading, LibrarySection.OutNow, LibrarySection.Shelf] },
    };

    [Theory]
    [MemberData(nameof(Moves))]
    public void Move_OnePlace_SwapsWithItsNeighbour(LibrarySection section, int offset, LibrarySection[] expected) =>
        LibrarySectionMoves.Move(Default, section, offset).ShouldBe(expected);

    [Fact]
    public void Move_ShelfUpTwice_PutsTheShelfFirst()
    {
        var once = LibrarySectionMoves.Move(Default, LibrarySection.Shelf, -1);

        LibrarySectionMoves.Move(once, LibrarySection.Shelf, -1)
            .ShouldBe([LibrarySection.Shelf, LibrarySection.ContinueReading, LibrarySection.OutNow]);
    }
}
