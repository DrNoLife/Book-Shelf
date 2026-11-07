using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LnShelf.Api.Tests.Data.Postgres;

/// <summary>
/// Runs another request's write in the gap between this context's load and its first
/// save: the window a real race falls into, made deterministic.
/// </summary>
/// <param name="concurrentWrite">The other request, on its own context.</param>
internal sealed class BeforeFirstSaveInterceptor(Func<CancellationToken, Task> concurrentWrite) : SaveChangesInterceptor
{
    private bool _hasRun;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (!_hasRun)
        {
            _hasRun = true;
            await concurrentWrite(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
