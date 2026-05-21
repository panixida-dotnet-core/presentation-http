using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Support;

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "TestApplication";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
