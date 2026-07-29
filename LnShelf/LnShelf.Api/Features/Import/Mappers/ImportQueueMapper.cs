namespace LnShelf.Api.Features.Import.Mappers;

/// <summary>
/// Import entities → the wire contracts the review page renders.
/// </summary>
public static class ImportQueueMapper
{
    extension(ImportBatch batch)
    {
        public ImportBatchResponse ToResponse() => new()
        {
            BatchId = batch.Id,
            FileName = batch.FileName,
            UploadedAt = batch.UploadedAt,
            RowCount = batch.RowCount,
            GroupCount = batch.Groups.Count,
            AutoMatched = batch.Groups.Count(group => group.Status is ImportGroupStatus.AutoMatched),
            NeedsReview = batch.Groups.Count(group => group.Status is ImportGroupStatus.NeedsReview),
            Resolved = batch.Groups.Count(group => group.Status is ImportGroupStatus.Resolved),
            Skipped = batch.Groups.Count(group => group.Status is ImportGroupStatus.Skipped),
            Committed = batch.Groups.Count(group => group.Status is ImportGroupStatus.Committed),
            CommittedAt = batch.CommittedAt,
        };

        public ImportQueueResponse ToQueueResponse() => new()
        {
            Batch = batch.ToResponse(),

            // Anything needing a decision first: the page is a work queue, so the
            // work goes at the top.
            Groups =
            [
                .. batch.Groups
                    .OrderByDescending(group => group.Status is ImportGroupStatus.NeedsReview)
                    .ThenBy(group => group.SeriesTitle, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.ToResponse()),
            ],
        };
    }

    extension(ImportGroup group)
    {
        public ImportGroupResponse ToResponse() => new()
        {
            GroupId = group.Id,
            SeriesTitle = group.SeriesTitle,
            Status = group.Status,
            Reason = group.Reason,
            RowCount = group.Rows.Count,
            VolumeSummary = VolumeRangeFormatter.Format(group.Rows.Select(row => row.VolumeNumber)),
            ReadCount = group.Rows.Count(row => row.ReadStatus is ReadStatus.Read),
            TreatReadAsOwned = group.TreatReadAsOwned,
            MatchedAniListId = group.MatchedAniListId,
            MatchScore = group.MatchScore,
            Candidates = [.. group.Candidates.OrderBy(candidate => candidate.Rank).Select(candidate => candidate.ToResponse())],

            // Rows are mapped through the group because effective ownership needs
            // the group's read-implies-owned setting.
            Rows =
            [
                .. group.Rows
                    .OrderBy(row => row.VolumeNumber ?? int.MaxValue)
                    .ThenBy(row => row.LineNumber)
                    .Select(row => row.ToResponse(group.TreatReadAsOwned)),
            ],
        };
    }

    extension(ImportCandidate candidate)
    {
        public ImportCandidateResponse ToResponse() => new()
        {
            AniListId = candidate.AniListId,
            TitleEnglish = candidate.TitleEnglish,
            TitleRomaji = candidate.TitleRomaji,
            TitleNative = candidate.TitleNative,
            Format = candidate.Format,
            PublicationStatus = candidate.PublicationStatus,
            VolumeCount = candidate.VolumeCount,
            StartYear = candidate.StartYear,
            CoverObjectKey = candidate.CoverObjectKey,
            Score = candidate.Score,
        };
    }

    extension(ImportRow row)
    {
        public ImportRowResponse ToResponse(bool treatReadAsOwned)
        {
            var owned = ImportOwnershipRule.IsOwned(row.OwnedInExport, row.ReadStatus, treatReadAsOwned);

            return new ImportRowResponse
            {
                LineNumber = row.LineNumber,
                RawTitle = row.RawTitle,
                VolumeNumber = row.VolumeNumber,
                ReadStatus = row.ReadStatus,
                Owned = owned,
                OwnedFormat = owned ? row.OwnedFormat : null,
                ReadAt = row.ReadAt,
            };
        }
    }
}
