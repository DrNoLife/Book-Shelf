namespace LnShelf.Shared;

/// <summary>
/// The API's problem payload, as the client reads it: RFC 9457, restricted to the two
/// fields worth showing a person.
/// </summary>
// The server writes the framework's own ProblemDetails (see ProblemDetailsExceptionHandler)
// and this is the mirror the WASM bundle deserializes, because that framework type is not in
// the client's package set — the browser boundary is not an ASP.NET Core one.
//
// A class rather than a record, and permissively nullable, because it is a wire shape being
// read at a boundary (CLAUDE.md — Types): a failing API is exactly the case where the body
// may not be what the contract says.
public sealed class ApiProblemResponse
{
    /// <summary>The kind of failure — "AniList is unavailable".</summary>
    public string? Title { get; set; }

    /// <summary>
    /// The specific one, in the words of whoever knew it — for an unavailable source,
    /// that source's own message.
    /// </summary>
    public string? Detail { get; set; }

    public int? Status { get; set; }

    /// <summary>
    /// <see cref="Detail"/> when there is one, otherwise <see cref="Title"/>, otherwise null.
    /// </summary>
    // One line, because that is what a notice bar has room for, and the detail is the half
    // that says what to do about it.
    public string? Message =>
        FirstNonBlank(Detail) ?? FirstNonBlank(Title);

    private static string? FirstNonBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
