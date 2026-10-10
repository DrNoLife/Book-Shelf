using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LnShelf.Api.Tests.Data.Fixtures;

/// <summary>
/// Makes a save fail the way Postgres would, for the in-memory provider, which enforces no
/// constraints of its own.
/// </summary>
/// <param name="shouldRefuse">Asked before each save; true throws instead of saving.</param>
/// <param name="exception">What the refused save throws.</param>
internal sealed class RefusingSaveInterceptor(
    Func<DbContext, bool> shouldRefuse,
    Func<DbUpdateException> exception) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && shouldRefuse(context))
        {
            throw exception();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>What Npgsql surfaces for a duplicate key, wrapped the way EF Core wraps it.</summary>
    public static DbUpdateException UniqueViolation() =>
        new(
            "An error occurred while saving the entity changes.",
            new Npgsql.PostgresException(
                "duplicate key value violates unique constraint",
                "ERROR",
                "ERROR",
                Npgsql.PostgresErrorCodes.UniqueViolation));
}
