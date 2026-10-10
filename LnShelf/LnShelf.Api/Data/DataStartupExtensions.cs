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

            // The context's owner filters need to know who is asking (Features/Auth), so the
            // current user is registered with the data layer that cannot work without it.
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();

            services.AddDbContext<LnShelfDbContext>(options => options.UseNpgsql(connectionString));
            return services;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Applies migrations on boot and, in development, prepares the development user.
        /// </summary>
        // One deployable, no migration window to coordinate — running migrations at startup
        // is cheaper than a manual deploy step here. If the app ever grows a second instance
        // this has to move out.
        public async Task MigrateAndSeedAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = app.Services.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);

            var auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
            if (auth.DevelopmentBypass && app.Environment.IsDevelopment())
            {
                await EnsureDevelopmentUserAsync(dbContext, cancellationToken);

                // The fixture library is the development user's; the seeder writes as whoever
                // the scope acts for, and skips a shelf that already has series.
                scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(User.DevelopmentUserId);
                await scope.ServiceProvider.GetRequiredService<ShelfSeeder>().SeedAsync(cancellationToken);
            }
            else if (await dbContext.Users.AnyAsync(
                         user => user.Id == User.InitialOwnerPlaceholderId && user.Issuer == User.LocalIssuer,
                         cancellationToken))
            {
                // Said once per boot until someone adopts it: a mistyped InitialOwnerEmail
                // otherwise leaves the old library unreachable without a word.
                app.Logger.LogWarning(
                    "The library from before users is still unclaimed; it goes to {InitialOwnerEmail} on first sign-in",
                    auth.InitialOwnerEmail.Length == 0 ? "(Auth:InitialOwnerEmail is not set)" : auth.InitialOwnerEmail);
            }
        }
    }

    private static async Task EnsureDevelopmentUserAsync(LnShelfDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(user => user.Id == User.DevelopmentUserId, cancellationToken))
        {
            return;
        }

        dbContext.Users.Add(new User
        {
            Id = User.DevelopmentUserId,
            Issuer = User.LocalIssuer,
            Subject = "development",
            Email = "developer@localhost",
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
