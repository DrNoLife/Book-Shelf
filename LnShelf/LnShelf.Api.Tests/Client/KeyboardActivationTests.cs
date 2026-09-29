namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Keyboard handling on real buttons, where the browser's own activation and a keydown
/// handler kept doing the same thing twice. Asserted against the source, which is this
/// project's convention for client behaviour with no browser in the test run (see
/// ClientSource, StateFlagsAreReadTests).
/// </summary>
public sealed class KeyboardActivationTests
{
    /// <summary>
    /// Enter and space already click a button, so handling them on keydown as well opened
    /// the dropdown and let the click close it again.
    /// </summary>
    [Fact]
    public void Dropdown_EnterAndSpace_AreLeftToTheNativeClick()
    {
        var codeBehind = ClientSource.ReadAllText(Path.Combine("Components", "Dropdown.razor.cs"));

        codeBehind.ShouldNotContain("\"Enter\"");
        codeBehind.ShouldNotContain("\" \"");
    }

    /// <summary>
    /// The tile and the row stay focusable while a write is in flight. A disabled button
    /// drops focus, so one space press used to leave the arrow keys nothing to move from.
    /// </summary>
    [Theory]
    [InlineData("VolumeTile.razor")]
    [InlineData("VolumeList.razor")]
    public void VolumeButtons_WhileSaving_AreAriaDisabledNotDisabled(string markupFile)
    {
        var markup = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Components", markupFile));

        markup.ShouldContain("aria-disabled=\"@((!IsInteractive).ToAriaState())\"");
        markup.ShouldNotContain("disabled=\"@(!IsInteractive)\"");

        // Space cycles read on keydown; its native click fires on keyup and would open the
        // menu too, unless keyup's default is prevented.
        markup.ShouldContain("@onkeyup:preventDefault");
    }

    /// <summary>
    /// Enter opens the tile's menu through the native click alone; a keydown case as well
    /// requested the menu twice, which toggled it shut.
    /// </summary>
    [Fact]
    public void VolumeTile_Enter_IsLeftToTheNativeClick() =>
        ClientSource.ReadAllText(Path.Combine("Features", "Series", "Components", "VolumeTile.razor.cs"))
            .ShouldNotContain("case \"Enter\"");
}
