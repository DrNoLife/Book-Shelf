namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Decides what one source's facts about one volume may actually write, and records the
/// disagreement where they contradict a source that already spoke.
/// </summary>
// Extracted from VolumeEnrichmentService when a second source arrived. That class's job is
// "which volumes are worth asking about, and who to ask"; this one's is "given an answer,
// what may be written from it", which is where all the conflict rules are. One copy of the
// rules, two callers — the per-ISBN pass and the catalogue pass — so the two cannot drift.
//
// What it never does: write user state, or touch VolumeUserState.CoverOverrideObjectKey.
// It only ever writes Volume.*, which is what keeps "sync never overwrites user state" and
// "overrides always win" true by construction.
public sealed class VolumeFactWriter(
    IVolumeCoverCache coverCache,
    ILogger<VolumeFactWriter> logger)
{
    /// <param name="today">
    /// "In the future" is relative to a day, so the caller supplies it — the same
    /// convention the projections follow.
    /// </param>
    public async Task ApplyAsync(
        Series series,
        Volume volume,
        VolumeFacts facts,
        SyncRun run,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(run);

        ApplyReleaseDate(series, volume, facts, run, today);
        ApplyTitle(volume, facts);
        ApplyDescription(volume, facts, run);
        await ApplyCoverAsync(volume, facts, run, today, cancellationToken);
    }

    /// <summary>
    /// Writes the date, unless it contradicts what the release source already said or a
    /// source lower in precedence would be replacing one.
    /// </summary>
    // The rule the verdict settled (§1). A real date wins for display where one exists,
    // but IsReleased stays the release model: it is a sourced count covering the whole
    // shelf, where a per-volume date covers only the volumes some catalogue happens to
    // carry.
    //
    // So a date in the past is written — it agrees with IsReleased and says more than it
    // does. A future date on a volume the release source has already counted as in print
    // is the two sources contradicting each other about one volume, and the standing rule
    // for that is "recorded, never silently picked": the disagreement becomes a row for a
    // person and the existing value is left exactly as it was. Writing it would put a
    // future date on a volume the shelf simultaneously reports as out, which is the
    // silent inconsistency the rule exists to prevent.
    //
    // A future date on a volume nothing calls in print is the opposite case and is written
    // outright: that is the announced-becomes-dated upgrade, and it is the whole point of
    // having per-volume dates.
    private void ApplyReleaseDate(
        Series series,
        Volume volume,
        VolumeFacts facts,
        SyncRun run,
        DateOnly today)
    {
        if (facts.ReleaseDate is not { } releaseDate)
        {
            if (facts.RawReleaseDate is { } raw)
            {
                // Said out loud: "2022" is a real fact that is not a date, and going
                // quiet about it would look like the record had none at all.
                logger.LogInformation(
                    "{RecordKey} dates {TitleEnglish} volume {VolumeNumber} only as {RawReleaseDate}, "
                    + "which names no day",
                    facts.SourceRecordKey,
                    series.TitleEnglish,
                    volume.Number,
                    raw);
            }

            return;
        }

        if (releaseDate == volume.ReleaseDate)
        {
            return;
        }

        if (releaseDate > today && volume.IsReleased)
        {
            run.Discrepancies.Add(new SyncDiscrepancy
            {
                Id = Guid.CreateVersion7(),
                SeriesId = series.Id,
                SeriesTitle = series.TitleEnglish,
                Field = SyncDiscrepancyField.ReleaseDate,
                VolumeNumber = volume.Number,
                ResolvedWith = null,
                Note = $"Record {facts.SourceRecordKey} from {facts.SourceName}. "
                       + "The date was not written; the count still decides whether this volume is out.",
            }
                .Says(MetadataSourceName.MangaUpdates, "in print")
                .Says(
                    facts.Source,
                    releaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            logger.LogInformation(
                "{TitleEnglish} volume {VolumeNumber}: the release source says in print, {SourceName} says "
                + "{ReleaseDate}. Recorded for review; nothing written",
                series.TitleEnglish,
                volume.Number,
                facts.SourceName,
                releaseDate);

            return;
        }

        // Precedence between the two per-volume sources, checked after the conflict above so a
        // contradiction is still recorded whichever source raised it. Without it the two passes
        // wrote in turn — the store's date, then Open Library's — so disagreeing sources moved
        // the date twice every night and the run reported two dates written for no change.
        if (volume.ReleaseDate is not null && !facts.MayReplaceReleaseDate)
        {
            return;
        }

        volume.ReleaseDate = releaseDate;
        run.DatesMoved++;
    }

    /// <summary>
    /// Fills the title in — as this volume's own subtitle, not the record's whole name —
    /// and never replaces a subtitle already written.
    /// </summary>
    // Both passes write through the same extractor, because "what a title column holds" is
    // a rule about the shelf rather than about either source. A store names a volume the way
    // a shop does ("86—EIGHTY-SIX, Vol. 3: Run Through the Battlefront (Finish)") and the row
    // being written already knows the series and the number; the leftover is the only new
    // fact in the string, and the only part that survives a tile caption's ellipsis.
    //
    // The empty string is written on purpose and is not a blank: it says the source named
    // this volume and it has no subtitle of its own, which is the ordinary case for a whole
    // series (every Slime volume). Without it, fill-if-null would re-ask about those volumes
    // on every run, forever, for an answer that has already been given (Volume.Title).
    //
    // Fill-if-null, which is a change: the by-ISBN pass used to let the source's current
    // answer replace the last one it gave. That was defensible with one source and is not
    // with two. There is no per-field provenance column, so nothing records *which* source
    // wrote the title that is there — which means last-writer-wins would have Apple Books
    // and Open Library overwriting each other on alternate nights, forever, with the
    // winner decided by whichever pass happened to have a record that run.
    //
    // First-wins in the passes' own order (catalogue, then Open Library) is the only rule
    // that cannot flip-flop. The cost is that a corrected title upstream never reaches a
    // volume that already has one, which is the cheaper failure: it is fixed by clearing
    // the column, whereas a nightly flip is not fixable at all.
    //
    // The one exception, and it cannot flip either because it only ever moves one way: an
    // empty title accepts a real subtitle. The store is first in precedence and measurably
    // never carries one — 0 of 282 live Apple titles say anything after the volume number —
    // so without this, the by-ISBN source's subtitles could never land on a volume the store
    // had already named, even on the runs where it is asked anyway. null → "" → a subtitle,
    // and never backwards.
    //
    // A record with no title of its own still resolves the volume, which is why the null
    // case coalesces to empty rather than returning. Reaching this method at all means some
    // source *placed* this volume — the catalogue matched a record to it, or its own ISBN was
    // looked up — and "placed, with no subtitle to show" is exactly what empty says. Left
    // null, the column would claim the volume was never resolved: the same settled question
    // would be re-asked every night forever, and `enrichment-verify` would count the volume
    // as one the pipeline never reached.
    private static void ApplyTitle(Volume volume, VolumeFacts facts)
    {
        var subtitle = facts.Title is { } recordTitle
            ? VolumeSubtitleExtractor.Extract(recordTitle, volume.Number)
            : string.Empty;

        if (volume.Title is null)
        {
            volume.Title = subtitle;
            return;
        }

        // Guarded rather than assigned unconditionally, so a run that learns nothing writes
        // nothing — re-running a sync has to be able to change no rows at all.
        if (volume.Title.Length is 0 && subtitle.Length > 0)
        {
            volume.Title = subtitle;
        }
    }

    /// <summary>
    /// Fills the blurb in, on the same first-wins rule as the title.
    /// </summary>
    private static void ApplyDescription(Volume volume, VolumeFacts facts, SyncRun run)
    {
        if (volume.Description is not null || facts.Description is not { } description)
        {
            return;
        }

        volume.Description = description;
        run.DescriptionsWritten++;
    }

    /// <summary>
    /// Caches the art under this volume's synced key, leaving any override untouched.
    /// </summary>
    // The override is not even consulted: an override lives on VolumeUserState under a
    // different key prefix, and the projection prefers it on read. Writing the synced key
    // while an override exists is correct and wanted — feature-spec §4.3 says removing an
    // override falls back to synced art, which requires the synced art to be there.
    private async Task ApplyCoverAsync(
        Volume volume,
        VolumeFacts facts,
        SyncRun run,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (facts.CoverImageUrl is not { } coverImageUrl || volume.CoverObjectKey is not null)
        {
            return;
        }

        // Observed on 86 volume 14, due 2026-12-08: the store's artwork for a volume that is
        // not out yet is a typographic placeholder — the title set in type on a plain field,
        // with none of the cover art the book will ship with. Cached, it becomes the shelf's
        // permanent answer for that volume, because caching is fill-if-null and the real
        // cover arriving at publication would never replace it.
        //
        // The record's own date decides, not the volume's: the volume may well have no date
        // at all, precisely because the conflict rule above refused to write this one.
        if (facts.ReleaseDate is { } releaseDate && releaseDate > today)
        {
            logger.LogInformation(
                "{RecordKey} from {SourceName} dates volume {VolumeNumber} as {ReleaseDate}, which is not "
                + "out yet; its artwork is a placeholder and was not cached",
                facts.SourceRecordKey,
                facts.SourceName,
                volume.Number,
                releaseDate);

            return;
        }

        var objectKey = await coverCache.EnsureCachedAsync(
            volume.Id, facts.SourceName, coverImageUrl, cancellationToken);

        if (objectKey is null)
        {
            return;
        }

        volume.CoverObjectKey = objectKey;
        run.VolumeCoversCached++;
    }
}
