namespace LnShelf.Client.Components;

/// <summary>
/// Renders an ARIA state as the literal string the specification requires.
/// </summary>
// Blazor renders a bool attribute the HTML way — present when true, omitted when
// false — so `aria-pressed="@isActive"` produces `aria-pressed=""` on the pressed
// control and nothing at all on the others. Neither is a valid ARIA state, so the
// toggle's state is invisible to a screen reader even though the fill is right.
// Every aria-pressed / aria-expanded / aria-selected in this client goes through
// here rather than trusting the bool.
public static class AriaExtensions
{
    extension(bool state)
    {
        public string ToAriaState() => state ? "true" : "false";
    }
}
