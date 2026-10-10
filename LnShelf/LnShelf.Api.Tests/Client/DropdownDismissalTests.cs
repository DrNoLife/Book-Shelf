namespace LnShelf.Api.Tests.Client;

/// <summary>
/// When the dropdown closes itself. Asserted against the source, which is this project's
/// convention for client behaviour with no browser in the test run (see ClientSource,
/// KeyboardActivationTests).
/// </summary>
public sealed class DropdownDismissalTests
{
    /// <summary>
    /// Pressing an option moves focus from the button to the option, which is a focusout
    /// on the dropdown. Closing on that removed the option before its click landed, so
    /// the tracking-status and sort dropdowns opened and never chose anything.
    /// </summary>
    [Fact]
    public void Dropdown_FocusOut_ClosesOnlyOnceFocusHasLeftTheDropdown()
    {
        var codeBehind = ClientSource.ReadAllText(Path.Combine("Components", "Dropdown.razor.cs"));
        var markup = ClientSource.ReadAllText(Path.Combine("Components", "Dropdown.razor"));

        // The root is measured, and the check runs before the menu is closed.
        markup.ShouldContain("@ref=\"_root\"");

        var focusCheck = codeBehind.IndexOf("lnshelf.hasFocusWithin", StringComparison.Ordinal);
        var close = codeBehind.LastIndexOf("IsOpen = false;", StringComparison.Ordinal);

        focusCheck.ShouldBeGreaterThan(-1);
        close.ShouldBeGreaterThan(focusCheck);
    }

    [Fact]
    public void Dropdown_TheFocusCheckItCalls_ExistsInTheScript() =>
        ClientSource.ReadAllText(Path.Combine("wwwroot", "js", "lnshelf.js"))
            .ShouldContain("hasFocusWithin: function");
}
