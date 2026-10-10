using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class AntiforgeryConfigurationTests
{
    private const string HeaderName = "X-Test-CSRF";
    private const string FormFieldName = "csrf";
    private const string CookieName = "Test.Antiforgery";

    [Theory(DisplayName = "UseHttp validates form and header tokens while preserving host antiforgery settings")]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public async Task UseHttp_ShouldPreserveHostOptionsAndAcceptValidTokens(bool configureBefore, bool useHeader, bool upload)
    {
        var builder = CreateBuilder();
        if (configureBefore)
        {
            builder.Services.AddAntiforgery(ConfigureOptions);
        }
        builder.Services.AddHttp(builder.Configuration);
        if (!configureBefore)
        {
            builder.Services.AddAntiforgery(ConfigureOptions);
        }
        await using var app = builder.Build();
        app.UseHttp();
        MapTokenEndpoint(app);
        app.MapPost("/form", ([FromForm] string value) => TypedResults.Text(value));
        app.MapPost("/upload", (IFormFile file) => TypedResults.Text(file.FileName));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        var token = await client.GetStringAsync("/token", TestContext.Current.CancellationToken);
        var values = new Dictionary<string, string> { ["value"] = "submitted" };
        if (useHeader)
        {
            client.DefaultRequestHeaders.Add(HeaderName, token);
        }
        else
        {
            values[FormFieldName] = token;
        }
        using var content = CreateFormContent(upload, values);

        using var response = await client.PostAsync(upload ? "/upload" : "/form", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(upload ? "test.txt" : "submitted");
        var options = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        options.HeaderName.ShouldBe(HeaderName);
        options.FormFieldName.ShouldBe(FormFieldName);
        options.Cookie.Name.ShouldBe(CookieName);
    }

    [Theory(DisplayName = "Invalid form and file tokens return 400 and a warning without invoking the handler")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UseHttp_ShouldRejectInvalidTokens(bool upload, bool tamperToken)
    {
        var builder = CreateBuilder();
        builder.Services.AddHttp(builder.Configuration);
        var logger = new TestLogger<LoggingMiddleware>();
        builder.Services.AddSingleton<ILogger<LoggingMiddleware>>(logger);
        await using var app = builder.Build();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Use(async (context, next) =>
        {
            await next(context);
            if (HttpMethods.IsPost(context.Request.Method))
            {
                completed.TrySetResult();
            }
        });
        app.UseHttp();
        MapTokenEndpoint(app);
        var handlerCalled = false;
        app.MapPost("/form", ([FromForm] string value) =>
        {
            handlerCalled = true;
            return TypedResults.Text(value);
        });
        app.MapPost("/upload", (IFormFile file) =>
        {
            handlerCalled = true;
            return TypedResults.Text(file.FileName);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        var token = await client.GetStringAsync("/token", TestContext.Current.CancellationToken);
        if (tamperToken)
        {
            client.DefaultRequestHeaders.Add(
                app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.HeaderName!,
                token + "invalid");
        }
        using var content = CreateFormContent(upload, new Dictionary<string, string> { ["value"] = "submitted" });

        using var response = await client.PostAsync(upload ? "/upload" : "/form", content, TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        handlerCalled.ShouldBeFalse();
        logger.Entries[logger.Entries.Count - 1].LogLevel.ShouldBe(LogLevel.Warning);
        logger.Entries.ShouldNotContain(entry => entry.LogLevel >= LogLevel.Error);
    }

    [Fact(DisplayName = "Form content metadata alone does not require antiforgery tokens")]
    public async Task UseHttp_ShouldAllowManuallyReadFormsWithoutAntiforgeryMetadata()
    {
        var builder = CreateBuilder();
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        app.UseHttp();
        app.MapPost("/protocol", handler: async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            return TypedResults.Text(form["value"].ToString());
        }).Accepts<string>("application/x-www-form-urlencoded");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        using var content = CreateFormContent(upload: false, new Dictionary<string, string> { ["value"] = "submitted" });

        using var response = await client.PostAsync("/protocol", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("submitted");
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        return builder;
    }

    private static void ConfigureOptions(AntiforgeryOptions options)
    {
        options.HeaderName = HeaderName;
        options.FormFieldName = FormFieldName;
        options.Cookie.Name = CookieName;
    }

    private static void MapTokenEndpoint(WebApplication app)
    {
        app.MapGet("/token", (IAntiforgery antiforgery, HttpContext context) =>
            TypedResults.Text(antiforgery.GetAndStoreTokens(context).RequestToken!));
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        return new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };
    }

    private static HttpContent CreateFormContent(bool upload, Dictionary<string, string> values)
    {
        if (upload)
        {
            var content = new MultipartFormDataContent
            {
                { new StringContent("file contents"), "file", "test.txt" }
            };
            foreach (var (name, value) in values)
            {
                content.Add(new StringContent(value), name);
            }
            return content;
        }

        return new FormUrlEncodedContent(values);
    }
}
