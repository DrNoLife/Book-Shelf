namespace LnShelf.Client.Features.Design.Pages;

public partial class FoundationGalleryPage
{
    private string _filter = string.Empty;
    private Format? _format;
    private string _view = "covers";
    private string _sort = "behind";
    private string _reading = "unread";

    private static readonly IReadOnlyList<SegmentedOption<string>> ViewOptions =
    [
        new("covers", "Covers", "Covers"),
        new("list", "List", "List"),
    ];

    private static readonly IReadOnlyList<SegmentedOption<string>> ReadingOptions =
    [
        new("unread", "Unread", "Unread"),
        new("reading", "Reading", "Reading"),
        new("read", "Read", "Read"),
    ];

    private static readonly IReadOnlyList<DropdownOption> SortOptions =
    [
        new("behind", "Sort: Most behind"),
        new("alphabetical", "Sort: A–Z"),
    ];
}
