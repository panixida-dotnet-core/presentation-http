using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Options.Scalar;

using System.Text;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Options.Scalar;

public sealed class ScalarOptionsValidatorTests
{
    [Theory(DisplayName = "Scalar options allow defaults and configured Bearer scheme names")]
    [InlineData("{}", new string[] { })]
    [InlineData("""{"ScalarConfiguration":{"BearerAuthenticationSchemes":null}}""", new string[] { })]
    [InlineData("""{"ScalarConfiguration":{"BearerAuthenticationSchemes":[]}}""", new string[] { })]
    [InlineData("""{"ScalarConfiguration":{"BearerAuthenticationSchemes":["CustomBearer","OpenIddict.Server.AspNetCore"]}}""",
        new[] { "CustomBearer", "OpenIddict.Server.AspNetCore" })]
    public async Task AddHttp_ShouldAcceptValidScalarOptions(string json, string[] expectedSchemes)
    {
        var builder = CreateBuilder(json);
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();

        await app.StartAsync(TestContext.Current.CancellationToken);

        var options = app.Services.GetRequiredService<IOptions<ScalarOptions>>().Value;
        options.BearerAuthenticationSchemes.ShouldBe(expectedSchemes);
    }

    [Theory(DisplayName = "Invalid Scalar Bearer scheme names prevent startup")]
    [InlineData("[null]")]
    [InlineData("[\"\"]")]
    [InlineData("[\" \"]")]
    [InlineData("[\"CustomBearer\",\"\"]")]
    public async Task AddHttp_ShouldRejectInvalidScalarOptions(string schemesJson)
    {
        var json = $$$"""{"ScalarConfiguration":{"BearerAuthenticationSchemes":{{{schemesJson}}}}}""";
        var builder = CreateBuilder(json);
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken));

        exception.OptionsType.ShouldBe(typeof(ScalarOptions));
        exception.Message.ShouldContain("ScalarConfiguration:BearerAuthenticationSchemes");
    }

    [Fact(DisplayName = "A null Scalar Bearer scheme collection prevents startup")]
    public async Task AddHttp_ShouldRejectNullBearerSchemeCollection()
    {
        var builder = CreateBuilder("{}");
        builder.Services.AddHttp(builder.Configuration);
        builder.Services.Configure<ScalarOptions>(options => options.BearerAuthenticationSchemes = null!);
        await using var app = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken));

        exception.OptionsType.ShouldBe(typeof(ScalarOptions));
        exception.Message.ShouldContain("ScalarConfiguration:BearerAuthenticationSchemes");
    }

    private static WebApplicationBuilder CreateBuilder(string json)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        builder.Configuration.AddJsonStream(stream);
        return builder;
    }
}
