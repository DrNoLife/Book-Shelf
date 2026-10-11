namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// What one cover-orphan-sweep found and did.
/// </summary>
/// <param name="Scanned">Uploaded covers and avatars in the store.</param>
/// <param name="Orphaned">Uploads whose volume no longer exists on any shelf, and avatars no account points at.</param>
/// <param name="Deleted">Orphans removed. Zero on a dry run.</param>
/// <param name="Failed">Orphans the store refused to delete; the next run tries again.</param>
public sealed record CoverOrphanSweepReport(int Scanned, int Orphaned, int Deleted, int Failed)
{
    public string Summary =>
        $"{Scanned} uploaded covers and avatars scanned, {Orphaned} orphaned, {Deleted} deleted, {Failed} failed.";
}
