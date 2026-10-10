namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>Who settled a recorded disagreement, as the readers should see it now.</summary>
public readonly record struct DiscrepancyResolution(MetadataSourceName? Source, bool ByChoice);

/// <summary>
/// Reads "settled" off a recorded disagreement and the choices that stand today.
/// </summary>
// Derived at read time rather than stored, in line with CLAUDE.md's computed-not-stored rule:
// the count of items to review drops the moment a choice is made and comes back the moment it
// is reset, with no stored row rewritten.
public static class DiscrepancyResolutionReader
{
    extension(SourceChoice choice)
    {
        /// <summary>Whether this choice is about the disagreement's own field (and volume).</summary>
        public bool Covers(SyncDiscrepancy discrepancy) =>
            choice.SeriesId == discrepancy.SeriesId
            && choice.Field == discrepancy.Field
            && choice.VolumeNumber == discrepancy.VolumeNumber;
    }

    extension(SyncDiscrepancy discrepancy)
    {
        /// <param name="choice">The user's current choice for this disagreement's key, if any.</param>
        public DiscrepancyResolution ResolutionUnder(SourceChoice? choice)
        {
            if (choice is not null)
            {
                return new DiscrepancyResolution(choice.Source, ByChoice: true);
            }

            // Recorded as the user's pick, and the pick has since been reset: the row no
            // longer describes what happens, so it reads as what automatic sync would do.
            return discrepancy.ResolvedByChoice
                ? new DiscrepancyResolution(AutomaticSource(discrepancy), ByChoice: false)
                : new DiscrepancyResolution(discrepancy.ResolvedWith, ByChoice: false);
        }
    }

    // Only the volume count is settled automatically every time: the higher count is
    // believed, a lower one leaves the shelf's own list standing. Dates and statuses are
    // open questions until somebody picks.
    private static MetadataSourceName? AutomaticSource(SyncDiscrepancy discrepancy)
    {
        if (discrepancy.Field is not SyncDiscrepancyField.VolumeCount)
        {
            return null;
        }

        if (!DiscrepancyValueFormat.TryParseCount(ValueOf(discrepancy, MetadataSourceName.AniList), out var shelf)
            || !DiscrepancyValueFormat.TryParseCount(ValueOf(discrepancy, MetadataSourceName.MangaUpdates), out var source))
        {
            return null;
        }

        return source > shelf ? MetadataSourceName.MangaUpdates : MetadataSourceName.AniList;
    }

    private static string? ValueOf(SyncDiscrepancy discrepancy, MetadataSourceName source) =>
        discrepancy.Values.FirstOrDefault(value => value.Source == source)?.Value;
}
