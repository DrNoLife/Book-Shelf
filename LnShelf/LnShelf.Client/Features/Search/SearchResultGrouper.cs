namespace LnShelf.Client.Features.Search;

/// <summary>
/// Gathers a flat list of search hits into works: every series whose titles share a base
/// title, split into the main story (the base title and its numbered parts) and the side
/// stories (everything else under it).
/// </summary>
// Pure and static so the rules can be tested without a browser. A source answers "Bookworm"
// with twelve rows that are one work, and "Mushoku" with the light novel, its manga, three
// anthologies and two unrelated series; the page reads better as one card per work.
//
// The rules, in order:
// 1. Two hits are one work when they share a work key — the first segment of the English or
//    the romaji stem ("Mushoku Tensei" in "Mushoku Tensei: Roxy Gets Serious"). Romaji counts
//    too because a light novel listed only in romaji is still the manga's work.
// 2. The main story's stem is the stem most of the work's titles start with: the fewest
//    segments wins a tie, then the earliest hit. Nobody sharing a stem means no evident main
//    story, and every series is listed as one.
// 3. A hit is main story when its stem is that stem (in English, or in the romaji its main
//    entries carry); everything else is a side story.
public static class SearchResultGrouper
{
    public static IReadOnlyList<SearchWork> Group(IReadOnlyList<SeriesSearchResultResponse> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var hits = results.Select((result, index) => new Hit(result, index)).ToList();

        return [.. GatherWorks(hits).Select(BuildWork)];
    }

    /// <summary>Hits sharing a work key, joined transitively, in the order the source ranked them.</summary>
    private static IEnumerable<List<Hit>> GatherWorks(List<Hit> hits)
    {
        List<WorkDraft> drafts = [];

        foreach (var hit in hits)
        {
            var matching = drafts.Where(draft => draft.Keys.Overlaps(hit.WorkKeys)).ToList();
            var target = matching.FirstOrDefault();

            if (target is null)
            {
                target = new WorkDraft();
                drafts.Add(target);
            }

            // A hit whose English title names one work and whose romaji names another is
            // the evidence that the two are the same work, so they become one.
            foreach (var other in matching.Skip(1))
            {
                target.Hits.AddRange(other.Hits);
                target.Keys.UnionWith(other.Keys);
                drafts.Remove(other);
            }

            target.Hits.Add(hit);
            target.Keys.UnionWith(hit.WorkKeys);
        }

        // Drafts are created in ranking order and a merge always lands in the earlier one, so
        // the works stay in the order of their best-ranked hit.
        return drafts.Select(draft => draft.Hits.OrderBy(hit => hit.Index).ToList());
    }

    private static SearchWork BuildWork(List<Hit> hits)
    {
        if (hits.Count == 1)
        {
            return SingleWork(hits[0]);
        }

        var mainStem = ChooseMainStem(hits);
        var titleHit = mainStem is null ? hits[0] : hits.First(hit => SameSegments(hit.English.Stem, mainStem));

        // The romaji of the main entries names the main story too: a hit listed only in romaji
        // ("Mushoku Tensei: Isekai Ittara Honki Dasu") is the same series as the English one.
        List<IReadOnlyList<string>> mainStems = mainStem is null
            ? []
            : [mainStem, .. hits.Where(hit => SameSegments(hit.English.Stem, mainStem) && hit.Romaji is { Stem.Count: > 0 })
                                .Select(hit => hit.Romaji!.Stem)];

        bool IsMain(Hit hit) =>
            mainStem is null
            || mainStems.Any(stem => SameSegments(hit.English.Stem, stem) || (hit.Romaji is { } romaji && SameSegments(romaji.Stem, stem)));

        var mainHits = hits.Where(IsMain).ToList();
        var representative = mainHits.FirstOrDefault(hit => hit.English.PartNumber is null) ?? mainHits[0];

        // What a side story's name is cut from: the longest of these its title starts with.
        List<IReadOnlyList<string>> prefixes = [.. mainStems, [.. titleHit.English.Stem.Take(1)]];
        if (titleHit.Romaji is { Stem.Count: > 0 } titleRomaji)
        {
            prefixes.Add([.. titleRomaji.Stem.Take(1)]);
        }

        var title = titleHit.English.StemText;

        return new SearchWork
        {
            Title = title,
            AlternativeTitles = AlternativeTitlesOf(representative, title),
            MainStory =
            [
                .. mainHits
                    .OrderBy(hit => hit.Result.Format)
                    .ThenBy(hit => hit.English.PartNumber ?? 0)
                    .ThenBy(hit => hit.Index)
                    .Select(hit => MainEntry(hit, namedByPart: mainStem is not null, workTitle: mainStem is null ? null : title)),
            ],
            SideStories =
            [
                .. hits.Where(hit => !IsMain(hit))
                    .OrderBy(hit => hit.Result.StartYear ?? int.MaxValue)
                    .ThenBy(hit => hit.Index)
                    .Select(hit => SideEntry(hit, prefixes)),
            ],
        };
    }

    private static SearchWork SingleWork(Hit hit) => new()
    {
        Title = hit.Result.TitleEnglish,
        AlternativeTitles = OtherScripts(hit, hit.Result.TitleEnglish),
        MainStory = [MainEntry(hit, namedByPart: false, workTitle: null)],
        SideStories = [],
    };

    /// <returns>Null when no stem is shared by two or more titles.</returns>
    private static IReadOnlyList<string>? ChooseMainStem(List<Hit> hits)
    {
        var best = hits
            .Where(hit => hit.English.Stem.Count > 0)
            .Select(hit => new
            {
                hit.English.Stem,
                Coverage = hits.Count(other => other.English.StemStartsWith(hit.English.Stem)),
                hit.Index,
            })
            .OrderByDescending(candidate => candidate.Coverage)
            .ThenBy(candidate => candidate.Stem.Count)
            .ThenBy(candidate => candidate.Index)
            .FirstOrDefault();

        return best is { Coverage: > 1 } ? best.Stem : null;
    }

