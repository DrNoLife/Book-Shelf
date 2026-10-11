using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The installable app (feature-spec §4.10): a browser silently refuses to offer "Install" or
/// to add a proper home-screen icon when the manifest is wrong, so what it needs is asserted.
/// </summary>
public sealed partial class WebAppManifestTests
{
    private static readonly JsonElement Manifest =
        JsonDocument.Parse(ClientSource.ReadAllText(Path.Combine("wwwroot", "manifest.webmanifest"))).RootElement;

    [Theory]
    [InlineData("name", "LN Shelf")]
    [InlineData("short_name", "LN Shelf")]
    [InlineData("start_url", "/")]
    [InlineData("scope", "/")]
    [InlineData("display", "standalone")]
    public void Manifest_CarriesWhatInstallingNeeds(string property, string expected) =>
        Manifest.GetProperty(property).GetString().ShouldBe(expected);

    // The colours are the page's own (--bg), so the splash screen and the status bar match it.
    [Theory]
    [InlineData("background_color")]
    [InlineData("theme_color")]
    public void Manifest_Colours_AreThePageBackgroundToken(string property) =>
        Manifest.GetProperty(property).GetString().ShouldBe(PageBackground());

    [Fact]
    public void IndexHtml_LinksTheManifestAndMatchesItsThemeColour()
    {
        var index = ClientSource.ReadAllText(Path.Combine("wwwroot", "index.html"));

        index.ShouldContain("<link rel=\"manifest\" href=\"manifest.webmanifest\" />");
        index.ShouldContain($"<meta name=\"theme-color\" content=\"{PageBackground()}\" />");
    }

    // Each declared icon exists and is the size it claims: a mismatch makes Chrome skip it.
    [Fact]
    public void Manifest_EveryIcon_IsAPngOfItsDeclaredSize()
    {
        var icons = Manifest.GetProperty("icons").EnumerateArray().ToList();

        icons.Select(icon => icon.GetProperty("sizes").GetString()).ShouldContain("192x192");
        icons.Select(icon => icon.GetProperty("sizes").GetString()).ShouldContain("512x512");
        icons.ShouldContain(icon => icon.GetProperty("purpose").GetString() == "maskable");

        foreach (var icon in icons)
        {
            var source = icon.GetProperty("src").GetString().ShouldNotBeNull().TrimStart('/');
            var (width, height) = PngSize(ClientSource.ReadAllBytes(Path.Combine("wwwroot", source)));

            $"{width}x{height}".ShouldBe(icon.GetProperty("sizes").GetString(), source);
        }
    }

    private static string PageBackground() =>
        BackgroundToken().Match(ClientSource.ReadAllText(Path.Combine("wwwroot", "css", "app.css"))).Groups[1].Value;

    // A PNG starts with an 8-byte signature, then IHDR: length, type, width, height (big-endian).
    private static (int Width, int Height) PngSize(byte[] png) =>
        (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

    [GeneratedRegex(@"--bg:\s*(#[0-9a-fA-F]{6});")]
    private static partial Regex BackgroundToken();
}
