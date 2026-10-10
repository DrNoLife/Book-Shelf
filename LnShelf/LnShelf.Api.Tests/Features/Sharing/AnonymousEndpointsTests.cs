using System.Reflection;
using LnShelf.Api.Features.Sharing.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace LnShelf.Api.Tests.Features.Sharing;

/// <summary>
/// The app is closed by default (feature-spec §7): the only endpoints a stranger may call are
/// login, health and the shared shelf, and the shared shelf's are rate-limited.
/// </summary>
public sealed class AnonymousEndpointsTests
{
    private static readonly string[] AllowedAnonymous =
    [
        "AuthController.Login",
        "HealthController.GetAsync",
        "SharedShelfController.GetAsync",
        "SharedShelfController.GetCoverAsync",
    ];

    [Fact]
    public void AllowAnonymous_IsOnlyOnLoginHealthAndTheSharedShelf()
    {
        var anonymous = Actions()
            .Where(action => action.Controller.IsDefined(typeof(AllowAnonymousAttribute))
                             || action.Method.IsDefined(typeof(AllowAnonymousAttribute)))
            .Select(action => $"{action.Controller.Name}.{action.Method.Name}")
            .Order(StringComparer.Ordinal);

        anonymous.ShouldBe(AllowedAnonymous);
    }

    [Fact]
    public void SharedShelfController_EveryAction_HasARateLimit()
    {
        var actions = Actions().Where(action => action.Controller == typeof(SharedShelfController)).ToList();

        actions.ShouldNotBeEmpty();
        actions.ShouldAllBe(action => action.Method.IsDefined(typeof(EnableRateLimitingAttribute)));
    }

    private static IEnumerable<(Type Controller, MethodInfo Method)> Actions() =>
        typeof(SharedShelfController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.IsDefined(typeof(HttpMethodAttribute), inherit: true))
                .Select(method => (controller, method)));
}
