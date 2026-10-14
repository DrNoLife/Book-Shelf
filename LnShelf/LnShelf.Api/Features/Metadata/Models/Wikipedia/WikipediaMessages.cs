namespace LnShelf.Api.Features.Metadata.Models.Wikipedia;

// Dirty boundary models: classes, settable members, permissive nullability (CLAUDE.md —
// Types). Read only by WikipediaClient, which hands clean values inward. Both wikis speak the
// same MediaWiki API, so one set of shapes covers Wikipedia and Wikidata.

/// <summary>What every MediaWiki answer may carry instead of a result: <c>maxlag</c>, <c>missingtitle</c>.</summary>
public sealed class MediaWikiError
{
    public string? Code { get; set; }

    public string? Info { get; set; }
}

/// <summary>The answer to <c>action=query&amp;list=search</c>, on either wiki.</summary>
public sealed class MediaWikiSearchResponse
{
    public MediaWikiError? Error { get; set; }

    public MediaWikiSearchQuery? Query { get; set; }
}

public sealed class MediaWikiSearchQuery
{
    public List<MediaWikiSearchHit>? Search { get; set; }
}

public sealed class MediaWikiSearchHit
{
    /// <summary>A page title on Wikipedia; an item id ("Q65240543") on Wikidata.</summary>
    public string? Title { get; set; }
}

/// <summary>The answer to <c>action=parse&amp;prop=wikitext&amp;formatversion=2</c>.</summary>
public sealed class WikipediaParseResponse
{
    public MediaWikiError? Error { get; set; }

    public WikipediaParsedPage? Parse { get; set; }
}

public sealed class WikipediaParsedPage
{
    /// <summary>The page's own title, after any redirect was followed.</summary>
    public string? Title { get; set; }

    /// <summary>A plain string under formatversion=2.</summary>
    public string? Wikitext { get; set; }
}

/// <summary>The answer to <c>action=wbgetclaims</c>, filtered to one property.</summary>
public sealed class WikidataClaimsResponse
{
    public MediaWikiError? Error { get; set; }

    public Dictionary<string, List<WikidataClaim>>? Claims { get; set; }
}

public sealed class WikidataClaim
{
    public WikidataSnak? MainSnak { get; set; }
}

public sealed class WikidataSnak
{
    public WikidataDataValue? DataValue { get; set; }
}

/// <summary>An item-valued claim's value; other datatypes leave <see cref="Value"/> unread.</summary>
public sealed class WikidataDataValue
{
    public string? Type { get; set; }

    // A JsonElement because the value's shape depends on Type: an object for an item, a bare
    // string for an external id. Only the item shape is read.
    public JsonElement Value { get; set; }
}

/// <summary>The answer to <c>action=wbgetentities&amp;props=sitelinks</c>.</summary>
public sealed class WikidataEntitiesResponse
{
    public MediaWikiError? Error { get; set; }

    public Dictionary<string, WikidataEntity>? Entities { get; set; }
}

public sealed class WikidataEntity
{
    public Dictionary<string, WikidataSitelink>? Sitelinks { get; set; }
}

public sealed class WikidataSitelink
{
    public string? Title { get; set; }
}
