using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

using PANiXiDA.Core.Presentation.Http.Middlewares;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class ClientAbortedExceptionHandlerTests
{
    [Theory(DisplayName = "TryHandleAsync preserves a started response when handling client cancellation")]
    [InlineData(false, false, StatusCodes.Status499ClientClosedRequest)]
    [InlineData(true, false, StatusCodes.Status499ClientClosedRequest)]
    [InlineData(false, true, StatusCodes.Status200OK)]
    [InlineData(true, true, StatusCodes.Status200OK)]
    public async Task TryHandleAsync_ShouldPreserveStartedResponse(
        bool aggregate,
        bool responseStarted,
        int expectedStatus)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = new CancellationToken(true)
        };
        httpContext.Features.Set<IHttpResponseFeature>(new TestResponseFeature(responseStarted));
        var cancellation = new TaskCanceledException();
        Exception exception = aggregate ? new AggregateException(cancellation) : cancellation;
        var handler = new ClientAbortedExceptionHandler();

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(expectedStatus);
    }

    private sealed class TestResponseFeature(bool responseStarted) : HttpResponseFeature
    {
        public override bool HasStarted => responseStarted;
    }
}
