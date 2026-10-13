namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One source's politeness settings: how fast it may be called, how many callers may queue
/// for it, and how long a 429 is honoured for.
/// </summary>
// Built from each source's own options rather than being a fifth options section. The four
// APIs have unrelated budgets and must keep their own numbers (CLAUDE.md — be a polite API
// citizen); what this record removes is the four copies of the *code* that applied them.
public sealed record SourceRateLimitPolicy
{
    /// <summary>The name that appears in the 429 warning and the queue-full message.</summary>
    public required string SourceName { get; init; }

    public required int RequestsPerMinute { get; init; }

    /// <summary>
    /// How deep the queue of waiting requests goes — what turns "too fast" into "slower"
    /// rather than into a failure.
    /// </summary>
    public required int QueuedRequestLimit { get; init; }

    /// <summary>
    /// Ceiling on an honoured <c>Retry-After</c>, and the wait when a 429 advertises nothing
    /// usable: a pathological value must not park a request for an hour.
    /// </summary>
    public required TimeSpan MaximumRetryDelay { get; init; }

    /// <summary>
    /// Bounds one request, separately from the wait for a permit.
    /// </summary>
    // Null when the HttpClient's own timeout is doing the job. Every registered source sets
    // it and leaves its client's timeout infinite — see PoliteRateLimitHandler for why the
    // two are not the same clock.
    public TimeSpan? RequestTimeout { get; init; }

    /// <summary>
    /// Whether this source answers a rate limit with 403 Forbidden rather than 429.
    /// </summary>
    // Apple's iTunes Search endpoint refuses an over-eager caller with a 403, not a 429, so for
    // that source a 403 is the same signal and gets the same single, polite retry. Opt-in per
    // source, because elsewhere a 403 means something else: AniList's 403 is "the API is
    // disabled", and the search fallback has to see it at once rather than after a minute's wait.
    public bool TreatsForbiddenAsRateLimited { get; init; }
}
