namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// What one cover-migrate run found and did.
/// </summary>
/// <param name="Scanned">Covers in the old store.</param>
/// <param name="Copied">Copied and read back identical. On a dry run, the ones that would be.</param>
/// <param name="Skipped">Already in the new store with the same bytes.</param>
/// <param name="Failed">Could not be copied or did not read back identical; a re-run retries them.</param>
public sealed record CoverMigrationReport(int Scanned, int Copied, int Skipped, int Failed)
{
    public string Summary =>
        $"{Scanned} covers scanned, {Copied} copied, {Skipped} already there, {Failed} failed.";
}
