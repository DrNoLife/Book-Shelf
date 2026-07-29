using System.Net;
using System.Net.Http.Json;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The transport: one POST, one envelope, no GraphQL library. A typed client, so
/// the rate-limit handler and the base address are wired once at registration.
/// </summary>
public sealed class AniListGraphQlClient(HttpClient httpClient, ILogger<AniListGraphQlClient> logger)
{
    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way
    /// production does, instead of with their own second copy of these options.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <returns>
    /// Null when AniList answers 404 — for the detail query that means the id
    /// does not exist, which is a normal answer rather than a failure.
    /// </returns>
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
        // read as data rather than thrown on. Anything else that failed is a
        // transport problem and gets no attempt at parsing an error page.
        if (response.StatusCode is not HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }

        var envelope = await response.Content.ReadFromJsonAsync<AniListGraphQlResponse<TData>>(
            JsonOptions, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "AniList has nothing for {OperationName}: {Errors}",
                operationName,
                DescribeErrors(envelope));

            return null;
        }

        if (envelope?.Errors is { Count: > 0 })
        {
            // Not recoverable here and not swallowed: an AniList-side error means
            // the query or the service is wrong, and both need to be seen.
            throw new InvalidOperationException(
                $"AniList rejected {operationName}: {DescribeErrors(envelope)}");
        }

        return envelope?.Data;
    }

    private static string DescribeErrors<TData>(AniListGraphQlResponse<TData>? envelope)
        where TData : class =>
        envelope?.Errors is { Count: > 0 } errors
            ? string.Join("; ", errors.Select(error => error.Message ?? "(no message)"))
            : "(no errors reported)";
}
