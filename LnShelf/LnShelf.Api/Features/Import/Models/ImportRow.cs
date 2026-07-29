namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// One book from the export, parsed. Staging only — nothing here is the shelf
/// until the batch is committed.
/// </summary>
public sealed class ImportRow
{
    public Guid Id { get; set; }
    public Guid ImportGroupId { get; set; }

    /// <summary>1-based line in the file, so a complaint can be traced back to it.</summary>
    public int LineNumber { get; set; }

    /// <summary>Kept verbatim: it is the evidence for how the parse turned out.</summary>
    public string RawTitle { get; set; } = string.Empty;

    public string? Author { get; set; }
    public string? Isbn { get; set; }

    /// <summary>Null when the title carried no volume number at all.</summary>
    public int? VolumeNumber { get; set; }

    public ReadStatus ReadStatus { get; set; }

    /// <summary>
    /// What the export's own "Owned?" column said. Effective ownership also
    /// depends on the group's read-implies-owned setting — see
    /// <c>ImportOwnershipRule</c>.
    /// </summary>
    public bool OwnedInExport { get; set; }

    public OwnedFormat? OwnedFormat { get; set; }
    public DateOnly? ReadAt { get; set; }
}
