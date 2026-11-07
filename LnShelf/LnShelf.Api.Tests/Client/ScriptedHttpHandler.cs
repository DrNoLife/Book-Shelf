using System.Net;
using System.Net.Http.Json;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Answers a client state container's calls from a script, and can hold any one of them until
/// the test lets it answer — which is how a test puts two calls in flight in a chosen order.
/// </summary>
// The state containers race their own calls (a write against a re-read, one search against
// the next), and those races are what these tests pin. A held request is matched by method and
// path start; it holds once, so a later call to the same route answers at once.
internal sealed class ScriptedHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    private readonly Lock _gate = new();
    private readonly List<HeldRequest> _held = [];
    private readonly List<string> _requests = [];

    /// <summary>Every call so far, as "PUT /api/volumes/…".</summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>A canned JSON answer, serialized the way the API serializes it.</summary>
    public static HttpResponseMessage Json<T>(T body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    /// <summary>Holds the next call whose method and path match, until it is released.</summary>
    public HeldRequest Hold(HttpMethod method, string pathStart)
    {
        var held = new HeldRequest(method, pathStart);

        lock (_gate)
        {
            _held.Add(held);
        }

        return held;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri.ShouldNotBeNull().AbsolutePath;
        HeldRequest? held;

        lock (_gate)
        {
            _requests.Add($"{request.Method} {path}");
            held = _held.FirstOrDefault(candidate => candidate.Matches(request.Method, path));
            if (held is not null)
            {
                _held.Remove(held);
            }
        }

        // Off any synchronization context, like the network: a released call answers on its own
        // thread, and only the state container's await resumes on the caller's (BrowserTurns).
        if (held is not null)
        {
            await held.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return answer(request);
    }

    /// <summary>One call held on its way to the answer.</summary>
    public sealed class HeldRequest(HttpMethod method, string pathStart)
    {
        private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when the call has been made and is being held.</summary>
        public Task Arrived => _arrived.Task;

        /// <summary>Lets the call answer.</summary>
        public void Release() => _released.TrySetResult();

        internal bool Matches(HttpMethod requestMethod, string path) =>
            requestMethod == method && path.StartsWith(pathStart, StringComparison.Ordinal);

        internal async Task WaitAsync(CancellationToken cancellationToken)
        {
            _arrived.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
