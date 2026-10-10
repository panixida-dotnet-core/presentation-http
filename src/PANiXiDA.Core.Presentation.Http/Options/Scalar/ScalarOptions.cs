namespace PANiXiDA.Core.Presentation.Http.Options.Scalar;

internal sealed class ScalarOptions
{
    public const string SectionName = "ScalarConfiguration";

    public string? Title { get; set; }

    public string? Favicon { get; set; }

    public string[] BearerAuthenticationSchemes { get; set; } = [];
}
