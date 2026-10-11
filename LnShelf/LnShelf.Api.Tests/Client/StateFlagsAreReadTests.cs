using LnShelf.Client.Features.Search;
using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Three flags that were wrong in the same way: written by the state container and never
/// read by the markup, or read by markup that could not tell two things apart. Asserted
/// against the source, which is this project's convention for client behaviour with no
/// browser in the test run (see ClientSource, LibraryGridDensityStylesheetTests).
/// </summary>
public sealed class StateFlagsAreReadTests
{
    [Fact]
    public void SeriesSearchState_SearchingAndAdding_AreSeparateFlags()
    {
        var state = typeof(SeriesSearchState);

        // One flag for both meant pressing Add replaced the result list with
        // "Searching…" until the POST came back.
        state.GetProperty(nameof(SeriesSearchState.IsSearching)).ShouldNotBeNull();
        state.GetProperty(nameof(SeriesSearchState.IsAdding)).ShouldNotBeNull();
        state.GetProperty(nameof(SeriesSearchState.IsBusy)).ShouldNotBeNull();
    }

    [Fact]
    public void SearchPage_TheSearchingNotice_IsGatedOnTheSearchAlone()
    {
        var markup = ClientSource.ReadAllText(
            Path.Combine("Features", "Search", "Pages", "SearchPage.razor"));

        markup.ShouldContain("@if (SearchState.IsSearching)");
    }

    /// <summary>
    /// The add controls still go inert for either call — a second Add while the first is in
    /// flight is the race this prevents. The page hands IsBusy down, so both flags reach the
    /// row's Add and the preview's "Add to shelf" through one parameter.
    /// </summary>
    [Theory]
    [InlineData("SearchEntryRow.razor")]
    [InlineData("SearchPreview.razor")]
    [InlineData("SearchWorkCard.razor")]
    public void SearchAddButtons_GoInertWhileEitherCallIsInFlight(string markupFile)
    {
        ClientSource.ReadAllText(Path.Combine("Features", "Search", "Pages", "SearchPage.razor"))
            .ShouldContain("IsBusy=\"SearchState.IsBusy\"");

        ClientSource.ReadAllText(Path.Combine("Features", "Search", "Components", markupFile))
            .ShouldContain("disabled=\"@IsBusy\"");
    }

    [Fact]
    public void SeriesPage_TheLoadingBranch_ChecksTheSeriesAgainstTheRoute()
    {
        var codeBehind = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Pages", "SeriesPage.razor.cs"));

        // SeriesState is scoped, which in a WASM app means it survives the navigation:
        // following the hero's relation link used to leave the previous series' hero and
        // volume grid on screen under the new URL until the fetch landed.
        codeBehind.ShouldContain("SeriesState.IsLoading");
        codeBehind.ShouldContain("Series.SeriesId != SeriesId");

        ClientSource.ReadAllText(Path.Combine("Features", "Series", "Pages", "SeriesPage.razor"))
            .ShouldContain("else if (IsLoading || Series is null)");
    }

    [Fact]
    public void SeriesState_HasNoUnreadFlagsLeft()
    {
        // IsLoading is now read by the page; LibraryState's copy was deleted because the
        // library deliberately keeps its cards while a refilter is in flight.
        typeof(SeriesState).GetProperty("IsLoading").ShouldNotBeNull();
        typeof(LnShelf.Client.Features.Library.LibraryState).GetProperty("IsLoading").ShouldBeNull();
    }

    [Theory]
    [InlineData("Features/Library/Components/ShelfSection.razor")]
    [InlineData("Features/Profile/Components/LibrarySettingsCard.razor")]
    [InlineData("Features/Series/Pages/SeriesPage.razor")]
    public void EveryPageWithPreferenceControls_PrintsAFailedSave(string markupPath)
    {
        // The message was set on every failed save and rendered by nobody, so a density,
        // sort or view that did not save reverted silently on the next load.
        ClientSource.ReadAllText(markupPath.Replace('/', Path.DirectorySeparatorChar))
            .ShouldContain("Preferences.Message");
    }
}