    /// <param name="namedByPart">
    /// True when the work has a main story the part numbers count through; "Part 2" alone
    /// means nothing on a card that does not.
    /// </param>
    /// <param name="workTitle">
    /// The card's title, when the card has a header; an entry whose title is exactly that is
    /// named by its format instead of repeating it.
    /// </param>
    private static SearchWorkEntry MainEntry(Hit hit, bool namedByPart, string? workTitle)
    {
        if (namedByPart && hit.English.PartLabel is { } partLabel)
        {
            return new SearchWorkEntry
            {
                Result = hit.Result,
                Name = partLabel,
                Subtitle = hit.English.AfterPart ?? hit.Romaji?.AfterPart,
                PartNumber = hit.English.PartNumber,
            };
        }

        var isTheWorkTitle = workTitle is not null
                             && SearchTitleNormalizer.Normalize(hit.Result.TitleEnglish) == SearchTitleNormalizer.Normalize(workTitle);

        return new SearchWorkEntry
        {
            Result = hit.Result,
            Name = isTheWorkTitle ? SearchWorkText.LongName(hit.Result.Format) : hit.Result.TitleEnglish,
            Subtitle = OtherScripts(hit, hit.Result.TitleEnglish),
            PartNumber = hit.English.PartNumber,
            IsNamedByFormat = isTheWorkTitle,
        };
    }

    private static SearchWorkEntry SideEntry(Hit hit, List<IReadOnlyList<string>> prefixes)
    {
        var name = RemainderOf(hit.English, prefixes);

        // The romaji line may also drop its own first segment: a side story listed as
        // "Honzuki no Gekokujou: Tanpenshuu" under an English work title is "Tanpenshuu".
        var romajiRest = hit.Romaji is { } romaji
            ? RemainderOf(romaji, [.. prefixes, [.. romaji.Segments.Take(1)]])
            : null;

        return new SearchWorkEntry
        {
            Result = hit.Result,
            Name = name,
            Subtitle = FirstDifferentFrom(name, romajiRest, hit.Result.TitleNative),
            PartNumber = hit.English.PartNumber,
        };
    }

    /// <summary>The title after the longest prefix it starts with, or the whole title if none.</summary>
    private static string RemainderOf(SearchTitle title, List<IReadOnlyList<string>> prefixes)
    {
        var longest = prefixes
            .Where(prefix => prefix.Count < title.Segments.Count && title.StartsWith(prefix))
            .MaxBy(prefix => prefix.Count);

        var remainder = longest is null ? string.Empty : title.RemainderAfter(longest.Count);

        return remainder.Length > 0 ? remainder : title.Original;
    }

    /// <summary>"Honzuki no Gekokujou · 本好きの下剋上", without anything that repeats the title.</summary>
    private static string? AlternativeTitlesOf(Hit representative, string title)
    {
        // A part's romaji and native titles carry the part as well; the work header wants
        // the base title in each script, so a part entry contributes its stem.
        var hasPart = representative.English.PartNumber is not null;
        var native = representative.Result.TitleNative is { } nativeTitle && hasPart
            ? SearchTitle.Parse(nativeTitle).FirstSegmentText
            : representative.Result.TitleNative;

        return JoinDistinct(title, representative.Romaji?.StemText, native);
    }

    /// <summary>The first candidate that is not just the name again.</summary>
    private static string? FirstDifferentFrom(string name, params string?[] candidates)
    {
        var normalizedName = SearchTitleNormalizer.Normalize(name);

        return candidates.FirstOrDefault(candidate =>
            SearchTitleNormalizer.Normalize(candidate) is { Length: > 0 } normalized && normalized != normalizedName);
    }

    private static string? OtherScripts(Hit hit, string name) =>
        JoinDistinct(name, hit.Result.TitleRomaji, hit.Result.TitleNative);

    private static string? JoinDistinct(string title, params string?[] candidates)
    {
        HashSet<string> seen = [SearchTitleNormalizer.Normalize(title)];
        List<string> kept = [];

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(SearchTitleNormalizer.Normalize(candidate)))
            {
                continue;
            }

            kept.Add(candidate.Trim());
        }

        return kept.Count == 0 ? null : string.Join(" · ", kept);
    }

    private static bool SameSegments(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count > 0 && left.SequenceEqual(right);

    /// <summary>One hit with its titles parsed once.</summary>
    private sealed class Hit
    {
        public Hit(SeriesSearchResultResponse result, int index)
        {
            Result = result;
            Index = index;
            English = SearchTitle.Parse(result.TitleEnglish);
            Romaji = string.IsNullOrWhiteSpace(result.TitleRomaji) ? null : SearchTitle.Parse(result.TitleRomaji);
            WorkKeys = new HashSet<string>(new[] { English.WorkKey, Romaji?.WorkKey }.OfType<string>().Where(key => key.Length > 0));
        }

        public SeriesSearchResultResponse Result { get; }

        /// <summary>Its place in the source's ranking.</summary>
        public int Index { get; }

        public SearchTitle English { get; }

        public SearchTitle? Romaji { get; }

        public IReadOnlySet<string> WorkKeys { get; }
    }

    /// <summary>A work while it is being gathered: its hits and every key they bring.</summary>
    private sealed class WorkDraft
    {
        public List<Hit> Hits { get; } = [];

        public HashSet<string> Keys { get; } = [];
    }
}
