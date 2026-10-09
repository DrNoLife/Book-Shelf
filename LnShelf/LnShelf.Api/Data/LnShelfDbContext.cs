using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace LnShelf.Api.Data;

/// <summary>
/// The single context. Entity configurations live in each slice's own
/// <c>Data/</c> folder and are discovered by assembly scan (data-access.md), so
/// this type stays thin and slices stay self-contained.
/// </summary>
public sealed class LnShelfDbContext(DbContextOptions<LnShelfDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Series> Series => Set<Series>();
    public DbSet<Volume> Volumes => Set<Volume>();
    public DbSet<SeriesUserState> SeriesUserStates => Set<SeriesUserState>();
    public DbSet<VolumeUserState> VolumeUserStates => Set<VolumeUserState>();

    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<SyncDiscrepancy> SyncDiscrepancies => Set<SyncDiscrepancy>();
    public DbSet<SyncDiscrepancyValue> SyncDiscrepancyValues => Set<SyncDiscrepancyValue>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportGroup> ImportGroups => Set<ImportGroup>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportCandidate> ImportCandidates => Set<ImportCandidate>();

    /// <summary>The session cookie's signing keys (Features/Auth).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LnShelfDbContext).Assembly);
}
