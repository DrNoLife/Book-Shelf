using System.Reflection;
using LnShelf.Shared.Sharing;

namespace LnShelf.Api.Tests.Contracts;

/// <summary>
/// The shared shelf's response is public, so every field on it is a decision (feature-spec §4.8).
/// </summary>
// Fails the moment someone adds Owned, SeriesId, a date or an email to the public contract,
// which is the kind of change that looks harmless in a diff and leaks on every shared link.
public sealed class SharedShelfContractTests
{
    [Fact]
    public void SharedSeriesCardResponse_Properties_AreExactlyTheAllowedSet() =>
        PropertyNames<SharedSeriesCardResponse>().ShouldBe(
            ["CoverObjectKey", "Format", "Out", "PublicationStatus", "Read", "TitleEnglish", "TitleRomaji", "TrackingStatus"]);

    [Fact]
    public void SharedShelfResponse_Properties_AreExactlyTheAllowedSet()
    {
        PropertyNames<SharedShelfResponse>().ShouldBe(["Series", "Totals"]);
        PropertyNames<SharedShelfTotals>().ShouldBe(["SeriesCompleted", "SeriesReading", "VolumesRead"]);
    }

    private static string[] PropertyNames<T>() =>
    [
        .. typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal),
    ];
}
