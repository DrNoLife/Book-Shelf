namespace LnShelf.Api.Features.Auth.Mappers;

/// <summary>
/// The rows a user owns → the export download. Hand-written, explicit, no reflection
/// (CLAUDE.md, Mapping).
/// </summary>
public static class AccountExportMapper
{
    public static AccountExportAccount ToAccount(User user) => new()
    {
        Id = user.Id,
        Issuer = user.Issuer,
        Subject = user.Subject,
        Email = user.Email,
        CreatedAt = user.CreatedAt,
        LastSeenAt = user.LastSeenAt,
    };

    public static AccountExportSharing ToSharing(ShelfShare? share) => new()
    {
        IsPublic = share is not null,
        Token = share?.Token,
        TokenCreatedAt = share?.CreatedAt,
    };

    public static AccountExportSeries ToSeries(Series series) => new()
    {
        Id = series.Id,
        Format = series.Format,
        TitleEnglish = series.TitleEnglish,
        TitleRomaji = series.TitleRomaji,
        TitleNative = series.TitleNative,
        AlternativeTitles = series.AlternativeTitles,
        PublicationStatus = series.PublicationStatus,
        Publisher = series.Publisher,
        StartYear = series.StartYear,
        Genres = series.Genres,
        Author = series.Author,
        Illustrator = series.Illustrator,
        Description = series.Description,
        AniListId = series.AniListId,
        MangaUpdatesId = series.MangaUpdatesId,

        // A series nothing has been recorded on has no state row, and reads as PlanToStart
        // everywhere else (SeriesUserState.Untouched), so the export says the same.
        TrackingStatus = series.UserState?.TrackingStatus ?? TrackingStatus.PlanToStart,
        StartedAt = series.UserState?.StartedAt,
        LastReadAt = series.UserState?.LastReadAt,
        Notes = series.UserState?.Notes,
        NotificationsMuted = series.UserState?.NotificationsMuted ?? false,
        HasNewVolume = series.UserState?.HasNewVolume ?? false,
        VolumeViewOverride = series.UserState?.VolumeViewOverride,

        Volumes = series.Volumes.OrderBy(volume => volume.Number).Select(ToVolume).ToList(),
    };

    public static AccountExportImportBatch ToImportBatch(ImportBatch batch) => new()
    {
        FileName = batch.FileName,
        UploadedAt = batch.UploadedAt,
        RowCount = batch.RowCount,
        CommittedAt = batch.CommittedAt,
        Groups = batch.Groups.OrderBy(group => group.SeriesTitle, StringComparer.Ordinal).Select(ToImportGroup).ToList(),
    };

    public static AccountExportSyncRun ToSyncRun(SyncRun run) => new()
    {
        StartedAt = run.StartedAt,
        FinishedAt = run.FinishedAt,
        CoversWholeShelf = run.CoversWholeShelf,
        Summary = run.Summary,
        SeriesRefreshed = run.SeriesRefreshed,
        SeriesLinked = run.SeriesLinked,
        DatesMoved = run.DatesMoved,
        VolumesEnriched = run.VolumesEnriched,
        VolumeCoversCached = run.VolumeCoversCached,
        DescriptionsWritten = run.DescriptionsWritten,
        VolumesResolvedBySearch = run.VolumesResolvedBySearch,
        EnrichmentGaps = run.EnrichmentGaps,
        EnrichmentFailures = run.EnrichmentFailures,
        VolumesMarkedReleased = run.VolumesMarkedReleased,
        NewVolumes = run.NewVolumes,
        Failures = run.Failures,
        Discrepancies = run.Discrepancies.Select(ToDiscrepancy).ToList(),
    };

    private static AccountExportVolume ToVolume(Volume volume) => new()
    {
        Number = volume.Number,
        Title = volume.Title,
        Isbn = volume.Isbn,
        ReleaseDate = volume.ReleaseDate,
        IsReleased = volume.IsReleased,
        IsLocalOnly = volume.IsLocalOnly,

        // No state row is Unread and NotOwned: absence is the default (VolumeUserState).
        ReadStatus = volume.UserState?.ReadStatus ?? ReadStatus.Unread,
        ReadAt = volume.UserState?.ReadAt,
        Owned = volume.UserState?.Owned ?? false,
        OwnedFormat = volume.UserState?.OwnedFormat,
        PurchasePrice = volume.UserState?.PurchasePrice,
        PurchasedAt = volume.UserState?.PurchasedAt,
        CoverOverrideObjectKey = volume.UserState?.CoverOverrideObjectKey,
    };

    private static AccountExportImportGroup ToImportGroup(ImportGroup group) => new()
    {
        SeriesTitle = group.SeriesTitle,
        Status = group.Status,
        Reason = group.Reason,
        MatchedAniListId = group.MatchedAniListId,
        MatchScore = group.MatchScore,
        TreatReadAsOwned = group.TreatReadAsOwned,
        Rows = group.Rows.OrderBy(row => row.LineNumber).Select(ToImportRow).ToList(),
    };

    private static AccountExportImportRow ToImportRow(ImportRow row) => new()
    {
        LineNumber = row.LineNumber,
        RawTitle = row.RawTitle,
        Subtitle = row.Subtitle,
        Author = row.Author,
        Isbn = row.Isbn,
        VolumeNumber = row.VolumeNumber,
        ReadStatus = row.ReadStatus,
        ReadAt = row.ReadAt,
        OwnedInExport = row.OwnedInExport,
        OwnedFormat = row.OwnedFormat,
    };

    private static AccountExportDiscrepancy ToDiscrepancy(SyncDiscrepancy discrepancy) => new()
    {
        SeriesTitle = discrepancy.SeriesTitle,
        Field = discrepancy.Field,
        VolumeNumber = discrepancy.VolumeNumber,
        Note = discrepancy.Note,
        ResolvedWith = discrepancy.ResolvedWith,
        Values = discrepancy.Values
            .Select(value => new AccountExportSourceValue { Source = value.Source, Value = value.Value })
            .ToList(),
    };
}
