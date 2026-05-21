using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.ResultPattern;

namespace PANiXiDA.Core.Presentation.Http.Helpers;

/// <summary>
/// Provides methods for mapping operation results to Minimal API HTTP responses.
/// </summary>
public static class ResultHttpMapper
{
    /// <summary>
    /// Maps a non-generic result to an HTTP response.
    /// </summary>
    /// <param name="result">The operation result.</param>
    /// <param name="onSuccess">The HTTP response factory for a successful result.</param>
    /// <returns>The success HTTP response or a Problem Details response for an error.</returns>
    public static IResult ToHttpResult(
        this Result result,
        Func<IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess();
        }

        return CreateProblem(result.Errors);
    }

    /// <summary>
    /// Maps a generic result to an HTTP response.
    /// </summary>
    /// <typeparam name="TValue">The successful result value type.</typeparam>
    /// <param name="result">The operation result.</param>
    /// <param name="onSuccess">The HTTP response factory for a successful result.</param>
    /// <returns>The success HTTP response or a Problem Details response for an error.</returns>
    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        Func<TValue, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        return CreateProblem(result.Errors);
    }

    /// <summary>
    /// Maps a failed non-generic result to a Problem Details HTTP response.
    /// </summary>
    /// <param name="result">The operation result.</param>
    /// <returns>A Problem Details HTTP response built from the result errors.</returns>
    public static IResult ToHttpProblem(this Result result)
    {
        return CreateProblem(result.Errors);
    }

    /// <summary>
    /// Maps a failed generic result to a Problem Details HTTP response.
    /// </summary>
    /// <typeparam name="TValue">The result value type.</typeparam>
    /// <param name="result">The operation result.</param>
    /// <returns>A Problem Details HTTP response built from the result errors.</returns>
    public static IResult ToHttpProblem<TValue>(this Result<TValue> result)
    {
        return CreateProblem(result.Errors);
    }

    private static IResult CreateProblem(IReadOnlyList<Error> errors)
    {
        var firstError = errors[0];
        var statusCode = GetStatusCode(firstError.Type);

        if (firstError.Type == ErrorType.Validation)
        {
            var validationErrors = errors
                .GroupBy(GetFieldName)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.Message)
                        .Distinct()
                        .ToArray());

            return TypedResults.ValidationProblem(
                errors: validationErrors,
                title: "One or more validation errors occurred.");
        }

        return TypedResults.Problem(
            statusCode: statusCode,
            title: GetTitle(firstError.Type),
            detail: firstError.Message);
    }

    private static int GetStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Failure => StatusCodes.Status400BadRequest,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string GetTitle(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.NotFound => "Resource not found",
            ErrorType.Conflict => "Conflict",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.Failure => "Request failed",
            ErrorType.Unexpected => "Server error",
            _ => "Server error"
        };
    }

    private static string GetFieldName(Error error)
    {
        if (error.Metadata.TryGetValue(Error.FieldMetadataKey, out var field) &&
            field is string fieldName &&
            !string.IsNullOrWhiteSpace(fieldName))
        {
            return fieldName;
        }

        return "general";
    }
}
