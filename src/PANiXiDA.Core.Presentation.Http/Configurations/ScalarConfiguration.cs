namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal sealed class ScalarConfiguration
{
    public string? Title { get; set; }

    public string? Favicon { get; set; }

    public string[] BearerAuthenticationSchemes { get; set; } = [];
}
