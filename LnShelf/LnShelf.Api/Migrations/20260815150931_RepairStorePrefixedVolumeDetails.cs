using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    // No schema change: this migration exists to undo three days' worth of enrichment writes
    // that the code above it will no longer make. Enrichment is fill-if-null by design, so a
    // wrong value already in a column is not something a later run heals — it is the one
    // failure mode that rule accepts, and clearing the column is the sanctioned fix for it
    // (VolumeFactWriter). A migration is the right place because the repair has to happen
    // exactly once per database, in step with the code that changed.
    //
    // Nothing here touches user state. Title, Description and CoverObjectKey are synced
    // metadata; the covers a reader dropped themselves live on VolumeUserState under a
    // different key prefix and are not reachable from any statement below.
    public partial class RepairStorePrefixedVolumeDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Titles that are still the record's whole name. The shelf now stores a
            //    volume's subtitle alone, so anything holding a volume marker or a medium
            //    marker is the old shape. Deliberately not "clear every title": Open Library
            //    subtitles such as "Run Through the Battlefield" are already exactly what the
            //    column should hold, and re-fetching them would cost lookups for no change.
            migrationBuilder.Sql(
                """
                UPDATE volume
                SET "Title" = NULL
                WHERE "Title" ~* 'vol(ume)?\.?\s*\d'
                   OR "Title" ~* '\((light novel|manga)\)';
                """);

            // 2. Every blurb, without exception. All of them were written by the store
            //    catalogue pass on the day it shipped, with a sanitizer that dropped the
            //    paragraph break between a publisher's headline and the body — so the whole
            //    stored set carries the defect and there is nothing to keep. Refilling them
            //    costs one catalogue request per series, which the next sync makes anyway.
            migrationBuilder.Sql("""UPDATE volume SET "Description" = NULL;""");

            // 3. The placeholder art the store serves for volumes that are not out yet. The
            //    set is exactly the volumes whose own release-date discrepancy was recorded
            //    against them — that row is only written when a source dated a volume in the
            //    future — and the prefix keeps this off the by-ISBN source's covers, which
            //    are of published editions. One row today: 86 volume 14, due 2026-12-08.
            //
            //    The cached object itself is left in the store. It is a few kilobytes nothing
            //    now points at, and reaching into MinIO from a migration would be a far worse
            //    trade than an orphan.
            migrationBuilder.Sql(
                """
                UPDATE volume v
                SET "CoverObjectKey" = NULL
                WHERE v."CoverObjectKey" LIKE 'volume/applebooks/%'
                  AND EXISTS (
                      SELECT 1
                      FROM sync_discrepancy d
                      WHERE d."Field" = 'ReleaseDate'
                        AND d."SeriesId" = v."SeriesId"
                        AND d."VolumeNumber" = v."Number");
                """);
        }

        /// <inheritdoc />
        // Deliberately empty. Down would have to put back values that were wrong when they
        // were written, and they are not recorded anywhere to put back — the migration
        // deletes bad data rather than changing a shape. Rolling this one back is a no-op by
        // design; the columns refill from their sources on the next sync either way.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
