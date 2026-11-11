using Microsoft.Extensions.Options;

namespace PANiXiDA.Core.Presentation.Http.Options.OpenApi;

internal sealed class OpenApiOptionsValidator : IValidateOptions<OpenApiOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenApiOptions options)
    {
        if (options.BearerAuthenticationSchemes is null ||
            options.BearerAuthenticationSchemes.Any(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail(
                $"{OpenApiOptions.SectionName}:BearerAuthenticationSchemes must be an array of non-empty scheme names.");
        }

        return ValidateOptionsResult.Success;
    }
}
