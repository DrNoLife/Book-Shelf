using System.Text.Json;
using LnShelf.Client.Features.Library;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The shelf's filters as one browser remembers them: how a tap changes the selection, how
/// the remembered value is written and read back, and which filters the shelf opens on.
/// </summary>
public sealed class LibraryFilterMemoryTests
{
    // The options the Blazor interop layer serialises with.
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public static TheoryData<LibraryFilter[], LibraryFilter, LibraryFilter[]> Toggles => new()
    {
        // A second filter joins the first.
        { [LibraryFilter.Behind], LibraryFilter.Reading, [LibraryFilter.Behind, LibraryFilter.Reading] },

        // Tapping a chosen one turns it off; the last one off leaves All.
        { [LibraryFilter.Behind, LibraryFilter.Reading], LibraryFilter.Behind, [LibraryFilter.Reading] },
        { [LibraryFilter.Behind], LibraryFilter.Behind, [LibraryFilter.All] },

        // All clears the rest, and anything chosen while All is on replaces it.
        { [LibraryFilter.Behind, LibraryFilter.Reading], LibraryFilter.All, [LibraryFilter.All] },
        { [LibraryFilter.All], LibraryFilter.Owned, [LibraryFilter.Owned] },
        { [LibraryFilter.All], LibraryFilter.All, [LibraryFilter.All] },
    };

    [Theory]
    [MemberData(nameof(Toggles))]
    public void Toggle_AChip_ChangesTheSelectionAsAMultiSelect(
        LibraryFilter[] current,
        LibraryFilter tapped,
        LibraryFilter[] expected) =>
        LibraryFilterSelection.Toggle(current, tapped).ShouldBe(expected);

    [Theory]
    [InlineData(new[] { LibraryFilter.Behind, LibraryFilter.Reading }, LibraryFilter.Reading, new[] { LibraryFilter.Behind })]
    [InlineData(new[] { LibraryFilter.Behind }, LibraryFilter.Behind, new[] { LibraryFilter.All })]
    public void Remove_AnActiveChip_TakesOnlyThatFilterOff(
        LibraryFilter[] current,
        LibraryFilter removed,
        LibraryFilter[] expected) =>
        LibraryFilterSelection.Remove(current, removed).ShouldBe(expected);

    [Fact]
    public void StoredLibraryFilters_WrittenAndReadBack_IsTheSameChoice()
    {
        var choice = new LibraryFilterChoice([LibraryFilter.Behind, LibraryFilter.Reading], Format.Manga);

        var json = JsonSerializer.Serialize(StoredLibraryFilters.From(choice), WebOptions);
        var read = JsonSerializer.Deserialize<StoredLibraryFilters>(json, WebOptions)!.ToChoice();

        json.ShouldBe("""{"statuses":["Behind","Reading"],"format":"Manga"}""");
        read.ShouldNotBeNull();
        read.Statuses.ShouldBe(choice.Statuses);
        read.Format.ShouldBe(Format.Manga);
    }

    public static TheoryData<string, LibraryFilter[]?, Format?> StoredValues => new()
    {
        { """{"statuses":["Owned"],"format":null}""", [LibraryFilter.Owned], null },

        // A name this release does not know is skipped; so is a number.
        { """{"statuses":["Retired","Reading","3"],"format":"LightNovel"}""", [LibraryFilter.Reading], Format.LightNovel },

        // An unknown format reads as every format, not as no memory at all.
        { """{"statuses":["Behind"],"format":"Audiobook"}""", [LibraryFilter.Behind], null },

        // Nothing usable is the same as nothing remembered.
        { """{"statuses":["Retired"]}""", null, null },
        { """{"statuses":[]}""", null, null },
        { """{}""", null, null },
    };

    [Theory]
    [MemberData(nameof(StoredValues))]
    public void StoredLibraryFiltersToChoice_WhateverWasStored_ReadsTolerantly(
        string json,
        LibraryFilter[]? expectedStatuses,
        Format? expectedFormat)
    {
        var choice = JsonSerializer.Deserialize<StoredLibraryFilters>(json, WebOptions)!.ToChoice();

        if (expectedStatuses is null)
        {
            choice.ShouldBeNull();
            return;
        }

        choice.ShouldNotBeNull();
        choice.Statuses.ShouldBe(expectedStatuses);
        choice.Format.ShouldBe(expectedFormat);
    }

    [Fact]
    public void Resolve_SomethingRemembered_WinsOverTheDefault()
    {
        var remembered = new LibraryFilterChoice([LibraryFilter.Reading], Format.Manga);

        LibraryFilterChoice.Resolve(remembered, [LibraryFilter.Owned]).ShouldBe(remembered);
    }

    [Fact]
    public void Resolve_NothingRemembered_IsTheDefaultAcrossEveryFormat()
    {
        var choice = LibraryFilterChoice.Resolve(remembered: null, [LibraryFilter.Reading, LibraryFilter.Owned]);

        choice.Statuses.ShouldBe([LibraryFilter.Owned, LibraryFilter.Reading]);
        choice.Format.ShouldBeNull();
    }

    /// <summary>The last step: preferences that never loaded still hold the built-in Behind.</summary>
    [Fact]
    public void Resolve_NothingRememberedAndNoPreferences_IsBehind() =>
        LibraryFilterChoice.Resolve(remembered: null, LibraryFilterSet.Default)
            .Statuses.ShouldBe([LibraryFilter.Behind]);
}
