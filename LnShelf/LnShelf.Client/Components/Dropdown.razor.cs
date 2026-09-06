namespace LnShelf.Client.Components;

/// <summary>One row of a <see cref="Dropdown"/>.</summary>
public sealed record DropdownOption(string Value, string Label);

public partial class Dropdown
{
    [Parameter]
    public required IReadOnlyList<DropdownOption> Options { get; set; }

    [Parameter]
    public required string Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Shown when the value matches no option.</summary>
    [Parameter]
    public string Placeholder { get; set; } = "Choose";

    /// <summary>
    /// The control's accessible name. A dropdown whose label is only its current
    /// value announces as that value and nothing else.
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Inert, and visibly so — the same rule SegmentedControl follows. A control that
    /// vanishes while a write is in flight reads as a bug; one that dims does not.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    private bool IsOpen { get; set; }

    private string SelectedLabel =>
        Options.FirstOrDefault(option => option.Value == Value)?.Label ?? Placeholder;

    private void Toggle() => IsOpen = !IsOpen;

    private async Task SelectAsync(string value)
    {
        IsOpen = false;
        await ValueChanged.InvokeAsync(value);
    }

    private void OnButtonKeyDown(KeyboardEventArgs args)
    {
        if (args.Key is "Escape")
        {
            IsOpen = false;
        }
        else if (args.Key is "ArrowDown" or "Enter" or " ")
        {
            IsOpen = true;
        }
    }

    /// <summary>
    /// Closes on click-away, using focusout rather than a document-level listener.
    /// </summary>
    // Blazor sees focusout before the new focus lands, so the check is deferred by a
    // yield; without it, clicking an option would close the menu before its own click
    // handler ran.
    private async Task OnFocusOutAsync()
    {
        await Task.Yield();
        if (IsOpen)
        {
            IsOpen = false;
            StateHasChanged();
        }
    }
}
