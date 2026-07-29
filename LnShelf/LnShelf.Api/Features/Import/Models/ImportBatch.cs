namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// One uploaded StoryGraph export. Kept after committing: it is the record of
/// what was imported and how each series was matched.
/// </summary>
public sealed class ImportBatch
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>Books parsed from the file.</summary>
    public int RowCount { get; set; }

    /// <summary>Null until the batch has been applied to the shelf.</summary>
    public DateTimeOffset? CommittedAt { get; set; }

    public ICollection<ImportGroup> Groups { get; set; } = [];
}
