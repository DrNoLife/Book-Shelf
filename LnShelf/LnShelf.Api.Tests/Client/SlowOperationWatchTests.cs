using LnShelf.Client;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The watch behind "This can take a moment": slow only once the threshold has passed, and
/// never for an operation that ended first.
/// </summary>
public sealed class SlowOperationWatchTests
{
    [Fact]
    public async Task Start_StillRunningAtTheThreshold_BecomesSlowAndSaysSoOnce()
    {
        using var watch = new SlowOperationWatch(TimeSpan.FromMilliseconds(20));
        var calls = 0;
        var becameSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        watch.Start(() =>
        {
            calls++;
            becameSlow.SetResult();
        });

        watch.IsSlow.ShouldBeFalse();

        await becameSlow.Task.WaitAsync(TimeSpan.FromSeconds(5));

        watch.IsSlow.ShouldBeTrue();
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Stop_BeforeTheThreshold_NeverBecomesSlow()
    {
        using var watch = new SlowOperationWatch(TimeSpan.FromMilliseconds(50));
        var called = false;

        watch.Start(() => called = true);
        watch.Stop();

        await Task.Delay(TimeSpan.FromMilliseconds(150));

        watch.IsSlow.ShouldBeFalse();
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task Stop_AfterBecomingSlow_IsNoLongerSlow()
    {
        using var watch = new SlowOperationWatch(TimeSpan.FromMilliseconds(1));
        var becameSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        watch.Start(becameSlow.SetResult);
        await becameSlow.Task.WaitAsync(TimeSpan.FromSeconds(5));

        watch.Stop();

        watch.IsSlow.ShouldBeFalse();
    }
}
