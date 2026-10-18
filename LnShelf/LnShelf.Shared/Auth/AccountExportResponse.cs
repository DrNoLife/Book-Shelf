using LnShelf.Shared.Import;
using LnShelf.Shared.Metadata;
using LnShelf.Shared.Preferences;
using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Auth;

/// <summary>
/// Everything LN Shelf stores about the signed-in user, as the "Export my data" download
/// (GDPR Art. 15(3) and 20).
/// </summary>
// Read through the same owner filters as every other query, so it holds this user's data and
// nobody else's. Catalogue facts that came from a metadata source are included where they
// describe a series or volume on the shelf; the per-group import candidates (AniList's
// suggestions) and the session keys are not, because they are not about the person.
public sealed record AccountExportResponse
{
    public required DateTimeOffset ExportedAt { get; init; }

    public required AccountExportAccount Account { get; init; }

    public required AccountExportSharing Sharing { get; init; }

    public required AccountExportCalendar Calendar { get; init; }

    /// <summary>The devices notifications are turned on for.</summary>
    public required IReadOnlyList<AccountExportPushDevice> NotificationDevices { get; init; }

    public required PreferencesResponse Preferences { get; init; }

    /// <summary>When "Since your last visit" was last dismissed. Null if never.</summary>
    public DateTimeOffset? ActivitySeenAt { get; init; }

    /// <summary>When the user agreed to share their uploaded covers with other users. Null if they have not.</summary>
    public DateTimeOffset? CoverSharingSince { get; init; }

    /// <summary>The covers published for other users to find, one per volume.</summary>
    public required IReadOnlyList<AccountExportSharedCover> SharedCovers { get; init; }

    /// <summary>Shared covers the user chose never to be offered.</summary>
    public required IReadOnlyList<AccountExportHiddenCover> HiddenCovers { get; init; }

    public required IReadOnlyList<AccountExportSeries> Series { get; init; }

    public required IReadOnlyList<AccountExportImportBatch> ImportBatches { get; init; }

    public required IReadOnlyList<AccountExportSyncRun> SyncRuns { get; init; }

    /// <summary>Works hidden from For you with Not interested.</summary>
    public required IReadOnlyList<AccountExportRecommendationDismissal> RecommendationDismissals { get; init; }
}

/// <summary>One uploaded cover published for other users, by the volume it came from.</summary>
public sealed record AccountExportSharedCover
{
    public required Guid SourceVolumeId { get; init; }

    /// <summary>Lowercase hex SHA-256 of the image; the image itself is not in the export.</summary>
    public required string ContentHash { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}

/// <summary>One shared cover the user hid, by the hash of its image.</summary>
public sealed record AccountExportHiddenCover
{
    public required string ContentHash { get; init; }
    public required DateTimeOffset HiddenAt { get; init; }
}

/// <summary>One work hidden from For you with Not interested.</summary>
public sealed record AccountExportRecommendationDismissal
{
    public required int AniListId { get; init; }
    public required string Title { get; init; }
    public required Format Format { get; init; }
    public required DateTimeOffset DismissedAt { get; init; }
}

/// <summary>The account row.</summary>
public sealed record AccountExportAccount
{
    public required Guid Id { get; init; }

    public required string Issuer { get; init; }

    /// <summary>The provider's id for the account; what identifies the user.</summary>
    public required string Subject { get; init; }

    public required string Email { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The last sign-in.</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Where the provider's picture was copied from, when it sent one.</summary>
    public string? ProviderPictureUrl { get; init; }

    /// <summary>The stored copy of the provider's picture, by key; the image itself is not in the export.</summary>
    public string? ProviderAvatarObjectKey { get; init; }

    /// <summary>The picture the user uploaded as their avatar, by key, as uploaded covers are listed.</summary>
    public string? AvatarOverrideObjectKey { get; init; }
}

/// <summary>The public-shelf link, when there is one.</summary>
public sealed record AccountExportSharing
{
    public required bool IsPublic { get; init; }

    public string? Token { get; init; }

    public DateTimeOffset? TokenCreatedAt { get; init; }
}

/// <summary>The release calendar link, when there is one.</summary>
public sealed record AccountExportCalendar
{
    public required bool IsOn { get; init; }

    public string? Token { get; init; }

    public DateTimeOffset? TokenCreatedAt { get; init; }
}

/// <summary>
/// One device with notifications on. The push service is named by host only, and the
/// browser's encryption keys are left out: they are secrets, and say nothing about the person.
/// </summary>
public sealed record AccountExportPushDevice
{
    /// <summary>The push service, e.g. "fcm.googleapis.com".</summary>
    public required string PushServiceHost { get; init; }

    public string? DeviceLabel { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public required int FailureCount { get; init; }
}

/// <summary>One series on the shelf, with the user's state on it.</summary>
public sealed record AccountExportSeries
{
    public required Guid Id { get; init; }
    public required Format Format { get; init; }
    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }
    public required IReadOnlyList<string> AlternativeTitles { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }
    public string? Publisher { get; init; }
    public int? StartYear { get; init; }
    public required IReadOnlyList<string> Genres { get; init; }
    public string? Author { get; init; }
    public string? Illustrator { get; init; }
    public string? Description { get; init; }
    public int? AniListId { get; init; }
    public long? MangaUpdatesId { get; init; }
    public VolumeCountBasis? VolumeCountBasis { get; init; }
    public string? VolumeCountEvidence { get; init; }

