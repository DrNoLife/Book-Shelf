using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which source treats a 403 as "slow down": Apple Books only.
/// </summary>
public sealed class MetadataRateLimitingTests
{
    [Fact]
    public void AppleBooks_ThePolicy_TreatsForbiddenAsRateLimited()
    {
        using var provider = CreateProvider();

        MetadataRateLimiting.AppleBooks(provider).TreatsForbiddenAsRateLimited.ShouldBeTrue();
    }

    [Theory]
    [InlineData("AniList")]
    [InlineData("MangaUpdates")]
    [InlineData("OpenLibrary")]
    public void Policy_AnyOtherSource_LeavesA403Alone(string source)
    {
        using var provider = CreateProvider();

        // AniList's 403 means the API is disabled; it must reach the search fallback at once.
        var policy = source switch
        {
            "AniList" => MetadataRateLimiting.AniList(provider),
            "MangaUpdates" => MetadataRateLimiting.MangaUpdates(provider),
            _ => MetadataRateLimiting.OpenLibrary(provider),
        };

        policy.TreatsForbiddenAsRateLimited.ShouldBeFalse();
    }

    private static ServiceProvider CreateProvider() => new ServiceCollection()
        .AddSingleton(Options.Create(new AniListOptions()))
        .AddSingleton(Options.Create(new MangaUpdatesOptions()))
        .AddSingleton(Options.Create(new OpenLibraryOptions()))
        .AddSingleton(Options.Create(new AppleBooksOptions()))
        .BuildServiceProvider();
}
