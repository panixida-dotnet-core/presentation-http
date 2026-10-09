using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class AuthenticationConfigurationTests
{
    [Theory(DisplayName = "AddHttp configures token introspection from standard OpenIddict options")]
    [InlineData(false)]
    [InlineData(true)]
    public void AddHttp_ShouldConfigureIntrospection(bool useModules)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = CreateConfiguration();
        var assembly = typeof(AuthenticationConfigurationTests).Assembly;
        configuration[$"HttpModules:{assembly.GetName().Name}:Name"] = "test";
        configuration[$"HttpModules:{assembly.GetName().Name}:Title"] = "Test API";

        var result = useModules
            ? services.AddHttp(configuration, assembly)
            : services.AddHttp(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictValidationOptions>>().Value;
        var authentication = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        result.ShouldBeSameAs(services);
        options.Issuer.ShouldBe(new Uri("https://identity.example.test/"));
        options.Audiences.ShouldHaveSingleItem().ShouldBe("panixida-api");
        options.ClientId.ShouldBe("panixida-api");
        options.ClientSecret.ShouldBe("test-secret");
        options.ValidationType.ShouldBe(OpenIddictValidationType.Introspection);
        options.TokenValidationParameters.NameClaimType.ShouldBe("name");
        options.TokenValidationParameters.RoleClaimType.ShouldBe("role");
        authentication.DefaultScheme.ShouldBe(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
    }

    [Fact(DisplayName = "AddHttp preserves host authentication when introspection configuration is absent")]
    public void AddHttp_ShouldPreserveHostAuthenticationWithoutSection()
    {
        var services = new ServiceCollection();
        services.AddAuthentication("Existing").AddCookie("Existing");

        services.AddHttp(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme.ShouldBe("Existing");
        provider.GetService<OpenIddictValidationService>().ShouldBeNull();
    }

    [Theory(DisplayName = "Invalid issuer or missing introspection credentials prevent startup")]
    [InlineData("Issuer", null)]
    [InlineData("Issuer", "relative")]
    [InlineData("Issuer", "https://identity.example.test/?query=1")]
    [InlineData("Issuer", "https://identity.example.test/#fragment")]
    [InlineData("ClientId", "")]
    [InlineData("ClientSecret", null)]
    public async Task AddHttp_ShouldRejectInvalidOpenIddictConfiguration(string key, string? value)
    {
        var builder = CreateBuilder(key, value);
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();

        await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken));
    }

    [Theory(DisplayName = "Startup uses OpenIddict validation without additional issuer or audience rules")]
    [InlineData("Issuer", "http://identity.example.test/")]
    [InlineData("Audiences:0", null)]
    [InlineData("Audiences:0", " ")]
    public async Task AddHttp_ShouldUseOpenIddictValidationRules(string key, string? value)
    {
        var builder = CreateBuilder(key, value);
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();

        await Should.NotThrowAsync(() => app.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "OpenIddict permits startup without audience configuration")]
    public async Task AddHttp_ShouldAllowMissingAudienceConfiguration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddictValidationOptions:Issuer"] = "https://identity.example.test/",
            ["OpenIddictValidationOptions:ClientId"] = "panixida-api",
            ["OpenIddictValidationOptions:ClientSecret"] = "test-secret"
        });
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();

        await app.StartAsync(TestContext.Current.CancellationToken);

        app.Services.GetRequiredService<IOptions<OpenIddictValidationOptions>>().Value.Audiences.ShouldBeEmpty();
    }

    private static WebApplicationBuilder CreateBuilder(string key, string? value)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var configuration = CreateConfiguration();
        configuration[$"OpenIddictValidationOptions:{key}"] = value;
        builder.Configuration.AddConfiguration(configuration);
        return builder;
    }

    private static IConfigurationRoot CreateConfiguration()
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddictValidationOptions:Issuer"] = "https://identity.example.test/",
            ["OpenIddictValidationOptions:Audiences:0"] = "panixida-api",
            ["OpenIddictValidationOptions:ClientId"] = "panixida-api",
            ["OpenIddictValidationOptions:ClientSecret"] = "test-secret"
        }).Build();
    }
}
