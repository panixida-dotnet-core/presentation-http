using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class ProblemDetailsConfigurationTests
{
    [Fact(DisplayName = "ProblemDetails configuration leaves regular ProblemDetails unchanged")]
    public void AddProblemDetailsConfiguration_ShouldLeaveRegularProblemDetailsUnchanged()
    {
        var options = CreateOptions();
        var problemDetails = new ProblemDetails
        {
            Title = "Validation failed"
        };

        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = problemDetails
        };

        options.CustomizeProblemDetails!(context);

        context.ProblemDetails.ShouldBeSameAs(problemDetails);
    }

    [Fact(DisplayName = "ProblemDetails configuration normalizes validation error keys to camelCase")]
    public void AddProblemDetailsConfiguration_ShouldNormalizeValidationErrorKeys()
    {
        var options = CreateOptions();
        var validationProblem = new HttpValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["UserName"] = ["Required"],
            ["Address.ZipCode"] = ["Invalid"]
        })
        {
            Title = "Validation failed",
            Type = "https://example.test/validation",
            Status = StatusCodes.Status400BadRequest,
            Detail = "Invalid request",
            Instance = "/users"
        };

        validationProblem.Extensions["code"] = "validation_failed";

        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = validationProblem
        };

        options.CustomizeProblemDetails!(context);

        var normalizedProblem = context.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();

        normalizedProblem.Errors.ContainsKey("userName").ShouldBeTrue();
        normalizedProblem.Errors.ContainsKey("address.ZipCode").ShouldBeTrue();
        normalizedProblem.Errors["userName"].ShouldBe(["Required"]);
        normalizedProblem.Errors["address.ZipCode"].ShouldBe(["Invalid"]);
        normalizedProblem.Title.ShouldBe(validationProblem.Title);
        normalizedProblem.Type.ShouldBe(validationProblem.Type);
        normalizedProblem.Status.ShouldBe(validationProblem.Status);
        normalizedProblem.Detail.ShouldBe(validationProblem.Detail);
        normalizedProblem.Instance.ShouldBe(validationProblem.Instance);
        normalizedProblem.Extensions.ContainsKey("code").ShouldBeTrue();
        normalizedProblem.Extensions["code"].ShouldBe("validation_failed");
    }

    private static ProblemDetailsOptions CreateOptions()
    {
        var services = new ServiceCollection();
        services.AddProblemDetailsConfiguration();

        using var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;
    }
}
