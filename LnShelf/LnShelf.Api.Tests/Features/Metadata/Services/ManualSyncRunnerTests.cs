using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// "Sync now" in the background: started once per user at a time, run as that user, and
/// never left holding the user's place after it ends.
/// </summary>
public sealed class ManualSyncRunnerTests
{
    private static readonly Guid User = new("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public void TryStart_TwiceForTheSameUser_RefusesTheSecond()
    {
        var gate = new SyncRunGate();
        var runner = new ManualSyncRunner(
            Substitute.For<IServiceScopeFactory>(), gate, NullLogger<ManualSyncRunner>.Instance);

        // Not started as a hosted service, so the first stays queued — which is exactly the
        // window a double-click lands in.
        runner.TryStart(User).ShouldBeTrue();
        runner.TryStart(User).ShouldBeFalse();
    }

    [Fact]
    public void TryStart_WhileTheNightlyRunHoldsTheUser_IsRefused()
    {
        var gate = new SyncRunGate();
        var runner = new ManualSyncRunner(
            Substitute.For<IServiceScopeFactory>(), gate, NullLogger<ManualSyncRunner>.Instance);

        using var nightly = gate.TryEnter(User);

        runner.TryStart(User).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_AStartedRun_RunsAsTheUserAndReleasesTheirPlace(bool runFails)
    {
        await using var host = new BackgroundSyncTestHost(failingUserId: runFails ? User : null);
        var runner = new ManualSyncRunner(host.ScopeFactory, host.Gate, NullLogger<ManualSyncRunner>.Instance);

        await runner.StartAsync(CancellationToken.None);
        runner.TryStart(User).ShouldBeTrue();

        // Released when the run ends, whether it finished or fell over: a failed run that
        // kept the lease would refuse every later "Sync now" until a restart.
        await WaitUntilAsync(() => host.RanFor.Contains(User));
        await WaitUntilAsync(() =>
        {
            using var lease = host.Gate.TryEnter(User);
            return lease is not null;
        });

        await runner.StopAsync(CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The background run did not finish in time.");
            }

            await Task.Delay(20);
        }
    }
}
