namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Read-implies-owned (feature-spec §4.4), on its own rather than through whatever
/// happened to call it.
/// </summary>
// It earns a direct test because every row of a real export says Owned? No: with the
// rule off by accident, a 76-book import would put nothing at all on the shelf as owned.
public sealed class ImportOwnershipRuleTests
{
    /// <summary>The whole matrix: what the export said × read status × the setting.</summary>
    public static TheoryData<bool, ReadStatus, bool, bool> OwnershipMatrix => new()
    {
        // The export's own column is never argued with, whatever else is true.
        { true, ReadStatus.Unread, false, true },
        { true, ReadStatus.Read, false, true },
        { true, ReadStatus.Unread, true, true },

        // Read or reading, with the rule on: you do not read volumes you do not have.
        { false, ReadStatus.Read, true, true },
        { false, ReadStatus.Reading, true, true },
        { false, ReadStatus.Unread, true, false },

        // The borrowed or library-read series: the rule off, and only the column counts.
        { false, ReadStatus.Read, false, false },
        { false, ReadStatus.Reading, false, false },
        { false, ReadStatus.Unread, false, false },
    };

    [Theory]
    [MemberData(nameof(OwnershipMatrix))]
    public void IsOwned_TheFullMatrix_MatchesTheSpec(
        bool ownedInExport, ReadStatus readStatus, bool treatReadAsOwned, bool expected) =>
        ImportOwnershipRule.IsOwned(ownedInExport, readStatus, treatReadAsOwned).ShouldBe(expected);
}