    /// <summary>The latest chapter MangaUpdates lists for the series.</summary>
    public decimal? LatestChapter { get; init; }

    public required TrackingStatus TrackingStatus { get; init; }
    public DateOnly? StartedAt { get; init; }
    public DateOnly? LastReadAt { get; init; }
    public string? Notes { get; init; }
    public required bool NotificationsMuted { get; init; }
    public required bool HasNewVolume { get; init; }
    public VolumeView? VolumeViewOverride { get; init; }

    /// <summary>Whether the user counts this series in volumes or chapters.</summary>
    public ProgressUnit ProgressUnit { get; init; } = ProgressUnit.Volumes;

    /// <summary>The last chapter the user recorded as read.</summary>
    public decimal? LastChapterRead { get; init; }

    public required IReadOnlyList<AccountExportVolume> Volumes { get; init; }

    /// <summary>What syncs noticed about this series: the activity log, oldest first.</summary>
    public required IReadOnlyList<AccountExportShelfEvent> Events { get; init; }
}

/// <summary>One entry of the activity log.</summary>
public sealed record AccountExportShelfEvent
{
    public required ShelfEventKind Kind { get; init; }
    public required decimal VolumeNumber { get; init; }
    public DateOnly? PreviousDate { get; init; }
    public DateOnly? NewDate { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset? NotifiedAt { get; init; }
}

/// <summary>One volume, with the user's read and ownership state.</summary>
public sealed record AccountExportVolume
{
    public required decimal Number { get; init; }
    public string? Title { get; init; }
    public string? Isbn { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public required bool IsReleased { get; init; }
    public required bool IsLocalOnly { get; init; }

    public required ReadStatus ReadStatus { get; init; }
    public DateOnly? ReadAt { get; init; }
    public required bool Owned { get; init; }
    public OwnedFormat? OwnedFormat { get; init; }
    public decimal? PurchasePrice { get; init; }
    public DateOnly? PurchasedAt { get; init; }

    /// <summary>Whether the reader asked to be told when this undated volume gets a date.</summary>
    public bool NotifyWhenDated { get; init; }

    /// <summary>The stored key of a cover the user uploaded, if any. The image is not in the export.</summary>
    public string? CoverOverrideObjectKey { get; init; }
}

/// <summary>One uploaded export file's record (the file itself is never stored).</summary>
public sealed record AccountExportImportBatch
{
    public required string FileName { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public required int RowCount { get; init; }
    public DateTimeOffset? CommittedAt { get; init; }
    public required IReadOnlyList<AccountExportImportGroup> Groups { get; init; }
}

/// <summary>The books of one series within an import, and how they were matched.</summary>
public sealed record AccountExportImportGroup
{
    public required string SeriesTitle { get; init; }
    public required ImportGroupStatus Status { get; init; }
    public required ImportReviewReason Reason { get; init; }
    public int? MatchedAniListId { get; init; }
    public int? MatchScore { get; init; }
    public required bool TreatReadAsOwned { get; init; }
    public required IReadOnlyList<AccountExportImportRow> Rows { get; init; }
}

/// <summary>One book as the import file stated it.</summary>
public sealed record AccountExportImportRow
{
    public required int LineNumber { get; init; }
    public required string RawTitle { get; init; }
    public string? Subtitle { get; init; }
    public string? Author { get; init; }
    public string? Isbn { get; init; }
    public decimal? VolumeNumber { get; init; }
    public required ReadStatus ReadStatus { get; init; }
    public DateOnly? ReadAt { get; init; }
    public required bool OwnedInExport { get; init; }
    public OwnedFormat? OwnedFormat { get; init; }
}

/// <summary>One metadata sync of the shelf.</summary>
public sealed record AccountExportSyncRun
{
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public required bool CoversWholeShelf { get; init; }
    public required string Summary { get; init; }
    public required int SeriesRefreshed { get; init; }
    public required int SeriesLinked { get; init; }
    public required int DatesMoved { get; init; }
    public required int VolumesEnriched { get; init; }
    public required int VolumeCoversCached { get; init; }
    public required int DescriptionsWritten { get; init; }
    public required int VolumesResolvedBySearch { get; init; }
    public required int EnrichmentGaps { get; init; }
    public required int EnrichmentFailures { get; init; }
    public required int VolumesMarkedReleased { get; init; }
    public required int NewVolumes { get; init; }
    public required int Failures { get; init; }
    public required IReadOnlyList<AccountExportDiscrepancy> Discrepancies { get; init; }
}

/// <summary>A disagreement between metadata sources found by a sync.</summary>
public sealed record AccountExportDiscrepancy
{
    public required string SeriesTitle { get; init; }
    public required SyncDiscrepancyField Field { get; init; }
    public decimal? VolumeNumber { get; init; }
    public string? Note { get; init; }
    public MetadataSourceName? ResolvedWith { get; init; }
    public required IReadOnlyList<AccountExportSourceValue> Values { get; init; }
}

/// <summary>What one source said.</summary>
public sealed record AccountExportSourceValue
{
    public required MetadataSourceName Source { get; init; }
    public string? Value { get; init; }
}
