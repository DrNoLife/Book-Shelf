using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace LnShelf.Api.Features.Auth;

/// <summary>
/// Login (feature-spec §7): the API runs OpenID Connect against the provider and keeps the
/// session in a cookie, so the WASM bundle never holds a token. Every signed-in identity is a
/// user with their own shelf.
/// </summary>
public static class AuthFeatureExtensions
{
    // Under /api/ because nginx only proxies that prefix; the handler's default
    // /signin-oidc would land on the client's fallback page instead.
    public const string CallbackPath = "/api/auth/callback";

    extension(IServiceCollection services)
    {
        /// <summary>The avatar: the provider picture's local copy and the user's own upload.</summary>
        private void AddAvatars(IConfiguration configuration)
        {
            services.AddOptions<AvatarOptions>()
                .Bind(configuration.GetSection(AvatarOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<IAvatarQueryService, AvatarQueryService>();
            services.AddScoped<IAvatarCommandService, AvatarCommandService>();

            // A client of its own, through the factory: the picture comes from the provider's
            // CDN, not from a metadata API, and sign-in waits on it, so its timeout is short
            // and its body capped well under a cover's (AvatarOptions).
            services.AddHttpClient<IProviderAvatarFetcher, ProviderAvatarFetcher>((provider, httpClient) =>
            {
                var avatars = provider.GetRequiredService<IOptions<AvatarOptions>>().Value;
                httpClient.Timeout = avatars.FetchTimeout;
                httpClient.MaxResponseContentBufferSize = avatars.MaximumFetchBytes;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
            })
            // No redirects: the address comes from the provider's signed token, but a redirect
            // could still send this server to a host nobody vetted, internal ones included.
            // The provider serves the picture at the address it names.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        }

        public IServiceCollection AddAuth(IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddOptions<AuthOptions>()
                .Bind(configuration.GetSection(AuthOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
            services.AddScoped<UserSignIn>();
            services.AddScoped<AccountDeletionService>();
            services.AddScoped<ICurrentUserQueryService, CurrentUserQueryService>();
            services.AddScoped<IAccountExportQueryService, AccountExportQueryService>();
            services.AddAvatars(configuration);

            var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

            // Every endpoint needs a signed-in owner unless it says [AllowAnonymous]. A new
            // controller is closed by default rather than open by default.
            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

            // Behind nginx, and in production behind a TLS proxy as well: the scheme and client
            // address come from X-Forwarded-*. Any proxy is trusted because the API publishes
            // no port; nginx on the compose network is the only thing that can reach it.
            services.Configure<ForwardedHeadersOptions>(forwarded =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.KnownIPNetworks.Clear();
                forwarded.KnownProxies.Clear();

                // Two hops, not the default one: behind the Cloudflare Tunnel X-Forwarded-For
                // arrives as "visitor, cloudflared" (cloudflared adds the first, nginx the
                // second), and with one hop every visitor would share cloudflared's address,
                // so the shared shelf's per-address rate limit (Features/Sharing) would be one
                // bucket for the whole internet. On the plain LAN port a client can spoof the
                // extra entry; that only moves it to another rate-limit bucket.
                forwarded.ForwardLimit = 2;
            });

            if (auth.DevelopmentBypass && environment.IsDevelopment())
            {
                // OpenID Connect is not registered at all here: its handler validates its options
                // on every request and would throw on the empty client id.
                services.AddAuthentication(DevelopmentBypassAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, DevelopmentBypassAuthenticationHandler>(
                        DevelopmentBypassAuthenticationHandler.SchemeName, configureOptions: null);

                return services;
            }

            services.AddDataProtection()
                .SetApplicationName("LnShelf")
                .PersistKeysToDbContext<LnShelfDbContext>();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(cookie =>
                {
                    cookie.Cookie.Name = "lnshelf.session";
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SameSite = SameSiteMode.Lax;

                    // Secure whenever the request was HTTPS, which behind the TLS proxy it is once
                    // forwarded headers have run. Plain HTTP stays possible for localhost and tests.
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    cookie.SlidingExpiration = true;
                    cookie.ExpireTimeSpan = auth.SessionLifetime;

                    // An API call is answered with a status, never a redirect to the provider:
                    // a fetch cannot follow one, and the client redirects on a 401 itself.
                    cookie.Events.OnRedirectToLogin = context => WriteProblemAsync(
                        context.HttpContext, StatusCodes.Status401Unauthorized, "Sign in first");
                    cookie.Events.OnRedirectToAccessDenied = context => WriteProblemAsync(
                        context.HttpContext, StatusCodes.Status403Forbidden, "Not allowed");

                    // A session whose user is gone (account deleted) or that predates users
                    // (no user claim) is dropped, and the next API call answers 401.
                    cookie.Events.OnValidatePrincipal = async context =>
                    {
                        var claim = context.Principal?.FindFirst(LnShelfClaims.UserId)?.Value;
                        var exists = Guid.TryParse(claim, out var userId)
                            && await context.HttpContext.RequestServices.GetRequiredService<LnShelfDbContext>()
                                .Users.AnyAsync(user => user.Id == userId, context.HttpContext.RequestAborted);

                        if (!exists)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    };
                })
                .AddOpenIdConnect(openIdConnect =>
                {
                    openIdConnect.Authority = auth.Authority.ToString();
                    openIdConnect.ClientId = auth.ClientId;
                    openIdConnect.ClientSecret = auth.ClientSecret;

                    // Authorization code with PKCE, which the handler adds by default.
                    openIdConnect.ResponseType = "code";
                    openIdConnect.CallbackPath = CallbackPath;
                    openIdConnect.SignedOutCallbackPath = "/api/auth/signout-callback";

                    // "profile" for the picture claim, which becomes the avatar (AvatarCommandService).
                    // It brings the name too, which is deliberately not kept: UserSignIn reads the
                    // picture and nothing else from it, and the privacy page promises as much.
                    openIdConnect.Scope.Clear();
                    openIdConnect.Scope.Add("openid");
                    openIdConnect.Scope.Add("email");
                    openIdConnect.Scope.Add("profile");

                    // The tokens are only needed to learn who signed in; nothing calls the
                    // provider's APIs afterwards, so none are kept in the cookie.
                    openIdConnect.SaveTokens = false;

                    // The token's own claim names ("sub", "email", "iss"), which UserSignIn reads,
                    // rather than the long WS-Federation URIs they are mapped to by default.
                    openIdConnect.MapInboundClaims = false;

                    // The session carries this app's user, not the provider's raw identity.
                    openIdConnect.Events.OnTokenValidated = async context =>
                    {
                        var principal = context.Principal is null
                            ? null
                            : await context.HttpContext.RequestServices.GetRequiredService<UserSignIn>()
                                .SignInAsync(context.Principal, context.HttpContext.RequestAborted);

                        if (principal is null)
                        {
                            context.Fail("This account is not allowed in.");
                            return;
                        }

                        context.Principal = principal;
                    };

                    openIdConnect.Events.OnRemoteFailure = async context =>
                    {
                        context.HandleResponse();
                        await WriteProblemAsync(
                            context.HttpContext, StatusCodes.Status403Forbidden, "Sign-in refused", context.Failure?.Message);
                    };
                });

            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        /// <summary>Who is asking, then whether they may. Between error handling and the controllers.</summary>
        public IApplicationBuilder UseAuth()
        {
            app.UseAuthentication();
            app.UseMiddleware<CrossSiteWriteRejectionMiddleware>();
            app.UseAuthorization();

            return app;
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string? detail = null)
    {
        context.Response.StatusCode = statusCode;

        return context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = statusCode, Title = title, Detail = detail },
        }).AsTask();
    }
}
