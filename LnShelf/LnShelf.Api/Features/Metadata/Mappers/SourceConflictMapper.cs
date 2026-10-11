namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// A recorded disagreement and the user's choice → the side-by-side view the series page prints.
/// </summary>
// Formatting lives here rather than in the client so the wording is the same wherever a
// disagreement is printed, and so the strings written by DiscrepancyValueFormat are read back
// by the code that knows them.
public static class SourceConflictMapper
{
    public static SourceConflictResponse ToConflict(
        RecordedDisagreement recorded,
        SourceChoice? choice)
    {
        ArgumentNullException.ThrowIfNull(recorded);

        var discrepancy = recorded.Discrepancy;
        var resolution = discrepancy.ResolutionUnder(choice);
        var (displayedSource, reason) = Displayed(discrepancy, resolution, choice);

        return new SourceConflictResponse
        {
            Field = discrepancy.Field,
            VolumeNumber = discrepancy.VolumeNumber,
            Options =
            [
                .. discrepancy.Values
                    .OrderBy(value => value.Source)
                    .Select(value => new SourceOptionResponse
                    {
                        Source = value.Source,
                        SourceName = value.Source.DisplayName,
                        Value = value.Value,
                        DisplayValue = DisplayValue(discrepancy.Field, value.Value),
                        IsDisplayed = value.Source == displayedSource,
                    }),
            ],
            DisplayedSource = displayedSource,
            Reason = reason,
            Explanation = Explain(discrepancy, reason, displayedSource, choice),
            CanChoose = discrepancy.Field is not SyncDiscrepancyField.SeriesLink,
            NeedsReview = resolution.Source is null,
            RecordedAt = recorded.RecordedAt,
            Note = discrepancy.Note,
        };
    }

    private static (MetadataSourceName? Source, SourceDisplayReason Reason) Displayed(
        SyncDiscrepancy discrepancy,
        DiscrepancyResolution resolution,
        SourceChoice? choice)
    {
        if (choice is not null)
        {
            return (choice.Source, SourceDisplayReason.YourChoice);
        }

        if (resolution.Source is { } settledBy)
        {
            return (settledBy, SourceDisplayReason.Automatic);
        }

        // Unresolved: what the shelf shows is what nothing was changed to. A future date never
        // overrules the count, and a boolean cannot overrule Completed.
        return discrepancy.Field switch
        {
            SyncDiscrepancyField.ReleaseDate => (MetadataSourceName.MangaUpdates, SourceDisplayReason.WaitingOnYou),
            SyncDiscrepancyField.Status => (MetadataSourceName.AniList, SourceDisplayReason.WaitingOnYou),
            _ => (null, SourceDisplayReason.WaitingOnYou),
        };
    }

    private static string Explain(
        SyncDiscrepancy discrepancy,
        SourceDisplayReason reason,
        MetadataSourceName? displayedSource,
        SourceChoice? choice)
    {
        switch (reason)
        {
            case SourceDisplayReason.YourChoice:
                var sentence = $"Your choice. Later syncs follow {choice!.Source.DisplayName} for this.";

                return TakesEffectLater(discrepancy, choice)
                    ? $"{sentence} Takes effect at the next sync."
                    : sentence;

            case SourceDisplayReason.Automatic:
                return ExplainAutomatic(discrepancy, displayedSource);
        }

        return discrepancy.Field switch
        {
            SyncDiscrepancyField.ReleaseDate =>
                "Nothing was changed: the count says it's out, and a future date never overrules it.",
            SyncDiscrepancyField.Status =>
                "MangaUpdates only says it isn't finished, so Completed stands until you pick.",
            _ => "No release source is linked to this series yet.",
        };
    }

    private static string ExplainAutomatic(SyncDiscrepancy discrepancy, MetadataSourceName? displayedSource)
    {
        if (discrepancy.Field is SyncDiscrepancyField.SeriesLink)
        {
            return $"You linked this series to {displayedSource?.DisplayName ?? "a release source"}.";
        }

        if (discrepancy.Field is not SyncDiscrepancyField.VolumeCount)
        {
            return "Settled by the usual precedence between sources.";
        }

        if (displayedSource is MetadataSourceName.MangaUpdates)
        {
            return "Volume counts come from MangaUpdates.";
        }

        var sourceValue = discrepancy.Values
            .FirstOrDefault(value => value.Source is MetadataSourceName.MangaUpdates)?.Value;

        return DiscrepancyValueFormat.TryParseCount(sourceValue, out var count)
            ? $"Volumes are never removed, so AniList's list stays; only MangaUpdates' {count} count as out."
            : "Volumes are never removed, so AniList's list stays.";
    }

    // The applier could not act on the recorded value, so the choice is saved and the next
    // refresh applies it from the source's fresh answer. Today only a row recorded before
    // statuses were written as words ("not completed") lands here.
    private static bool TakesEffectLater(SyncDiscrepancy discrepancy, SourceChoice choice)
    {
        var chosenValue = discrepancy.Values.FirstOrDefault(value => value.Source == choice.Source)?.Value;

        return discrepancy.Field switch
        {
            SyncDiscrepancyField.ReleaseDate when choice.Source is not MetadataSourceName.MangaUpdates =>
                !DiscrepancyValueFormat.TryParseDate(chosenValue, out _),
            SyncDiscrepancyField.Status when choice.Source is MetadataSourceName.MangaUpdates =>
                !DiscrepancyValueFormat.TryParseStatus(chosenValue, out _),
            _ => false,
        };
    }

    private static string DisplayValue(SyncDiscrepancyField field, string? value)
    {
        if (value is null)
        {
            return "nothing";
        }

        return field switch
        {
            SyncDiscrepancyField.ReleaseDate when DiscrepancyValueFormat.TryParseDate(value, out var date) =>
                date.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
            SyncDiscrepancyField.VolumeCount when DiscrepancyValueFormat.TryParseCount(value, out var count) =>
                count == 1 ? "1 volume" : $"{count} volumes",

            // Rows recorded before statuses were written as words.
            SyncDiscrepancyField.Status when value == "not completed" => "not finished",
            _ => value,
        };
    }
}
