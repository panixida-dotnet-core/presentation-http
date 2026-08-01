namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal sealed class HttpModuleMetadata
{
    internal HttpModuleMetadata(string name)
    {
        Name = name;
    }

    internal string Name { get; }
}
