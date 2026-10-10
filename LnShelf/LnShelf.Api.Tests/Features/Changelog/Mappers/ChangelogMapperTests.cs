using LnShelf.Api.Features.Changelog.Mappers;
using LnShelf.Api.Features.Changelog.Models;

namespace LnShelf.Api.Tests.Features.Changelog.Mappers;

public sealed class ChangelogMapperTests
{
    [Fact]
    public void ToEntry_EntryWithThreeCategories_CarriesEachAcrossUnchanged()
    {
        var entry = new ChangelogEntry("1.2.3", new DateOnly(2026, 1, 2), "Title",
            Features: ["feature"], Improvements: ["one", "two"], Fixes: []);

        var response = ChangelogMapper.ToEntry(entry);

        response.Features.ShouldBe(["feature"]);
        response.Improvements.ShouldBe(["one", "two"]);
        response.Fixes.ShouldBeEmpty();
    }
}
