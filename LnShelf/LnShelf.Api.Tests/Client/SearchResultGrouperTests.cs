using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The search page's grouping: hits that share a base title become one work, split into the
/// main story (the base title and its numbered parts) and the side stories.
/// </summary>
public sealed class SearchResultGrouperTests
{
    private int _nextId = 1;

    [Fact]
    public void Group_TitleColonPartN_IsOneWorkNamedByPart()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Ascendance of a Bookworm: Part 2 – Apprentice Shrine Maiden"),
            Hit("Ascendance of a Bookworm: Part 1 – Daughter of a Soldier"),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.Title.ShouldBe("Ascendance of a Bookworm");
        work.MainStory.Select(entry => entry.Name).ShouldBe(["Part 1", "Part 2"]);
        work.MainStory.Select(entry => entry.Subtitle).ShouldBe(["Daughter of a Soldier", "Apprentice Shrine Maiden"]);
        work.SideStories.ShouldBeEmpty();
    }

    [Fact]
    public void Group_TitlePartNColonSubtitle_JoinsTheSameWork()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Ascendance of a Bookworm: Part 1"),
            Hit("Ascendance of a Bookworm Part 3: Adopted Daughter of an Archduke"),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.MainStory.Select(entry => (entry.Name, entry.Subtitle, entry.PartNumber)).ShouldBe(
        [
            ("Part 1", null, 1),
            ("Part 3", "Adopted Daughter of an Archduke", 3),
        ]);
    }

    [Theory]
    [InlineData("Classroom of the Elite: 2nd Season", "Season 2")]
    [InlineData("Classroom of the Elite: Season 2", "Season 2")]
    [InlineData("Classroom of the Elite - Part II", "Part 2")]
    [InlineData("Classroom of the Elite Book 2", "Book 2")]
    public void Group_SeasonsAndOtherNumberings_FollowTheBaseTitle(string numbered, string expectedName)
    {
        var works = SearchResultGrouper.Group([Hit(numbered), Hit("Classroom of the Elite")]);

        var work = works.ShouldHaveSingleItem();
        work.Title.ShouldBe("Classroom of the Elite");

        // The base title itself first — named by its format, the header already says the
        // title — then the numbered entry, named by its number.
        work.MainStory.Select(entry => entry.Name).ShouldBe(["Light novel", expectedName]);
    }

    /// <summary>MangaUpdates files Bookworm's parts in romaji: "Dai 2-bu".</summary>
    [Fact]
    public void Group_RomajiPartMarker_IsAPart()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Honzuki no Gekokujou: Shisho ni Naru Tame ni wa - Dai 2-bu Shinden no Miko Minarai"),
            Hit("Honzuki no Gekokujou: Shisho ni Naru Tame ni wa - Dai 1-bu Heishi no Musume"),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.Title.ShouldBe("Honzuki no Gekokujou: Shisho ni Naru Tame ni wa");
        work.MainStory.Select(entry => (entry.Name, entry.Subtitle)).ShouldBe(
        [
            ("Part 1", "Heishi no Musume"),
            ("Part 2", "Shinden no Miko Minarai"),
        ]);
    }

    [Fact]
    public void Group_SideStories_AreNamedByWhatTheyAddAndOrderedByYear()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Ascendance of a Bookworm: Part 1", year: 2015),
            Hit("Ascendance of a Bookworm: Short Story Collection", year: 2019, romaji: "Honzuki no Gekokujou: Tanpenshuu"),
            Hit("Ascendance of a Bookworm: Hannelore's Fifth Year at the Royal Academy", year: null),
            Hit("Ascendance of a Bookworm: Royal Academy Stories – First Year", year: 2018),
            Hit("Ascendance of a Bookworm: Part 2", year: 2015),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.MainStory.Select(entry => entry.Name).ShouldBe(["Part 1", "Part 2"]);

        // By year, oldest first; a side story with no year goes last.
        work.SideStories.Select(entry => entry.Name).ShouldBe(
        [
            "Royal Academy Stories – First Year",
            "Short Story Collection",
            "Hannelore's Fifth Year at the Royal Academy",
        ]);

        // The romaji line drops the base title the same way.
        work.SideStories[1].Subtitle.ShouldBe("Tanpenshuu");
    }

    [Fact]
    public void Group_LightNovelAndMangaOfOneTitle_AreBothMainStoryLightNovelFirst()
    {
        const string romaji = "Mushoku Tensei: Isekai Ittara Honki Dasu";
        const string native = "無職転生 ～異世界行ったら本気だす～";

        var works = SearchResultGrouper.Group(
        [
            Hit("Mushoku Tensei: Jobless Reincarnation", Format.Manga, romaji, native),
            Hit("Mushoku Tensei: Jobless Reincarnation", Format.LightNovel, romaji, native, volumes: 26),
            Hit("Mushoku Tensei: Roxy Gets Serious", Format.Manga, "Mushoku Tensei: Roxy datte Honki desu", year: 2017),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.Title.ShouldBe("Mushoku Tensei: Jobless Reincarnation");
        work.AlternativeTitles.ShouldBe($"{romaji} · {native}");
        work.MainStory.Select(entry => entry.Name).ShouldBe(["Light novel", "Manga"]);
        work.MainStory.ShouldAllBe(entry => entry.IsNamedByFormat && entry.Subtitle == $"{romaji} · {native}");

        var side = work.SideStories.ShouldHaveSingleItem();
        side.Name.ShouldBe("Roxy Gets Serious");
        side.Subtitle.ShouldBe("Roxy datte Honki desu");
    }

    /// <summary>
    /// AniList leaves the English title null for many entries, and the romaji stands in. One
    /// whose romaji is the main story's romaji is the main story.
    /// </summary>
    [Fact]
    public void Group_AnEntryListedOnlyInRomaji_JoinsTheMainStoryByItsRomaji()
    {
        const string romaji = "Mushoku Tensei: Isekai Ittara Honki Dasu";

        var works = SearchResultGrouper.Group(
        [
            Hit("Mushoku Tensei: Jobless Reincarnation", Format.LightNovel, romaji),
            Hit("Mushoku Tensei: Jobless Reincarnation ~Eris Sharpens Her Fangs~", Format.Manga),
            Hit(romaji, Format.Manga, romaji),
            Hit($"{romaji} - Shitsui no Majutsushi-hen", Format.Manga, $"{romaji} - Shitsui no Majutsushi-hen", "無職転生 失意の魔術師編"),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.MainStory.Select(entry => entry.Result.TitleEnglish).ShouldBe(["Mushoku Tensei: Jobless Reincarnation", romaji]);

        // Cut after the longest base it shares — here the main story's romaji.
        work.SideStories.Select(entry => entry.Name).ShouldBe(["Eris Sharpens Her Fangs", "Shitsui no Majutsushi-hen"]);
        work.SideStories[1].Subtitle.ShouldBe("無職転生 失意の魔術師編");
    }

    [Fact]
    public void Group_UnrelatedTitlesSharingAWord_StaySeparate()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Mushoku Tensei: Jobless Reincarnation"),
            Hit("Mushoku no Eiyuu: Betsu ni Skill nanka Ira Nakattan Daga"),
            Hit("34-sai Mushoku-san", Format.Manga),
            Hit("Mushoku Tensei: Redundant Reincarnation"),
        ]);

        works.Select(work => work.Title).ShouldBe(
        [
            "Mushoku Tensei: Jobless Reincarnation",
            "Mushoku no Eiyuu: Betsu ni Skill nanka Ira Nakattan Daga",
            "34-sai Mushoku-san",
        ]);
    }

    /// <summary>A colon with no space after it is part of the name, not a separator.</summary>
    [Fact]
    public void Group_ColonInsideAName_DoesNotJoinWorks()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Re:ZERO -Starting Life in Another World-"),
            Hit("Re:Monster"),
        ]);

        works.Count.ShouldBe(2);
    }

    /// <summary>
    /// The manga lists an English title and the light novel only its romaji; the shared
    /// romaji makes them one work, and with no title shared by both, both are main story.
    /// </summary>
    [Fact]
    public void Group_JoinedOnlyByRomaji_IsOneWorkWithEveryEntryMain()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Hero Without a Class: Who Even Needs Skills?!", Format.Manga, "Mushoku no Eiyuu: Betsu ni Skill Nanka Iranakattan daga"),
            Hit("Mushoku no Eiyuu: Betsu ni Skill nanka Ira Nakattan Daga", Format.LightNovel, "Mushoku no Eiyuu: Betsu ni Skill nanka Ira Nakattan Daga"),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.MainStory.Count.ShouldBe(2);
        work.SideStories.ShouldBeEmpty();
        work.MainStory.Select(entry => entry.Result.Format).ShouldBe([Format.LightNovel, Format.Manga]);
    }

    [Theory]
    [InlineData("ascendance of a BOOKWORM: part 2", "Ascendance of a Bookworm – Part 1")]
    [InlineData("The Saga of Tanya the Evil: Part 2", "Saga of Tanya the Evil: Part 1")]
    [InlineData("Rokudenashi Majutsu Kōshi: Part 2", "Rokudenashi Majutsu Koshi: Part 1")]
    [InlineData("Ascendance of a Bookworm!: Part 2", "Ascendance of a Bookworm: Part 1")]
    public void Group_CaseAccentArticleAndPunctuation_DoNotSplitAWork(string first, string second)
    {
        var works = SearchResultGrouper.Group([Hit(first), Hit(second)]);

        var work = works.ShouldHaveSingleItem();
        work.MainStory.Select(entry => entry.PartNumber).ShouldBe([1, 2]);
    }

    [Fact]
    public void Group_Works_KeepTheOrderOfTheirBestRankedHit()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Overlord"),
            Hit("Mushoku Tensei: Jobless Reincarnation"),
            Hit("Overlord: The Undead King Oh!"),
            Hit("Mushoku Tensei: Roxy Gets Serious"),
        ]);

        works.Select(work => work.Title).ShouldBe(["Overlord", "Mushoku Tensei: Jobless Reincarnation"]);
        works[0].SideStories.ShouldHaveSingleItem().Name.ShouldBe("The Undead King Oh!");
    }

    [Fact]
    public void Group_ASingleSeries_IsOneRowNamedInFullWithItsOtherScripts()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("The Saga of Tanya the Evil", romaji: "Youjo Senki", native: "幼女戦記"),
        ]);

        var work = works.ShouldHaveSingleItem();
        work.IsSingle.ShouldBeTrue();

        var entry = work.MainStory.ShouldHaveSingleItem();
        entry.Name.ShouldBe("The Saga of Tanya the Evil");
        entry.Subtitle.ShouldBe("Youjo Senki · 幼女戦記");
    }

    /// <summary>A part's romaji and native titles carry the part too; the header wants the base.</summary>
    [Fact]
    public void Group_PartsOnly_TheHeaderNamesTheBaseInEveryScript()
    {
        var works = SearchResultGrouper.Group(
        [
            Hit("Ascendance of a Bookworm: Part 1",
                romaji: "Honzuki no Gekokujou: Dai 1-bu Heishi no Musume",
                native: "本好きの下剋上 ～第一部 兵士の娘～"),
            Hit("Ascendance of a Bookworm: Part 2"),
        ]);

        works.ShouldHaveSingleItem().AlternativeTitles.ShouldBe("Honzuki no Gekokujou · 本好きの下剋上");
    }

    [Fact]
    public void Group_NoResults_IsNoWorks() =>
        SearchResultGrouper.Group([]).ShouldBeEmpty();

    private SeriesSearchResultResponse Hit(
        string english,
        Format format = Format.LightNovel,
        string? romaji = null,
        string? native = null,
        int? year = 2015,
        int? volumes = null) => new()
        {
            Source = MetadataSourceName.AniList,
            AniListId = _nextId++,
            TitleEnglish = english,
            TitleRomaji = romaji,
            TitleNative = native,
            Format = format,
            PublicationStatus = PublicationStatus.Completed,
            VolumeCount = volumes,
            StartYear = year,
        };
}
