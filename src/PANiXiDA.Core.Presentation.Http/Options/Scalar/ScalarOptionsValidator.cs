using Microsoft.Extensions.Options;

namespace PANiXiDA.Core.Presentation.Http.Options.Scalar;

internal sealed class ScalarOptionsValidator : IValidateOptions<ScalarOptions>
{
    public ValidateOptionsResult Validate(string? name, ScalarOptions options)
    {
        if (options.BearerAuthenticationSchemes is null ||
            options.BearerAuthenticationSchemes.Any(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail(
                $"{ScalarOptions.SectionName}:BearerAuthenticationSchemes must be an array of non-empty scheme names.");
        }

        return ValidateOptionsResult.Success;
    }
}
