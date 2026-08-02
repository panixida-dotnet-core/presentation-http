using Microsoft.Extensions.DependencyInjection;

using System.Text.Json.Serialization;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class JsonConfiguration
{
    internal static IServiceCollection AddJsonConfiguration(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        return services;
    }
}
