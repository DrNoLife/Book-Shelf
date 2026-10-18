using LnShelf.Api.Features.Calendar.Models;
using LnShelf.Api.Features.Notifications.Models;
using LnShelf.Api.Features.Recommendations.Models;
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
        [typeof(CommunityCover)] = "Published on purpose by a consenting user; every write is hand-scoped to the current user and reads go only by match key (Features/Covers).",
        [typeof(ShelfShare)] = "How an anonymous request learns whom to act for (Features/Sharing).",
        [typeof(CalendarFeed)] = "How a calendar app's anonymous fetch learns whom to read for (Features/Calendar).",
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

    // Two foreign keys, so outside the single-key theory below: the sharer's account and the
    // volume it was published from each take the row with them.
    [Fact]
    public void CommunityCover_ForeignKeys_CascadeFromTheUserAndTheVolume()
    {
        using var dbContext = Context();

        var foreignKeys = dbContext.Model.FindEntityType(typeof(CommunityCover)).ShouldNotBeNull().GetForeignKeys().ToList();

        foreignKeys.Select(foreignKey => foreignKey.PrincipalEntityType.ClrType)
            .ShouldBe([typeof(User), typeof(Volume)], ignoreOrder: true);
        foreignKeys.ShouldAllBe(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }

    // Each of these is deleted with its principal by the database, so account deletion
    // (ExecuteDelete on the user) and an older image that does not know the table still work.
    public static TheoryData<Type, Type> CascadingForeignKeys => new()
    {
        { typeof(SourceChoice), typeof(Series) },
        { typeof(ShelfEvent), typeof(Series) },
        { typeof(CalendarFeed), typeof(User) },
        { typeof(PushSubscription), typeof(User) },
        { typeof(RecommendationDismissal), typeof(User) },
        { typeof(CommunityCoverHide), typeof(User) },
    };

    [Theory]
    [MemberData(nameof(CascadingForeignKeys))]
    public void ForeignKeyToPrincipal_CascadesOnDelete(Type dependent, Type principal)
    {
        using var dbContext = Context();

        var foreignKey = dbContext.Model.FindEntityType(dependent).ShouldNotBeNull()
            .GetForeignKeys().ShouldHaveSingleItem();

        foreignKey.PrincipalEntityType.ClrType.ShouldBe(principal);
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    // The activity log is a child filtered through its series, like Volume: one user's sync
    // events never reach another user's "Since your last visit" or notification pass.
    [Fact]
    public async Task ShelfEvents_TwoUsersWithEvents_ReadsOnlyTheCurrentUsersOwn()
    {
        var database = $"owner-filters-{Guid.CreateVersion7()}";
        var ownerSeriesId = await SeedSeriesWithEventAsync(database, TestUsers.Owner);
        await SeedSeriesWithEventAsync(database, TestUsers.SomeoneElse);

        await using var dbContext = Context(database, TestUsers.Owner);
        var events = await dbContext.ShelfEvents.ToListAsync();

        events.ShouldHaveSingleItem().SeriesId.ShouldBe(ownerSeriesId);
    }

    [Fact]
    public async Task PushSubscriptions_TwoUsersOnOneEndpoint_ReadsOnlyTheCurrentUsersOwn()
    {
        var database = $"owner-filters-{Guid.CreateVersion7()}";
        foreach (var userId in new[] { TestUsers.Owner, TestUsers.SomeoneElse })
        {
            await using var seedContext = Context(database, userId);
            seedContext.PushSubscriptions.Add(new PushSubscription
            {
                Id = Guid.CreateVersion7(),
                Endpoint = "https://fcm.googleapis.com/fcm/send/shared-browser",
                P256dh = "key",
                Auth = "secret",
            });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Context(database, TestUsers.Owner);
        var subscriptions = await dbContext.PushSubscriptions.ToListAsync();

        subscriptions.ShouldHaveSingleItem().OwnerId.ShouldBe(TestUsers.Owner);
    }

    private static async Task<Guid> SeedSeriesWithEventAsync(string database, Guid userId)
    {
        await using var dbContext = Context(database, userId);
        var seriesId = Guid.CreateVersion7();
        dbContext.Series.Add(new Series { Id = seriesId, TitleEnglish = "Overlord" });
        dbContext.ShelfEvents.Add(new ShelfEvent
        {
            Id = Guid.CreateVersion7(),
            SeriesId = seriesId,
            Kind = ShelfEventKind.VolumeReleased,
            VolumeNumber = 17,
            OccurredAt = DateTimeOffset.UnixEpoch,
        });
        await dbContext.SaveChangesAsync();
        return seriesId;
    }

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));

    private static LnShelfDbContext Context() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"owner-filters-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
