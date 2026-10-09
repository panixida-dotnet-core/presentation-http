using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Application.Authentication.Abstractions;
using PANiXiDA.Core.Presentation.Http.Authentication;
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

using System.Globalization;
using System.Security.Claims;

using ApplicationClaimTypes = PANiXiDA.Core.Application.Authentication.ClaimTypes;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Authentication;

public sealed class HttpCurrentUserTests
{
    [Fact(DisplayName = "Current user is anonymous outside an HTTP request")]
    public void CurrentUser_ShouldBeAnonymousWithoutHttpContext()
    {
        AssertAnonymous(new HttpCurrentUser(new HttpContextAccessor()));
    }

    [Fact(DisplayName = "Current user ignores all claims on an unauthenticated identity")]
    public void CurrentUser_ShouldIgnoreUnauthenticatedClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "untrusted"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ApplicationClaimTypes.Permission, "files.create")
        ]);

        AssertAnonymous(CreateUser(identity));
    }

    [Fact(DisplayName = "Current user reads raw JWT identity claims and distinct roles")]
    public void CurrentUser_ShouldReadJwtClaims()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("name", "alice"),
            new Claim("role", "editor"),
            new Claim("role", "editor"),
            new Claim(ClaimTypes.Role, "reader"),
            new Claim(ApplicationClaimTypes.Permission, "files.create")
        ], "Bearer"));

        user.IsAuthenticated.ShouldBeTrue();
        user.UserId.ShouldBe(userId);
        user.UserName.ShouldBe("alice");
        user.Roles.ShouldBe(["editor", "reader"]);
        user.HasPermission("files.create").ShouldBeTrue();
        user.HasPermission("Files.Create").ShouldBeFalse();
        user.TryGetClaimValue<Guid>("sub", out var parsed).ShouldBeTrue();
        parsed.ShouldBe(userId);
    }

    [Fact(DisplayName = "Current user respects mapped and configured identity claim types")]
    public void CurrentUser_ShouldReadConfiguredClaimTypes()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("display_name", "Alice"),
            new Claim("name", "fallback"),
            new Claim("user_role", "editor")
        ], "Bearer", "display_name", "user_role"));

        user.UserId.ShouldBe(userId);
        user.UserName.ShouldBe("Alice");
        user.Roles.ShouldBe(["editor"]);
    }

    [Theory(DisplayName = "Current user returns null for missing or invalid account identifiers")]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public void CurrentUser_ShouldReturnNullForInvalidUserId(string? subject)
    {
        var identity = new ClaimsIdentity("Bearer");
        if (subject is not null)
        {
            identity.AddClaim(new Claim("sub", subject));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        }

        var user = CreateUser(identity);

        user.IsAuthenticated.ShouldBeTrue();
        user.UserId.ShouldBeNull();
        user.UserName.ShouldBeNull();
        user.Roles.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Current user never combines claims from different identities")]
    public void CurrentUser_ShouldUseFirstAuthenticatedIdentity()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(
            new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            new ClaimsIdentity([new Claim("sub", userId.ToString())], "Bearer"),
            new ClaimsIdentity([new Claim(ApplicationClaimTypes.Permission, "files.create")], "Other"));

        user.UserId.ShouldBe(userId);
        user.HasPermission("files.create").ShouldBeFalse();
    }

    [Fact(DisplayName = "Claim parsing uses invariant culture and only the first matching claim")]
    public void TryGetClaimValue_ShouldUseInvariantCultureAndFirstClaim()
    {
        var user = CreateUser(new ClaimsIdentity(
        [
            new Claim("amount", "1.5"),
            new Claim("count", "invalid"),
            new Claim("count", "42")
        ], "Bearer"));
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

            user.TryGetClaimValue<decimal>("amount", out var amount).ShouldBeTrue();
            amount.ShouldBe(1.5m);
            user.TryGetClaimValue<int>("count", out var count).ShouldBeFalse();
            count.ShouldBe(0);
            user.TryGetClaimValue<string>("missing", out var missing).ShouldBeFalse();
            missing.ShouldBeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory(DisplayName = "Roles, sections, and empty values never grant permissions")]
    [InlineData("files.create")]
    [InlineData("")]
    [InlineData(" ")]
    public void HasPermission_ShouldRequirePermissionClaim(string permission)
    {
        var user = CreateUser(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "files.create"),
            new Claim(ApplicationClaimTypes.Section, "files.create"),
            new Claim(ApplicationClaimTypes.Permission, " ")
        ], "Bearer"));

        user.HasPermission(permission).ShouldBeFalse();
    }

    [Fact(DisplayName = "AddHttp registers a scoped current user that reads the current HTTP context")]
    public void AddHttp_ShouldRegisterScopedCurrentUser()
    {
        var services = new ServiceCollection();
        services.AddHttp(new ConfigurationBuilder().Build());
        services.Single(service => service.ServiceType == typeof(ICurrentUser))
            .Lifetime.ShouldBe(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();

        try
        {
            user.IsAuthenticated.ShouldBeFalse();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
            };

            user.IsAuthenticated.ShouldBeTrue();
            user.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    [Fact(DisplayName = "AddHttp preserves an existing current user registration")]
    public void AddHttp_ShouldPreserveCustomCurrentUser()
    {
        var currentUser = CreateUser(new ClaimsIdentity("Custom"));
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(currentUser);

        services.AddHttp(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICurrentUser>().ShouldBeSameAs(currentUser);
    }

    private static void AssertAnonymous(HttpCurrentUser user)
    {
        user.IsAuthenticated.ShouldBeFalse();
        user.UserId.ShouldBeNull();
        user.UserName.ShouldBeNull();
        user.Roles.ShouldBeEmpty();
        user.HasPermission("files.create").ShouldBeFalse();
        user.TryGetClaimValue<Guid>("sub", out var userId).ShouldBeFalse();
        userId.ShouldBe(Guid.Empty);
    }

    private static HttpCurrentUser CreateUser(params ClaimsIdentity[] identities)
    {
        return new HttpCurrentUser(new TestHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identities) }
        });
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
