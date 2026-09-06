using System.Text.Json;

namespace LnShelf.Client;

/// <summary>
/// Reads the API's own account of a refusal off a failed response.
/// </summary>
// One home for what two state containers had written out separately, over two different
// mirrors of the same ProblemDetails shape. The shared contract (ApiProblemResponse) is
// the one mirror, and its Message already picks Detail over Title.
public static class ApiProblemReader
{
    extension(HttpResponseMessage response)
    {
        /// <summary>
        /// The API's own words for the failure, or null when it did not give any.
        /// </summary>
        // Best-effort by design: a proxy or a dev-server can answer a failure with HTML,
        // and a second failure while reading the first must not replace a bad status code
        // with a deserialization message nobody can act on.
        public async Task<string?> ReadProblemAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var problem = await response.Content
                    .ReadFromJsonAsync<ApiProblemResponse>(cancellationToken);

                return problem?.Message;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
                when (exception is HttpRequestException or JsonException or NotSupportedException)
            {
                return null;
            }
        }
    }
}
