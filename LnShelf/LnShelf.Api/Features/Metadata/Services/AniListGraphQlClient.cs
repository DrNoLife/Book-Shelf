using System.Net;
using System.Net.Http.Json;
using LnShelf.Api.Errors;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The transport: one POST, one envelope, no GraphQL library. A typed client, so
/// the rate-limit handler and the base address are wired once at registration.
/// </summary>
public sealed class AniListGraphQlClient(HttpClient httpClient, ILogger<AniListGraphQlClient> logger)
{
    /// <summary>The source name this client speaks for, in logs and problem titles.</summary>
    private static readonly string SourceName = nameof(MetadataSourceName.AniList);

    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way
    /// production does, instead of with their own second copy of these options.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <returns>
    /// Null when AniList answers 404 — for the detail query that means the id
    /// does not exist, which is a normal answer rather than a failure.
    /// </returns>
    /// <exception cref="MetadataSourceUnavailableException">
    /// AniList answered with a failure status other than 404 or 429 — it is refusing to
    /// serve, and the caller is entitled to know that rather than to see a bare 500.
    /// </exception>
    public async Task<TData?> ExecuteAsync<TData>(
        string operationName,
        string query,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken)
        where TData : class
    {
        var request = new AniListGraphQlRequest { Query = query, Variables = variables };

        using var response = await httpClient.PostAsJsonAsync(
            string.Empty, request, JsonOptions, cancellationToken);

        // A missing id comes back as 404 *with* a GraphQL body, so that status is
        // read as data rather than thrown on.
        if (response.StatusCode is not HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
        {
            // 429 keeps the transport exception it always threw. The rate-limit handler
            // owns throttling — it has already waited and retried once by the time this
            // runs — and "slow down" is not the same claim as "we are not serving". The
            // distinction is load-bearing: only the second one may send the search to
            // another source (MetadataSourceFailure).
            if (response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                response.EnsureSuccessStatusCode();
            }

            throw await ToUnavailableAsync(operationName, response, cancellationToken);
        }

        var envelope = await response.Content.ReadFromJsonAsync<AniListGraphQlResponse<TData>>(
            JsonOptions, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "AniList has nothing for {OperationName}: {Errors}",
                operationName,
                DescribeErrors(envelope?.Errors));

            return null;
        }

        if (envelope?.Errors is { Count: > 0 } errors)
        {
            // Not recoverable here and not swallowed: a GraphQL-level error on a 200
            // means the query is wrong, which is this app's defect and not an outage.
            throw new InvalidOperationException(
                $"AniList rejected {operationName}: {DescribeErrors(errors)}");
        }

        return envelope?.Data;
    }

    /// <summary>
    /// Reads the refusal AniList sent and turns it into the typed error, message and all.
    /// </summary>
    // The body is read on purpose rather than discarded by EnsureSuccessStatusCode. AniList's
    // outage response is a perfectly good GraphQL error envelope — "The AniList API has been
    // temporarily disabled due to severe stability issues" — and that sentence is the entire
    // useful content of the failure. Throwing it away is what made the shelf say
    // "net_http_message_not_success_statuscode_reason".
    private async Task<MetadataSourceUnavailableException> ToUnavailableAsync(
        string operationName,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var upstreamMessage = await ReadUpstreamMessageAsync(response, cancellationToken);

        logger.LogWarning(
            "AniList refused {OperationName} with {StatusCode}: {UpstreamMessage}",
            operationName,
            statusCode,
            upstreamMessage);

        return new MetadataSourceUnavailableException(SourceName, upstreamMessage, statusCode);
    }

    /// <summary>
    /// The first error message in the body, or the status line when the body is not one.
    /// </summary>
    // Parsing is best-effort by design: an outage often comes back as a proxy's HTML error
    // page, and failing to read the failure would be a poor reason to lose it entirely.
    private static async Task<string> ReadUpstreamMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fallback = response.ReasonPhrase is { Length: > 0 } reason
            ? reason
            : $"HTTP {(int)response.StatusCode}";

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
            {
                return fallback;
            }

            var envelope = JsonSerializer.Deserialize<AniListGraphQlErrorEnvelope>(body, JsonOptions);

            return envelope?.Errors is { Count: > 0 } errors ? DescribeErrors(errors) : fallback;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string DescribeErrors(List<AniListGraphQlError>? errors) =>
        errors is { Count: > 0 }
            ? string.Join("; ", errors.Select(error => error.Message ?? "(no message)"))
            : "(no errors reported)";
}
