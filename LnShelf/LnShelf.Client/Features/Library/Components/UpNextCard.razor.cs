namespace LnShelf.Client.Features.Library.Components;

public partial class UpNextCard
{
    [Parameter]
    public required ContinueReadingEntry Entry { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback<ContinueReadingEntry> Finished { get; set; }

    private LibraryCardResponse Card => Entry.Card;

    private string CoverWord => Entry is NextChapterEntry ? "Chapter" : "Volume";

    private decimal CoverNumber => Entry switch
    {
        NextChapterEntry chapter => chapter.NextChapter,
        NextVolumeEntry volume => volume.NextVolumeNumber,
        _ => 0,
    };

    private string Eyebrow => Entry switch
    {
        NextChapterEntry chapter => $"Next: Ch. {chapter.NextChapter.ToVolumeText()}",
        NextVolumeEntry volume =>
            $"Vol {volume.NextVolumeNumber.ToVolumeText()} of {CardVolumeNumbers.LastMainOut(Card.VolumeTicks).ToVolumeText()}",
        _ => string.Empty,
    };

    // Only the volumes that are out: the strip is "how far through what exists", as the
    // card's "Vol 18 of 22" is.
    private List<VolumeTickResponse> OutTicks => [.. Card.VolumeTicks.Where(tick => tick.IsOut)];

    private IReadOnlyList<VolumeTickKind> OutKinds => [.. OutTicks.Select(tick => tick.Kind)];

    private int? NextIndex(NextVolumeEntry volume)
    {
        var index = OutTicks.FindIndex(tick => tick.Number == volume.NextVolumeNumber);
        return index < 0 ? null : index;
    }

    // Backlog counts the next volume itself, so what is left after it is one fewer.
    private string ShelfText => (Card.Backlog - 1) switch
    {
        <= 0 => "Last one on your shelf",
        1 => "1 more on your shelf after this",
        var left => $"{left} more on your shelf after this",
    };
}
