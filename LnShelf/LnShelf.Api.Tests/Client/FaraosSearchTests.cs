using LnShelf.Client;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The Faraos link searches by series, never by volume, and names light novels the way
/// Faraos does.
/// </summary>
public sealed class FaraosSearchTests
{
    [Fact]
    public void UrlFor_LightNovel_AppendsLnToTheTitle()
    {
        var url = FaraosSearch.UrlFor("Overlord", Format.LightNovel);

        Assert.Equal("https://www.faraos.dk/filtrering?search=Overlord%20Ln", url);
    }

    [Fact]
    public void UrlFor_Manga_SearchesTheTitleAlone()
    {
        var url = FaraosSearch.UrlFor("Berserk", Format.Manga);

        Assert.Equal("https://www.faraos.dk/filtrering?search=Berserk", url);
    }

    [Fact]
    public void UrlFor_TitleWithReservedCharacters_EscapesThem()
    {
        var url = FaraosSearch.UrlFor("Re:ZERO & Friends", Format.Manga);

        Assert.Equal("https://www.faraos.dk/filtrering?search=Re%3AZERO%20%26%20Friends", url);
    }
}
