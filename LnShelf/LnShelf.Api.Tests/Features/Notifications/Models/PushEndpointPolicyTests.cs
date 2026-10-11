using LnShelf.Api.Features.Notifications.Models;

namespace LnShelf.Api.Tests.Features.Notifications.Models;

/// <summary>
/// The SSRF guard: the server only ever POSTs to a browser push service over https, never to
/// an address a signed-in user made up.
/// </summary>
public sealed class PushEndpointPolicyTests
{
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/dQw4w9WgXcQ:APA91b")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAAAABk")]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=BQYAAAB")]
    [InlineData("https://web.push.apple.com/QGuQyavXutnMH9Fq")]
    public void IsAllowed_AKnownPushService_IsAllowed(string endpoint) =>
        PushEndpointPolicy.IsAllowed(endpoint).ShouldBeTrue();

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc")]
    [InlineData("https://user:secret@fcm.googleapis.com/fcm/send/abc")]
    [InlineData("http://api:8080/api/sync")]
    [InlineData("https://192.168.20.224:3001/api/push/token")]
    [InlineData("https://evilfcm.googleapis.com.example.com/x")]
    [InlineData("https://push.apple.com.example.com/x")]
    [InlineData("https://example.com/web.push.apple.com")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAllowed_AnythingElse_IsRefused(string? endpoint) =>
        PushEndpointPolicy.IsAllowed(endpoint).ShouldBeFalse();

    [Fact]
    public void IsAllowed_AnEndpointLongerThanTheColumn_IsRefused() =>
        PushEndpointPolicy.IsAllowed("https://fcm.googleapis.com/" + new string('a', PushSubscription.MaximumEndpointLength))
            .ShouldBeFalse();
}
