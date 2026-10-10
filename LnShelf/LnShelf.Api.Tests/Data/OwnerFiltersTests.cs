using LnShelf.Api.Features.Sharing.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Data;

/// <summary>
/// Every table is either filtered by owner or on a short list that says why it is not
/// (feature-spec §7). LnShelfDbContext.ApplyOwnerFilters points here.
/// </summary>
// A new entity forces a decision: add a filter in ApplyOwnerFilters, or add it below with the
// reason. An entity that is neither is one a request could read across users without anyone
// having decided that it may.
public sealed class OwnerFiltersTests
{
    private static readonly Dictionary<Type, string> Unfiltered = new()
    {
        [typeof(User)] = "It is what the owner filters are about.",
        [typeof(ShelfShare)] = "How an anonymous request learns whom to act for (Features/Sharing).",
        [typeof(DataProtectionKey)] = "The session cookie's signing keys: the app's, not a user's.",
        [typeof(VolumeUserState)] = "Reached only through its volume, which is filtered through its series.",
        [typeof(ImportRow)] = "Reached only through its import group, which is filtered through its batch.",
        [typeof(ImportCandidate)] = "Reached only through its import row.",
        [typeof(SyncDiscrepancy)] = "Reached only through its sync run, which is filtered.",
        [typeof(SyncDiscrepancyValue)] = "Reached only through its discrepancy.",
    };

    [Fact]
    public void EveryEntityType_IsOwnedFilteredThroughARootOrOnTheUnfilteredList()
    {
        using var dbContext = Context();

        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var isFiltered = entityType.FindDeclaredQueryFilter(LnShelfDbContext.OwnerFilter) is not null;
            var isListed = Unfiltered.ContainsKey(entityType.ClrType);

            (isFiltered || isListed).ShouldBeTrue(
                $"{entityType.ClrType.Name} has no owner filter and is not on the unfiltered list. Add a filter "
                + "in LnShelfDbContext.ApplyOwnerFilters, or list it here with the reason it may be read across users.");

            (isFiltered && isListed).ShouldBeFalse(
                $"{entityType.ClrType.Name} is filtered and also listed as unfiltered; take it off the list.");
        }
    }

    // InMemory cannot run the ExecuteDelete account deletion uses, so the cascade that takes a
    // deleted account's link with it is asserted on the model.
    [Fact]
    public void ShelfShare_ForeignKeyToUser_CascadesOnDelete()
    {
        using var dbContext = Context();

        var foreignKey = dbContext.Model.FindEntityType(typeof(ShelfShare)).ShouldNotBeNull()
            .GetForeignKeys().ShouldHaveSingleItem();

        foreignKey.PrincipalEntityType.ClrType.ShouldBe(typeof(User));
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    // A choice is the user's data about one series and means nothing without it.
    [Fact]
    public void SourceChoice_ForeignKeyToSeries_CascadesOnDelete()
    {
        using var dbContext = Context();

        var foreignKey = dbContext.Model.FindEntityType(typeof(SourceChoice)).ShouldNotBeNull()
            .GetForeignKeys().ShouldHaveSingleItem();

        foreignKey.PrincipalEntityType.ClrType.ShouldBe(typeof(Series));
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    private static LnShelfDbContext Context() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"owner-filters-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
