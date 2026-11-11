namespace PANiXiDA.Core.Presentation.Http.Options.Scalar;

internal sealed class ScalarOptions
{
    public const string SectionName = "ScalarConfiguration";

    public bool? Enabled { get; set; }

    public string[] BearerAuthenticationSchemes { get; set; } = [];
}
