namespace LnShelf.Api.Features.Metadata.Models.AniList;

/// <summary>
/// The GraphQL envelope AniList speaks.
/// </summary>
// Dirty boundary models, so: classes, settable members, permissive nullability,
// and the shapes the wire actually has (CLAUDE.md — Types: records vs classes).
// They are mapped to clean records in Mappers/ and never travel further inward.
public sealed class AniListGraphQlRequest
{
    public string Query { get; set; } = string.Empty;

    public Dictionary<string, object?> Variables { get; set; } = [];
}

/// <inheritdoc cref="AniListGraphQlRequest"/>
public sealed class AniListGraphQlResponse<TData>
    where TData : class
{
    public TData? Data { get; set; }

    /// <summary>
    /// Present alongside a 404 when an id does not exist, and alongside a 400
    /// when the query itself is wrong.
    /// </summary>
    public List<AniListGraphQlError>? Errors { get; set; }
}

/// <inheritdoc cref="AniListGraphQlRequest"/>
public sealed class AniListGraphQlError
{
    public string? Message { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// The errors half of the envelope on its own, for a response whose data half cannot
/// be typed.
/// </summary>
// A failed request is read with this rather than with AniListGraphQlResponse&lt;TData&gt;:
// when AniList answers 403 with `{"errors":[…],"data":null}` there is no TData to name,
// and the caller only wants the message anyway.
public sealed class AniListGraphQlErrorEnvelope
{
    public List<AniListGraphQlError>? Errors { get; set; }
}
