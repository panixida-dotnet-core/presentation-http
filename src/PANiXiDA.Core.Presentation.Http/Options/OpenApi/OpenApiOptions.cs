namespace PANiXiDA.Core.Presentation.Http.Options.OpenApi;

internal sealed class OpenApiOptions
{
    public const string SectionName = "ScalarConfiguration";

    public bool? Enabled { get; set; }

    public string[] BearerAuthenticationSchemes { get; set; } = [];
}
