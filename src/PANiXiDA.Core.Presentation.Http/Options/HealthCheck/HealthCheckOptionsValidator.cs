using Microsoft.Extensions.Options;

namespace PANiXiDA.Core.Presentation.Http.Options.HealthCheck;

internal sealed class HealthCheckOptionsValidator : IValidateOptions<HealthCheckOptions>
{
    public ValidateOptionsResult Validate(string? name, HealthCheckOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Path) || !options.Path.StartsWith('/'))
        {
            return ValidateOptionsResult.Fail(
                $"{HealthCheckOptions.SectionName}.Path must be a non-empty path starting with '/'.");
        }

        return ValidateOptionsResult.Success;
    }
}
