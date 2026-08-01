using PANiXiDA.Core.Presentation.Http.DependencyInjection;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class HttpModuleTests
{
    [Fact(DisplayName = "HTTP module uses the document name as the default title")]
    public void Constructor_ShouldUseNameAsDefaultTitle()
    {
        var assembly = typeof(HttpModuleTests).Assembly;

        var module = new HttpModule("identity", assembly);

        module.Name.ShouldBe("identity");
        module.Title.ShouldBe("identity");
        module.PresentationAssembly.ShouldBeSameAs(assembly);
    }

    [Fact(DisplayName = "HTTP module keeps the explicit document title")]
    public void Constructor_ShouldUseExplicitTitle()
    {
        var assembly = typeof(HttpModuleTests).Assembly;

        var module = new HttpModule("identity", "Identity API", assembly);

        module.Name.ShouldBe("identity");
        module.Title.ShouldBe("Identity API");
        module.PresentationAssembly.ShouldBeSameAs(assembly);
    }

    [Fact(DisplayName = "HTTP module rejects an empty document name")]
    public void Constructor_ShouldRejectEmptyName()
    {
        var assembly = typeof(HttpModuleTests).Assembly;

        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = new HttpModule(" ", assembly);
        });

        exception.ParamName.ShouldBe("name");
    }

    [Fact(DisplayName = "HTTP module rejects an empty document title")]
    public void Constructor_ShouldRejectEmptyTitle()
    {
        var assembly = typeof(HttpModuleTests).Assembly;

        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = new HttpModule("identity", " ", assembly);
        });

        exception.ParamName.ShouldBe("title");
    }

    [Fact(DisplayName = "HTTP module rejects a missing presentation assembly")]
    public void Constructor_ShouldRejectMissingPresentationAssembly()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            _ = new HttpModule("identity", null!);
        });

        exception.ParamName.ShouldBe("presentationAssembly");
    }
}
