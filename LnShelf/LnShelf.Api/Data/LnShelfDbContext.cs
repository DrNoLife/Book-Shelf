using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace LnShelf.Api.Data;

/// <summary>
/// The single context. Entity configurations live in each slice's own
/// <c>Data/</c> folder and are discovered by assembly scan (data-access.md), so
/// this type stays thin and slices stay self-contained.
/// </summary>
public sealed class LnShelfDbContext(DbContextOptions<LnShelfDbContext> options, ICurrentUser currentUser)
    : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>The name of the per-user filter, for the few operator tools that lift it.</summary>
    public const string OwnerFilter = "Owner";

    public DbSet<Series> Series => Set<Series>();
    public DbSet<Volume> Volumes => Set<Volume>();
    public DbSet<SeriesUserState> SeriesUserStates => Set<SeriesUserState>();
    public DbSet<VolumeUserState> VolumeUserStates => Set<VolumeUserState>();

    /// <summary>The activity log sync writes (Features/Shelf); filtered through its series.</summary>
    public DbSet<ShelfEvent> ShelfEvents => Set<ShelfEvent>();

    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    public DbSet<User> Users => Set<User>();

    /// <summary>Public shelf links (Features/Sharing). Unfiltered on purpose, like Users.</summary>
    public DbSet<ShelfShare> ShelfShares => Set<ShelfShare>();

    /// <summary>Release calendar links (Features/Calendar). Unfiltered on purpose, like ShelfShares.</summary>
    public DbSet<CalendarFeed> CalendarFeeds => Set<CalendarFeed>();

    /// <summary>Uploaded covers users share on purpose (Features/Covers). Unfiltered, like ShelfShares.</summary>
    public DbSet<CommunityCover> CommunityCovers => Set<CommunityCover>();

    /// <summary>Shared covers a user chose to hide (Features/Covers).</summary>
    public DbSet<CommunityCoverHide> CommunityCoverHides => Set<CommunityCoverHide>();

    /// <summary>Devices with Web Push turned on (Features/Notifications).</summary>
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    /// <summary>"Not interested" (Features/Recommendations).</summary>
    public DbSet<RecommendationDismissal> RecommendationDismissals => Set<RecommendationDismissal>();

    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<SyncDiscrepancy> SyncDiscrepancies => Set<SyncDiscrepancy>();
    public DbSet<SyncDiscrepancyValue> SyncDiscrepancyValues => Set<SyncDiscrepancyValue>();

    public DbSet<SourceChoice> SourceChoices => Set<SourceChoice>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportGroup> ImportGroups => Set<ImportGroup>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportCandidate> ImportCandidates => Set<ImportCandidate>();

    /// <summary>The session cookie's signing keys (Features/Auth).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Read by the owner filters on every query, so it is evaluated per query, per context.
    private Guid? CurrentOwnerId => currentUser.IdOrNull;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LnShelfDbContext).Assembly);
        ApplyOwnerFilters(modelBuilder);
    }

    /// <summary>
    /// Every user's data is invisible to every other user (feature-spec §7).
    /// </summary>
    // Here rather than in the slices' configurations because a filter that reads the current
    // user has to close over this context. A scope with no user matches nothing: a forgotten
    // ActAs reads an empty shelf instead of everyone's. The seven roots carry OwnerId; the
    // children that request paths query by their own id are filtered through their root.
    // Children reached only through a root (volume user state, import rows and candidates,
    // discrepancies) are covered by the root's filter; OwnerFiltersTests holds that list.
    // ShelfShare and CalendarFeed are unfiltered on purpose (Features/Sharing, Features/Calendar):
    // each is how an anonymous request learns whom to act for, the same reason User is.
    // CommunityCover is unfiltered for another reason: its owner published it so that other
    // users can read it by match key (Features/Covers).
    private void ApplyOwnerFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Series>().HasQueryFilter(OwnerFilter, series => series.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<ImportBatch>().HasQueryFilter(OwnerFilter, batch => batch.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<SyncRun>().HasQueryFilter(OwnerFilter, run => run.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<UserPreferences>().HasQueryFilter(OwnerFilter, preferences => preferences.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<PushSubscription>().HasQueryFilter(OwnerFilter, subscription => subscription.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<RecommendationDismissal>().HasQueryFilter(OwnerFilter, dismissal => dismissal.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<CommunityCoverHide>().HasQueryFilter(OwnerFilter, hide => hide.OwnerId == CurrentOwnerId);

        modelBuilder.Entity<Volume>().HasQueryFilter(OwnerFilter, volume => volume.Series!.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<SeriesUserState>().HasQueryFilter(OwnerFilter, state => state.Series!.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<SourceChoice>().HasQueryFilter(OwnerFilter, choice => choice.Series!.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<ShelfEvent>().HasQueryFilter(OwnerFilter, shelfEvent => shelfEvent.Series!.OwnerId == CurrentOwnerId);
        modelBuilder.Entity<ImportGroup>().HasQueryFilter(OwnerFilter, group => group.Batch!.OwnerId == CurrentOwnerId);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwners();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOwners();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    // A new root belongs to whoever this scope acts for. No current user throws, so a write
    // from background work that forgot ActAs fails instead of creating an orphan.
    private void StampOwners()
    {
        foreach (var entry in ChangeTracker.Entries<IOwned>())
        {
            if (entry.State is EntityState.Added && entry.Entity.OwnerId == Guid.Empty)
            {
                entry.Entity.OwnerId = currentUser.Id;
            }
        }
    }
}
