namespace LnShelf.Api.Features.Metadata.Models.AniList;

/// <summary>
/// AniList's <c>Media</c> node, restricted to the fields the shelf reads.
/// </summary>
// Dirty boundary model. Everything is nullable because everything genuinely is:
// this is community-maintained data, and a sparse entry may carry no English
// title, no volume count and no staff at all.
public sealed class AniListMedia
{
    public int Id { get; set; }

    /// <summary>NOVEL, MANGA or ONE_SHOT for the MANGA media type.</summary>
    public string? Format { get; set; }

    /// <summary>RELEASING, FINISHED, HIATUS, CANCELLED or NOT_YET_RELEASED.</summary>
    public string? Status { get; set; }

    /// <summary>The total volume count — not a volume list. Null while unknown.</summary>
    public int? Volumes { get; set; }

    /// <summary>
    /// The synopsis, requested with <c>asHtml: false</c> — which still carries
    /// <c>&lt;br&gt;</c>, <c>&lt;i&gt;</c> and HTML entities. Only the detail query asks
    /// for it; a search row leaves it null.
    /// </summary>
    public string? Description { get; set; }

    public AniListMediaTitle? Title { get; set; }
    public AniListFuzzyDate? StartDate { get; set; }
    public AniListCoverImage? CoverImage { get; set; }
    public List<string>? Genres { get; set; }
    public AniListStaffConnection? Staff { get; set; }

    /// <summary>Only the recommendations query asks for it; null everywhere else.</summary>
    public bool? IsAdult { get; set; }

    /// <summary>What readers also liked. Only the recommendations query asks for it.</summary>
    public AniListRecommendationConnection? Recommendations { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListCoverImage
{
    /// <summary>
    /// AniList's own field name; the URL it returns is a CDN path, which the
    /// cover cache downloads once and never hands to the browser.
    /// </summary>
    public string? Large { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListMediaTitle
{
    public string? English { get; set; }
    public string? Romaji { get; set; }
    public string? Native { get; set; }
}

/// <summary>
/// AniList dates are "fuzzy": any part may be missing. Only the year is read.
/// </summary>
public sealed class AniListFuzzyDate
{
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListStaffConnection
{
    public List<AniListStaffEdge>? Edges { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListStaffEdge
{
    /// <summary>
    /// Freeform community text: "Story ", "Story &amp; Art", "Illustration",
    /// "Translator (English: vols 2, 4, 6-8)".
    /// </summary>
    public string? Role { get; set; }

    public AniListStaffNode? Node { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListStaffNode
{
    public AniListStaffName? Name { get; set; }

    /// <summary>
    /// What else this person is credited on. Only the author-works query asks for it; every
    /// other query leaves it null.
    /// </summary>
    public AniListStaffMediaConnection? StaffMedia { get; set; }
}

/// <summary>A person's credits, most popular first, as <c>Staff.staffMedia</c> returns them.</summary>
public sealed class AniListStaffMediaConnection
{
    public List<AniListStaffMediaEdge>? Edges { get; set; }
}

/// <inheritdoc cref="AniListStaffMediaConnection"/>
public sealed class AniListStaffMediaEdge
{
    /// <summary>
    /// The person's role on <see cref="Node"/>, as freeform as <see cref="AniListStaffEdge.Role"/>:
    /// "Story ", "Original Story", "Illustration".
    /// </summary>
    public string? StaffRole { get; set; }

    public AniListMedia? Node { get; set; }
}

/// <inheritdoc cref="AniListMedia"/>
public sealed class AniListStaffName
{
    public string? Full { get; set; }
}
