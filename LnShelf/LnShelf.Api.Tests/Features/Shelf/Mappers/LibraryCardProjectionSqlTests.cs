using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Mappers;

/// <summary>
/// The library grid is the hot path and must be one SQL statement (data-access.md).
/// </summary>
// The sibling tests compile the projection and run it in memory, which proves the rules
// but says nothing about translation — the in-memory provider evaluates anything. This
// one asks the real Npgsql provider to produce SQL for the same expression. No connection
// is opened: ToQueryString compiles the query and stops, and a subquery or a conditional
// EF cannot translate throws there rather than silently becoming client evaluation.
public sealed class LibraryCardProjectionSqlTests
{
    [Fact]
    public void ToCard_AgainstTheRealProvider_TranslatesToSqlWithNoClientEvaluation()
    {
        using var dbContext = CreatePostgresDbContext();

        var sql = dbContext.Series
            .AsNoTracking()
            .Select(LibraryCardProjection.ToCard(new DateOnly(2026, 7, 29)))
            .ToQueryString();

        // Compiling at all is most of the assertion: a projection EF cannot translate
        // throws here. The rest pins the card cover, the newest and most fragile part —
        // a correlated subquery ordered by volume number, falling back through COALESCE
        // to the series' own column.
        sql.ShouldNotBeNullOrWhiteSpace();
        sql.ShouldContain("COALESCE");
        sql.ShouldContain("CoverOverrideObjectKey");

        // The only descending order in the whole projection, so this is that subquery and
        // nothing else: highest volume number first, take one.
        sql.ShouldContain("DESC");
    }

    /// <summary>A context on the production provider that is never connected to.</summary>
    private static LnShelfDbContext CreatePostgresDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseNpgsql("Host=localhost;Database=lnshelf;Username=lnshelf;Password=none")
            .Options, TestUsers.Current);
}
