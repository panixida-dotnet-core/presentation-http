using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using PANiXiDA.Core.Presentation.Http.Helpers;
using PANiXiDA.Core.ResultPattern;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Helpers;

public sealed class ResultHttpMapperTests
{
    [Fact(DisplayName = "ToHttpResult returns the factory response for a successful Result")]
    public void ToHttpResult_ShouldReturnSuccessResultForSuccessfulResult()
    {
        var expected = TypedResults.Ok("created");
        var result = Result.Success();

        var httpResult = result.ToHttpResult(() => expected);

        httpResult.ShouldBeSameAs(expected);
    }

    [Fact(DisplayName = "ToHttpResult returns ProblemDetails for a failed Result")]
    public void ToHttpResult_ShouldReturnProblemForFailedResult()
    {
        var result = Result.Failure(Error.NotFound("Order not found"));
        var successInvoked = false;

        var httpResult = result.ToHttpResult(() =>
        {
            successInvoked = true;

            return TypedResults.Ok();
        });

        successInvoked.ShouldBeFalse();

        var problemDetails = AssertProblem(httpResult, StatusCodes.Status404NotFound);
        problemDetails.Title.ShouldBe("Resource not found");
        problemDetails.Detail.ShouldBe("Order not found");
    }

    [Fact(DisplayName = "ToHttpResult passes the value to the factory for a successful generic Result")]
    public void ToHttpResult_ShouldPassValueToSuccessFactoryForSuccessfulGenericResult()
    {
        var expected = TypedResults.Ok("mapped");
        var result = Result.Success("source");
        string? receivedValue = null;

        var httpResult = result.ToHttpResult(value =>
        {
            receivedValue = value;

            return expected;
        });

        httpResult.ShouldBeSameAs(expected);
        receivedValue.ShouldBe("source");
    }

    [Fact(DisplayName = "ToHttpResult returns ProblemDetails for a failed generic Result")]
    public void ToHttpResult_ShouldReturnProblemForFailedGenericResult()
    {
        var result = Result.Failure<string>(Error.Conflict("Order already exists"));
        var successInvoked = false;

        var httpResult = result.ToHttpResult(value =>
        {
            successInvoked = true;

            return TypedResults.Ok(value);
        });

        successInvoked.ShouldBeFalse();

        var problemDetails = AssertProblem(httpResult, StatusCodes.Status409Conflict);
        problemDetails.Title.ShouldBe("Conflict");
        problemDetails.Detail.ShouldBe("Order already exists");
    }

    [Theory(DisplayName = "ToHttpProblem returns the expected status code and title")]
    [MemberData(nameof(GetProblemCases))]
    public void ToHttpProblem_ShouldReturnExpectedStatusCodeAndTitle(
        ErrorType errorType,
        string message,
        int expectedStatusCode,
        string expectedTitle)
    {
        var error = CreateError(errorType, message);
        var result = Result.Failure(error);

        var httpResult = result.ToHttpProblem();

        var problemDetails = AssertProblem(httpResult, expectedStatusCode);
        problemDetails.Title.ShouldBe(expectedTitle);
        problemDetails.Detail.ShouldBe(message);
    }

    [Fact(DisplayName = "ToHttpProblem returns ProblemDetails for a generic Result")]
    public void ToHttpProblem_ShouldReturnProblemForGenericResult()
    {
        var result = Result.Failure<string>(Error.Unauthorized("Authentication required"));

        var httpResult = result.ToHttpProblem();

        var problemDetails = AssertProblem(httpResult, StatusCodes.Status401Unauthorized);
        problemDetails.Title.ShouldBe("Unauthorized");
        problemDetails.Detail.ShouldBe("Authentication required");
    }

    [Fact(DisplayName = "ToHttpProblem groups validation errors by field")]
    public void ToHttpProblem_ShouldGroupValidationErrorsByField()
    {
        var result = Result.Failure(
        [
            Error.Validation("Required").WithField("email"),
            Error.Validation("Required").WithField("email"),
            Error.Validation("Too short").WithField("password"),
            Error.Validation("General error"),
            Error.Validation("Blank field").WithMetadata(Error.FieldMetadataKey, " "),
            Error.Validation("Wrong metadata").WithMetadata(Error.FieldMetadataKey, 123)
        ]);

        var httpResult = result.ToHttpProblem();

        var problemDetails = AssertValidationProblem(httpResult);
        problemDetails.Title.ShouldBe("One or more validation errors occurred.");
        problemDetails.Errors["email"].ShouldBe(["Required"]);
        problemDetails.Errors["password"].ShouldBe(["Too short"]);
        problemDetails.Errors["general"].ShouldBe(["General error", "Blank field", "Wrong metadata"]);
    }

    public static TheoryData<ErrorType, string, int, string> GetProblemCases()
    {
        return new TheoryData<ErrorType, string, int, string>
    {
        { ErrorType.NotFound, "Not found", StatusCodes.Status404NotFound, "Resource not found" },
        { ErrorType.Conflict, "Conflict", StatusCodes.Status409Conflict, "Conflict" },
        { ErrorType.Unauthorized, "Unauthorized", StatusCodes.Status401Unauthorized, "Unauthorized" },
        { ErrorType.Forbidden, "Forbidden", StatusCodes.Status403Forbidden, "Forbidden" },
        { ErrorType.Failure, "Failure", StatusCodes.Status400BadRequest, "Request failed" },
        { ErrorType.Unexpected, "Unexpected", StatusCodes.Status500InternalServerError, "Server error" },
        { (ErrorType)999, "Unknown", StatusCodes.Status500InternalServerError, "Server error" }
    };
    }

    private static Error CreateError(ErrorType errorType, string message)
    {
        return errorType switch
        {
            ErrorType.NotFound => Error.NotFound(message),
            ErrorType.Conflict => Error.Conflict(message),
            ErrorType.Unauthorized => Error.Unauthorized(message),
            ErrorType.Forbidden => Error.Forbidden(message),
            ErrorType.Failure => Error.Failure(message),
            ErrorType.Unexpected => Error.Unexpected(message),
            _ => new Error(message, errorType, new Dictionary<string, object?>())
        };
    }

    private static ProblemDetails AssertProblem(IResult httpResult, int expectedStatusCode)
    {
        httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>()
            .StatusCode.ShouldBe(expectedStatusCode);

        var valueResult = httpResult.ShouldBeAssignableTo<IValueHttpResult<ProblemDetails>>();

        return valueResult.Value.ShouldNotBeNull()
            .ShouldBeOfType<ProblemDetails>();
    }

    private static HttpValidationProblemDetails AssertValidationProblem(IResult httpResult)
    {
        httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>()
            .StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var valueResult = httpResult.ShouldBeAssignableTo<IValueHttpResult<HttpValidationProblemDetails>>();

        return valueResult.Value.ShouldNotBeNull()
            .ShouldBeOfType<HttpValidationProblemDetails>();
    }
}
