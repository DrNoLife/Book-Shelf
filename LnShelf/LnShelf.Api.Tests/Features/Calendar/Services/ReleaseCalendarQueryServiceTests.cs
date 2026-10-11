using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Api.Features.Calendar.Models;
using LnShelf.Api.Features.Calendar.Services;
using LnShelf.Api.Tests.Features.Calendar.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Calendar.Services;

/// <summary>
/// What a subscribed calendar app gets: the token owner's dated volumes, and nobody else's.
/// </summary>
public sealed class ReleaseCalendarQueryServiceTests : IDisposable
{
    private static readonly DateOnly Today = CalendarHarness.Today;

    private readonly CalendarHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GetAsync_DatedVolumes_AreEventsFromSixtyDaysAgoOnwardOwnedOrNot()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            CalendarHarness.NewSeries(
                "Overlord",
                TrackingStatus.Reading,
                CalendarHarness.NewVolume(16, Today.AddDays(-ReleaseCalendarQueryService.PastDays - 1), owned: true),
                CalendarHarness.NewVolume(17, Today.AddDays(-ReleaseCalendarQueryService.PastDays), owned: true),
                CalendarHarness.NewVolume(18, Today.AddDays(30)),
                CalendarHarness.NewVolume(19, releaseDate: null)));
        var token = await _harness.TurnOnAsync(TestUsers.Owner);

        var document = await GetAsync(token);

        var summaries = Summaries(document);
        summaries.ShouldBe(["Overlord Vol 17", "Overlord Vol 18"]);
    }

    [Fact]
    public async Task GetAsync_DroppedSeries_IsLeftOut()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            CalendarHarness.NewSeries("Overlord", TrackingStatus.Dropped, CalendarHarness.NewVolume(18, Today.AddDays(30))),
            CalendarHarness.NewSeries("Mushoku Tensei", status: null, CalendarHarness.NewVolume(27, Today.AddDays(10))));
        var token = await _harness.TurnOnAsync(TestUsers.Owner);

        var document = await GetAsync(token);

        Summaries(document).ShouldBe(["Mushoku Tensei Vol 27"]);
    }

    // The anonymous request reads through the owner filters as the token's owner, so
    // another user's shelf can never leak into the feed.
    [Fact]
    public async Task GetAsync_TwoShelves_ReadsOnlyTheTokenOwners()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            CalendarHarness.NewSeries("Overlord", TrackingStatus.Reading, CalendarHarness.NewVolume(18, Today.AddDays(30))));
        await _harness.AddSeriesAsync(
            TestUsers.SomeoneElse,
            CalendarHarness.NewSeries("Mushoku Tensei", TrackingStatus.Reading, CalendarHarness.NewVolume(27, Today.AddDays(10))));
        await _harness.TurnOnAsync(TestUsers.Owner);
        var someoneElsesToken = await _harness.TurnOnAsync(TestUsers.SomeoneElse);

        var document = await GetAsync(someoneElsesToken);

        Summaries(document).ShouldBe(["Mushoku Tensei Vol 27"]);
    }

    [Fact]
    public async Task GetAsync_UnknownToken_IsNull()
    {
        await _harness.TurnOnAsync(TestUsers.Owner);

        (await GetAsync(CalendarToken.Create())).ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_TurnedOffToken_IsNull()
    {
        var token = await _harness.TurnOnAsync(TestUsers.Owner);
        await _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<ICalendarFeedCommandService>().SetOnAsync(false, CancellationToken.None));

        (await GetAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_SeveralSeries_OrdersEventsByDateThenTitle()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            CalendarHarness.NewSeries("Overlord", TrackingStatus.Reading,
                CalendarHarness.NewVolume(18, Today.AddDays(30)),
                CalendarHarness.NewVolume(17, Today.AddDays(5))),
            CalendarHarness.NewSeries("Mushoku Tensei", TrackingStatus.Reading,
                CalendarHarness.NewVolume(27, Today.AddDays(5))));
        var token = await _harness.TurnOnAsync(TestUsers.Owner);

        var document = await GetAsync(token);

        Summaries(document).ShouldBe(["Mushoku Tensei Vol 27", "Overlord Vol 17", "Overlord Vol 18"]);
    }

    private Task<string?> GetAsync(string token) =>
        _harness.AsAnonymousAsync(
            services => services.GetRequiredService<IReleaseCalendarQueryService>().GetAsync(token, CancellationToken.None));

    private static string[] Summaries(string? document) =>
        [.. document.ShouldNotBeNull()
            .Split("\r\n")
            .Where(line => line.StartsWith("SUMMARY:", StringComparison.Ordinal))
            .Select(line => line["SUMMARY:".Length..])];
}
