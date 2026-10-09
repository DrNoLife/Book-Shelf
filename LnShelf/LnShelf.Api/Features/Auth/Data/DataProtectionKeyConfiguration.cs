using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace LnShelf.Api.Features.Auth.Data;

/// <summary>
/// The keys that sign the session cookie. In Postgres, so a container restart or a
/// redeploy does not log the owner out, and the database's backup covers them.
/// </summary>
public sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder) =>
        builder.ToTable("data_protection_key");
}
