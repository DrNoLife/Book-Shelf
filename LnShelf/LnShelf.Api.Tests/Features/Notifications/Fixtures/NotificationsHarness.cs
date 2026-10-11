using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using LnShelf.Api.Features.Notifications.Models;
using LnShelf.Api.Features.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LnShelf.Api.Tests.Features.Notifications.Fixtures;

/// <summary>
/// One in-memory shelf, real VAPID keys, and a push service that answers each endpoint with
/// whatever status the test chose. The encryption runs for real; only the network is fake.
/// </summary>
internal sealed class NotificationsHarness : IDisposable
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"notifications-{Guid.CreateVersion7()}";

    public NotificationsHarness(bool webPushConfigured = true)
    {
        var keys = VapidKeys.Create();
        Authentication = new WebPushAuthentication(Options.Create(webPushConfigured
            ? new WebPushOptions { PublicKey = keys.PublicKey, PrivateKey = keys.PrivateKey, Subject = "mailto:owner@example.com" }
            : new WebPushOptions()));
    }

    public WebPushAuthentication Authentication { get; }

    public FakePushService PushService { get; } = new();

    public TimeProvider Time { get; } = new FixedTime(new DateTimeOffset(2026, 10, 11, 4, 30, 0, TimeSpan.Zero));

    public LnShelfDbContext CreateDbContext(Guid? userId = null) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot)
            .Options, TestUsers.As(userId ?? TestUsers.Owner));

    public WebPushReleaseNotifier CreateNotifier(LnShelfDbContext dbContext) =>
        new(dbContext,
            new Lib.Net.Http.WebPush.PushServiceClient(new HttpClient(PushService)) { AutoRetryAfter = false },
            Authentication,
            Time,
            NullLogger<WebPushReleaseNotifier>.Instance);

    /// <summary>A device as a browser would subscribe it: a real P-256 key and a 16-byte secret.</summary>
    public static PushSubscription Device(string endpoint, string? label = "Chrome on Android")
    {
        using var browserKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = browserKey.ExportParameters(includePrivateParameters: false).Q;

        return new PushSubscription
        {
            Id = Guid.CreateVersion7(),
            Endpoint = endpoint,
            P256dh = Base64Url.EncodeToString([0x04, .. point.X!, .. point.Y!]),
            Auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(VapidKeys.AuthenticationSecretLength)),
            DeviceLabel = label,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
    }

    public void Dispose() => Authentication.Dispose();

    /// <summary>Answers 201 unless told otherwise per endpoint, and records what it was sent.</summary>
    internal sealed class FakePushService : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpStatusCode> _answers = new(StringComparer.Ordinal);

        public List<HttpRequestMessage> Requests { get; } = [];

        private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);

        public void Answer(string endpoint, HttpStatusCode status) => _answers[endpoint] = status;

        /// <summary>No answer at all for this endpoint: the network, or the client's timeout.</summary>
        public void Fail(string endpoint, Exception exception) => _failures[endpoint] = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (_failures.TryGetValue(request.RequestUri!.ToString(), out var failure))
            {
                return Task.FromException<HttpResponseMessage>(failure);
            }

            var status = _answers.GetValueOrDefault(request.RequestUri!.ToString(), HttpStatusCode.Created);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(string.Empty) });
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
