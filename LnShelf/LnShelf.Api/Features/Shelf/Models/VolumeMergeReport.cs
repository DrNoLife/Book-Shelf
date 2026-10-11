namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>What a volume merge did, or with a dry run would have done.</summary>
/// <param name="SeriesTitle">The series the two volumes belong to.</param>
/// <param name="FromNumber">The volume merged away.</param>
/// <param name="IntoNumber">The volume that received the user state.</param>
/// <param name="MovedUserState">False when the merged-away volume had no state to move.</param>
/// <param name="MovedCoverOverride">True when an uploaded cover went along with the state.</param>
/// <param name="DryRun">True when nothing was written.</param>
public sealed record VolumeMergeReport(
    string SeriesTitle,
    decimal FromNumber,
    decimal IntoNumber,
    bool MovedUserState,
    bool MovedCoverOverride,
    bool DryRun)
{
    /// <summary>One line for the person who ran the command.</summary>
    public string Summary
    {
        get
        {
            var moved = (MovedUserState, MovedCoverOverride) switch
            {
                (false, _) => "no user state to move",
                (true, false) => "user state moved",
                (true, true) => "user state and uploaded cover moved",
            };

            var from = FromNumber.ToVolumeText();
            var into = IntoNumber.ToVolumeText();

            return DryRun
                ? $"Would merge {SeriesTitle} volume {from} into volume {into}: {moved}, volume {from} would be deleted. Nothing was written."
                : $"Merged {SeriesTitle} volume {from} into volume {into}: {moved}, volume {from} deleted.";
        }
    }
}
