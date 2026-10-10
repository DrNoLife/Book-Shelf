namespace LnShelf.Client.Layout;

public partial class MainLayout
{
    private ElementReference _mainElement;

    /// <summary>Moves focus to the content column, so the next Tab lands in the page.</summary>
    private async Task SkipToContentAsync() => await _mainElement.FocusAsync();
}
