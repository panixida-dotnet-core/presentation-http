using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class ProblemDetailsConfiguration
{
    internal static IServiceCollection AddProblemDetailsConfiguration(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (context.ProblemDetails is not HttpValidationProblemDetails validationProblem)
                {
                    return;
                }

                var normalizedErrors = validationProblem.Errors.ToDictionary(
                    item => JsonNamingPolicy.CamelCase.ConvertName(item.Key),
                    item => item.Value);

                var normalizedProblem = new HttpValidationProblemDetails(normalizedErrors)
                {
                    Title = validationProblem.Title,
                    Type = validationProblem.Type,
                    Status = validationProblem.Status,
                    Detail = validationProblem.Detail,
                    Instance = validationProblem.Instance
                };

                foreach (var extension in validationProblem.Extensions)
                {
                    normalizedProblem.Extensions[extension.Key] = extension.Value;
                }

                context.ProblemDetails = normalizedProblem;
            };
        });

        return services;
    }
}
