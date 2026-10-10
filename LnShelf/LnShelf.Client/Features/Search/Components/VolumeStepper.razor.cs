namespace LnShelf.Client.Features.Search.Components;

public partial class VolumeStepper
{
    /// <summary>"Volumes read".</summary>
    [Parameter]
    public required string Label { get; set; }

    [Parameter]
    public int Value { get; set; }

    /// <summary>The minus button's accessible name: "Fewer volumes read".</summary>
    [Parameter]
    public required string DecreaseLabel { get; set; }

    /// <summary>The plus button's accessible name: "More volumes read".</summary>
    [Parameter]
    public required string IncreaseLabel { get; set; }

    [Parameter]
    public bool CanDecrease { get; set; }

    [Parameter]
    public bool CanIncrease { get; set; }

    [Parameter]
    public EventCallback Decreased { get; set; }

    [Parameter]
    public EventCallback Increased { get; set; }
}
