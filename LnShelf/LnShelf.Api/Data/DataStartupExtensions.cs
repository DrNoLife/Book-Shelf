namespace LnShelf.Api.Data;

/// <summary>
/// Registration and startup for the database itself — cross-slice, so it lives
/// beside the context rather than inside a feature.
/// </summary>
public static class DataStartupExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddLnShelfData(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Db");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Db is not configured. Set it in appsettings.Development.json " +
                    "locally or as ConnectionStrings__Db in the container.");
            }

            services.AddDbContext<LnShelfDbContext>(options => options.UseNpgsql(connectionString));
            return services;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Applies migrations and seeds the fixture on boot.
        /// </summary>
        // One user, one deployable, no migration window to coordinate — running
        // migrations at startup is cheaper than a manual deploy step here. If the
        // app ever grows a second instance this has to move out.
        public async Task MigrateAndSeedAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = app.Services.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);

            var seeder = scope.ServiceProvider.GetRequiredService<ShelfSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
