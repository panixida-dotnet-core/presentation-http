using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Middlewares;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class RequestCompletionLoggingTests
{
    [Theory(DisplayName = "UseHttp logs handled exceptions once with the final status and request scope")]
    [InlineData(true, ExceptionKind.Cancellation, StatusCodes.Status499ClientClosedRequest, LogLevel.Warning)]
    [InlineData(false, ExceptionKind.Cancellation, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(true, ExceptionKind.TaskCancellation, StatusCodes.Status499ClientClosedRequest, LogLevel.Warning)]
    [InlineData(false, ExceptionKind.TaskCancellation, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(true, ExceptionKind.ConnectionFailure, StatusCodes.Status499ClientClosedRequest, LogLevel.Warning)]
    [InlineData(false, ExceptionKind.ConnectionFailure, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(true, ExceptionKind.ApplicationFailure, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(false, ExceptionKind.ApplicationFailure, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(false, ExceptionKind.BadRequest, StatusCodes.Status400BadRequest, LogLevel.Warning)]
    public async Task UseHttp_ShouldLogHandledExceptionOnceWithFinalStatusAndRequestScope(
        bool requestAborted,
        ExceptionKind exceptionKind,
        int expectedStatus,
        LogLevel expectedLevel)
    {
        using var provider = new CapturingLoggerProvider();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestAbortedToken = new CancellationToken(requestAborted);
        Exception exception = exceptionKind switch
        {
            ExceptionKind.Cancellation => new OperationCanceledException(requestAbortedToken),
            ExceptionKind.TaskCancellation => new TaskCanceledException("Request canceled", null, requestAbortedToken),
            ExceptionKind.ConnectionFailure => new IOException("Connection closed"),
            ExceptionKind.BadRequest => new BadHttpRequestException("Invalid request"),
            _ => new InvalidOperationException("Independent application failure")
        };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(provider);
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-42")], "test"));
            await next(context);
            completed.SetResult();
        });
        app.UseHttp();
        app.MapGet("/throw", (HttpContext context) =>
        {
            context.RequestAborted = requestAbortedToken;
            throw exception;
        }).WithDisplayName("Throw endpoint");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var request = new HttpRequestMessage(HttpMethod.Get, "/throw?status=active");
        request.Headers.UserAgent.ParseAdd("UnitTest");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe((HttpStatusCode)expectedStatus);
        var completion = provider.Records.Single(record => record.Category == typeof(LoggingMiddleware).FullName);
        completion.Level.ShouldBe(expectedLevel);
        completion.Message.ShouldBe("HTTP request finished");
        completion.Attributes.Single(pair => pair.Key == "http.response.status_code").Value.ShouldBe(expectedStatus);
        completion.Attributes.Single(pair => pair.Key == "http.server.request.duration_ms").Value.ShouldBeAssignableTo<double>();
        var records = provider.Records.Where(record => record.Category.StartsWith("PANiXiDA.Core.Presentation.Http.", StringComparison.Ordinal)).ToArray();
        records.ShouldHaveSingleItem().ShouldBeSameAs(completion);
        provider.Records.Count(record => record.Level >= LogLevel.Error).ShouldBe(expectedLevel == LogLevel.Error ? 1 : 0);
        if (expectedStatus == StatusCodes.Status499ClientClosedRequest)
        {
            completion.Exception.ShouldBeNull();
        }
        else
        {
            completion.Exception.ShouldBeSameAs(exception);
            completion.Exception.ShouldNotBeNull().StackTrace.ShouldNotBeNullOrEmpty();
        }

        foreach (var record in records)
        {
            record.Attributes.Single(pair => pair.Key == "network.protocol.name").Value.ShouldBe("http");
            record.Attributes.Single(pair => pair.Key == "http.request.method").Value.ShouldBe(HttpMethods.Get);
            record.Attributes.Single(pair => pair.Key == "url.path").Value.ShouldBe("/throw");
            record.Attributes.Single(pair => pair.Key == "url.query").Value.ShouldBe("?status=active");
            record.Attributes.Single(pair => pair.Key == "http.route").Value.ShouldBe("/throw");
            record.Attributes.Single(pair => pair.Key == "aspnetcore.endpoint.display_name").Value.ShouldBe("Throw endpoint");
            record.Attributes.Single(pair => pair.Key == "enduser.id").Value.ShouldBe("user-42");
            record.Attributes.Single(pair => pair.Key == "client.address").Value.ShouldBe("127.0.0.1");
            record.Attributes.Single(pair => pair.Key == "user_agent.original").Value.ShouldBe("UnitTest");
        }
    }

    public enum ExceptionKind
    {
        Cancellation,
        TaskCancellation,
        ConnectionFailure,
        ApplicationFailure,
        BadRequest
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

        public ConcurrentQueue<LogEntry> Records { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

        public void SetScopeProvider(IExternalScopeProvider externalScopeProvider) => scopeProvider = externalScopeProvider;

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => provider.scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var attributes = new List<KeyValuePair<string, object?>>();
                provider.scopeProvider.ForEachScope(static (scope, values) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        values.AddRange(pairs);
                    }
                }, attributes);
                provider.Records.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception, attributes));
            }
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception, List<KeyValuePair<string, object?>> Attributes);
}
