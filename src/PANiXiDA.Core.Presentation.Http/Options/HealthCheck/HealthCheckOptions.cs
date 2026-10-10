namespace PANiXiDA.Core.Presentation.Http.Options.HealthCheck;

internal sealed class HealthCheckOptions
{
    public const string SectionName = "HealthCheckOptions";

    public string Path { get; set; } = "/health";
}
