namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// The staging tables' column lengths, named once so the entity configurations and the
/// upload's own trimming cannot disagree.
/// </summary>
public static class ImportColumnLimits
{
    public const int FileName = 400;
    public const int SeriesKey = 400;
    public const int SeriesTitle = 400;
    public const int RawTitle = 600;
    public const int Author = 400;
    public const int Isbn = 32;
    public const int Subtitle = 400;
}
