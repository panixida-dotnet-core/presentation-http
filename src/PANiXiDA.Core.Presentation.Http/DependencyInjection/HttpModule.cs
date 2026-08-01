using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.DependencyInjection;

/// <summary>
/// Describes an HTTP presentation module and its OpenAPI document.
/// </summary>
public sealed class HttpModule
{
    /// <summary>
    /// Initializes a module that uses its document name as the Scalar display title.
    /// </summary>
    /// <param name="name">The unique OpenAPI document name.</param>
    /// <param name="presentationAssembly">The assembly containing the module endpoint groups.</param>
    public HttpModule(string name, Assembly presentationAssembly)
        : this(name, name, presentationAssembly)
    {
    }

    /// <summary>
    /// Initializes a module with an explicit Scalar display title.
    /// </summary>
    /// <param name="name">The unique OpenAPI document name.</param>
    /// <param name="title">The document title displayed by Scalar.</param>
    /// <param name="presentationAssembly">The assembly containing the module endpoint groups.</param>
    public HttpModule(
        string name,
        string title,
        Assembly presentationAssembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(presentationAssembly);

        Name = name;
        Title = title;
        PresentationAssembly = presentationAssembly;
    }

    /// <summary>
    /// Gets the unique OpenAPI document name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the document title displayed by Scalar.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the assembly containing the module endpoint groups.
    /// </summary>
    public Assembly PresentationAssembly { get; }
}
