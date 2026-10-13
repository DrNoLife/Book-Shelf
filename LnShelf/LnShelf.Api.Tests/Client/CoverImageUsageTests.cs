using System.Text.RegularExpressions;
using LnShelf.Client;
using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Every image in the client renders through CoverImage, which is what keeps a cover whose
/// file is gone from showing the browser's broken-image icon. A bare img would ship that icon
/// the first time its file is missing, as Vol 1's lost override did (2026-10-13).
/// </summary>
public sealed class CoverImageUsageTests
{
    /// <summary>
    /// The only markup allowed a bare img: CoverImage itself, and the avatar, whose own
    /// error handler falls back to the initial.
    /// </summary>
    private static readonly string[] AllowedFiles = ["CoverImage.razor", "ProfileAvatar.razor"];

    [Fact]
    public void Markup_EveryImage_RendersThroughCoverImage()
    {
        var offenders = ClientSource.Markup()
            .Where(path => !AllowedFiles.Contains(Path.GetFileName(path)))
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"<img\b", RegexOptions.IgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        offenders.ShouldBeEmpty(
            "A bare <img> shows the browser's broken-image icon when its file cannot load. "
            + "Render it through <CoverImage> with its candidates and a placeholder instead.");
    }

    public static TheoryData<string?, bool, string?, string[]> CandidateCases => new()
    {
        // An override, versioned against the browser cache, then the art behind it.
        { "override/volume/a.webp", true, "volume/apple/a.jpg", ["http://lnshelf.test/api/covers/override/volume/a.webp?v=3", "http://lnshelf.test/api/covers/volume/apple/a.jpg"] },

        // Synced art alone.
        { "volume/apple/a.jpg", false, null, ["http://lnshelf.test/api/covers/volume/apple/a.jpg"] },

        // No art: nothing to try, so the tile shows its coverless state.
        { null, false, null, [] },
    };

    [Theory]
    [MemberData(nameof(CandidateCases))]
    public void VolumeCoverUrl_Candidates_OverrideFirstThenTheArtBehindIt(
        string? coverKey, bool hasOverride, string? fallbackKey, string[] expected)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://lnshelf.test/") };
        var apiUrl = new ApiUrlBuilder(httpClient);
        var volume = new VolumeResponse
        {
            VolumeId = Guid.CreateVersion7(),
            Number = 1,
            IsOut = true,
            ReadStatus = ReadStatus.Unread,
            Owned = false,
            IsLocalOnly = false,
            CoverObjectKey = coverKey,
            HasCoverOverride = hasOverride,
            CoverFallbackObjectKey = fallbackKey,
        };

        VolumeCoverUrl.Candidates(apiUrl, volume, coverVersion: 3).ShouldBe(expected);
    }
}
