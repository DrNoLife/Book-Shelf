using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Api.Features.Calendar.Models;
using LnShelf.Api.Tests.Features.Calendar.Fixtures;
using LnShelf.Shared.Calendar;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Calendar.Services;

/// <summary>
/// On, off and "New link": the row is the setting, so turning the calendar off kills the link
/// for good and turning it back on never revives an old one.
/// </summary>
public sealed class CalendarFeedCommandServiceTests : IDisposable
{
    private readonly CalendarHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GetAsync_NeverTurnedOn_IsOff()
    {
        var feed = await GetAsync();

        feed.ShouldBe(new CalendarFeedResponse { IsOn = false, Token = null });
    }

    [Fact]
    public async Task SetOnAsync_On_CreatesAWellFormedToken()
    {
        var token = await _harness.TurnOnAsync(TestUsers.Owner);

        CalendarToken.IsWellFormed(token).ShouldBeTrue();
        (await GetAsync()).ShouldBe(new CalendarFeedResponse { IsOn = true, Token = token });
    }

    // Idempotent: a second "on" keeps the link a calendar app is already subscribed to.
    [Fact]
    public async Task SetOnAsync_OnTwice_KeepsTheSameToken()
    {
        var first = await _harness.TurnOnAsync(TestUsers.Owner);

        var second = await SetOnAsync(isOn: true);

        second.Token.ShouldBe(first);
    }

    [Fact]
    public async Task SetOnAsync_OffThenOn_IssuesANewToken()
    {
        var first = await _harness.TurnOnAsync(TestUsers.Owner);
        (await SetOnAsync(isOn: false)).ShouldBe(new CalendarFeedResponse { IsOn = false, Token = null });

        var again = await SetOnAsync(isOn: true);

        again.Token.ShouldNotBeNull().ShouldNotBe(first);
    }

    [Fact]
    public async Task RegenerateAsync_WhileOn_ReplacesTheToken()
    {
        var first = await _harness.TurnOnAsync(TestUsers.Owner);

        var regenerated = await RegenerateAsync();

        regenerated.IsOn.ShouldBeTrue();
        regenerated.Token.ShouldNotBeNull().ShouldNotBe(first);
        (await GetAsync()).Token.ShouldBe(regenerated.Token);
    }

    [Fact]
    public async Task RegenerateAsync_WhileOff_ThrowsConflict()
    {
        await Should.ThrowAsync<ConflictException>(RegenerateAsync);
    }

    // The table is unfiltered, so this is the guard that one user's settings page never
    // shows another user's link.
    [Fact]
    public async Task GetAsync_SomeoneElseIsOn_IsStillOffForMe()
    {
        await _harness.TurnOnAsync(TestUsers.SomeoneElse);

        (await GetAsync()).IsOn.ShouldBeFalse();
    }

    private Task<CalendarFeedResponse> SetOnAsync(bool isOn) =>
        _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<ICalendarFeedCommandService>().SetOnAsync(isOn, CancellationToken.None));

    private Task<CalendarFeedResponse> RegenerateAsync() =>
        _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<ICalendarFeedCommandService>().RegenerateAsync(CancellationToken.None));

    private Task<CalendarFeedResponse> GetAsync() =>
        _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<ICalendarFeedQueryService>().GetAsync(CancellationToken.None));
}
