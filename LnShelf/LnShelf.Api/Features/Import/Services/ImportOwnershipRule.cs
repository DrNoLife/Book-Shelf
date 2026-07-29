namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// The one place feature-spec §4.4's read-implies-owned decision is applied.
/// </summary>
// It is a rule with an exception, so it lives in one function rather than being
// spread across the parser, the queue mapper and the commit step — three copies
// of a rule the user can toggle is three chances for the toggle to half-work.
public static class ImportOwnershipRule
{
    /// <param name="ownedInExport">What the export's own ownership column said.</param>
    /// <param name="treatReadAsOwned">
    /// The group's setting. True by default: you do not read physical volumes you
    /// do not own. A borrowed or library-read series is the case for turning it off.
    /// </param>
    public static bool IsOwned(bool ownedInExport, ReadStatus readStatus, bool treatReadAsOwned) =>
        ownedInExport
        || (treatReadAsOwned && readStatus is ReadStatus.Read or ReadStatus.Reading);
}
