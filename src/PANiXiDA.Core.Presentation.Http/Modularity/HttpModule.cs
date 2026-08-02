using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.Modularity;

internal sealed class HttpModule
{
    internal HttpModule(
        string name,
        string title,
        Assembly presentationAssembly)
    {
        Name = name;
        Title = title;
        PresentationAssembly = presentationAssembly;
    }

    internal string Name { get; }

    internal string Title { get; }

    internal Assembly PresentationAssembly { get; }
}
