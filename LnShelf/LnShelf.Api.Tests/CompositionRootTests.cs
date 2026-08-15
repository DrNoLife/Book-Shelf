using LnShelf.Api.Commands;
using LnShelf.Api.Data;
using LnShelf.Api.Features.Covers;
using LnShelf.Api.Features.Import;
using LnShelf.Api.Features.Metadata;
using LnShelf.Api.Features.Preferences;
using LnShelf.Api.Features.Shelf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests;

/// <summary>
/// Every service the composition root registers can actually be constructed.
/// </summary>
// Written after a registration change broke startup and no test noticed: the service tests
// all construct their subject with `new`, which is the right way to test behaviour and the
// exact reason none of them exercises the container. Swapping a concrete registration for
// an interface left one constructor asking for a type nobody registered any more, and the
// only thing that caught it was running the app.
//
// This is not a ceremony test for DI. It is the one assertion that the wiring exists, which
// is a fact about the app that nothing else here checks.
public sealed class CompositionRootTests
{
    [Fact]
    public void EveryRegisteredService_CanBeConstructed()
    {
        var services = BuildServices();

        // ValidateOnBuild walks every descriptor and resolves its constructor arguments,
        // which is precisely the check that was missing. ValidateScopes catches a singleton
        // capturing a scoped DbContext, which is the other way this wiring goes wrong.
        var act = () => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        act.ShouldNotThrow();
    }

    [Theory]
    [InlineData("isbn-backfill")]
    [InlineData("isbn-discover")]
    [InlineData("isbn-apply")]
    [InlineData("enrichment-verify")]
    [InlineData("subtitle-backfill")]
    public void EveryConsoleCommand_IsResolvableByItsName(string commandName)
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();

        // The commands are the least-exercised path in the app — nothing calls them unless
        // somebody types their name — so the name and the registration are asserted rather
        // than discovered at the moment of use.
        scope.ServiceProvider.GetServices<IConsoleCommand>()
            .ShouldContain(command => command.Name == commandName);
    }

    public static TheoryData<Type> Controllers =>
    [
        .. typeof(LnShelf.Api.Features.Shelf.Controllers.SeriesController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                           && type.Name.EndsWith("Controller", StringComparison.Ordinal)),
    ];

    [Theory]
    [MemberData(nameof(Controllers))]
    public void EveryController_CanBeActivatedFromAScope(Type controllerType)
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();

        // MVC does not register controllers as services, so ValidateOnBuild never sees
        // them — a controller asking for something nobody registered fails on the first
        // request instead. Activating each one here is the equivalent check.
        var act = () => ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);

        act.ShouldNotThrow();
    }

    /// <summary>
    /// The composition root's own registrations, over configuration that resolves but
    /// connects to nothing.
    /// </summary>
    // A connection string is required by AddLnShelfData and never opened here: registering a
    // DbContext does not touch Postgres, and this test is about the graph rather than the
    // database.
    private static IServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Db"] = "Host=localhost;Database=none;Username=none;Password=none",
                ["Covers:Endpoint"] = "localhost:9000",
                ["Covers:AccessKey"] = "none",
                ["Covers:SecretKey"] = "none",
                ["Covers:Bucket"] = "lnshelf-covers",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Deliberately no AddControllers: MVC's own graph needs a web host to validate
        // (IWebHostEnvironment and friends), and framework registrations are not what this
        // is policing. The controllers are covered separately, by activating them.
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddLnShelfData(configuration);
        services.AddShelf(configuration);
        services.AddMetadata(configuration);
        services.AddCovers(configuration);
        services.AddImport(configuration);
        services.AddPreferences();

        return services;
    }
}
