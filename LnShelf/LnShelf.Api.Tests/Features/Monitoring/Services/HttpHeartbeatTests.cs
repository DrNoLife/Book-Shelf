using System.Net;
using LnShelf.Api.Features.Monitoring.Services;
using Microsoft.Extensions.Configuration;

namespace LnShelf.Api.Tests.Features.Monitoring.Services;

/// <summary>
/// The heartbeat sends the right URL when a monitor is configured, nothing when it is not,
/// and never lets a monitoring outage fail the job that beat.
/// </summary>
public sealed class HttpHeartbeatTests
{
    private static readonly Uri PushUrl = new("http://kuma.local:3001/api/push/token123");

    public static TheoryData<string, HeartbeatOutcome, string, string> PushUris => new()
    {
        { "http://kuma.local:3001/api/push/token123", HeartbeatOutcome.Up, "2 shelves synced, 0 failed",
            "http://kuma.local:3001/api/push/token123?status=up&msg=2%20shelves%20synced%2C%200%20failed" },

        // The sample query Uptime Kuma shows with the URL is replaced, not appended to.
        { "http://kuma.local:3001/api/push/token123?status=up&msg=OK&ping=", HeartbeatOutcome.Down, "failed",
            "http://kuma.local:3001/api/push/token123?status=down&msg=failed" },
    };

    [Theory]
    [MemberData(nameof(PushUris))]
    public void PushUri_ConfiguredUrl_CarriesStatusAndMessage(string configured, HeartbeatOutcome outcome, string message, string expected) =>
        HttpHeartbeat.PushUri(new Uri(configured), outcome, message).AbsoluteUri.ShouldBe(expected);

    [Fact]
    public async Task BeatAsync_ConfiguredMonitor_CallsItsPushUrl()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Heartbeat(handler, configured: true)
            .BeatAsync(HeartbeatMonitors.NightlySync, HeartbeatOutcome.Up, "ok", CancellationToken.None);

        handler.Requests.ShouldHaveSingleItem().AbsoluteUri.ShouldStartWith(PushUrl.AbsoluteUri + "?status=up");
    }

    [Fact]
    public async Task BeatAsync_MonitorNotConfigured_SendsNothing()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Heartbeat(handler, configured: false)
            .BeatAsync(HeartbeatMonitors.NightlySync, HeartbeatOutcome.Up, "ok", CancellationToken.None);

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task BeatAsync_MonitorConfiguredEmpty_SendsNothing()
    {
        // What compose hands the API when HEARTBEAT_NIGHTLY_SYNC_URL is not in the .env.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Heartbeat:Monitors:NightlySync"] = "" })
            .Build();
        var options = new HeartbeatOptions();
        configuration.GetSection("Heartbeat").Bind(options);
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await new HttpHeartbeat(new HttpClient(handler), Options.Create(options), NullLogger<HttpHeartbeat>.Instance)
            .BeatAsync(HeartbeatMonitors.NightlySync, HeartbeatOutcome.Up, "ok", CancellationToken.None);

        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task BeatAsync_MonitorAnswersAnError_DoesNotThrow(HttpStatusCode status)
    {
        await Should.NotThrowAsync(() => Heartbeat(new RecordingHandler(status), configured: true)
            .BeatAsync(HeartbeatMonitors.NightlySync, HeartbeatOutcome.Up, "ok", CancellationToken.None));
    }

    [Fact]
    public async Task BeatAsync_MonitorUnreachable_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => Heartbeat(new RecordingHandler(failWith: new HttpRequestException("refused")), configured: true)
            .BeatAsync(HeartbeatMonitors.NightlySync, HeartbeatOutcome.Up, "ok", CancellationToken.None));
    }

    private static HttpHeartbeat Heartbeat(RecordingHandler handler, bool configured)
    {
        var options = new HeartbeatOptions();
        if (configured)
        {
            options.Monitors[HeartbeatMonitors.NightlySync] = PushUrl;
        }

        return new HttpHeartbeat(new HttpClient(handler), Options.Create(options), NullLogger<HttpHeartbeat>.Instance);
    }

    private sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK, Exception? failWith = null) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            return failWith is null
                ? Task.FromResult(new HttpResponseMessage(status))
                : Task.FromException<HttpResponseMessage>(failWith);
        }
    }
}
