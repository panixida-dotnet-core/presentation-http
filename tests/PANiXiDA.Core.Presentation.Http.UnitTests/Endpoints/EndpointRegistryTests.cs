using Microsoft.AspNetCore.Builder;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

public sealed class EndpointRegistryTests
{
    [Fact(DisplayName = "Generated endpoint registration rejects null arguments")]
    public void RegisterAssembly_ShouldRejectNullArguments()
    {
        var assembly = typeof(EndpointRegistryTests).Assembly;

        Should.Throw<ArgumentNullException>(() => EndpointRegistry.RegisterAssembly(null!, _ => { }, (_, _) => null!, (_, _) => []))
            .ParamName.ShouldBe("assembly");
        Should.Throw<ArgumentNullException>(() => EndpointRegistry.RegisterAssembly(assembly, null!, (_, _) => null!, (_, _) => []))
            .ParamName.ShouldBe("mapGroups");
        Should.Throw<ArgumentNullException>(() => EndpointRegistry.RegisterAssembly(assembly, _ => { }, null!, (_, _) => []))
            .ParamName.ShouldBe("createGroup");
        Should.Throw<ArgumentNullException>(() => EndpointRegistry.RegisterAssembly(assembly, _ => { }, (_, _) => null!, null!))
            .ParamName.ShouldBe("createEndpoints");
    }

    [Fact(DisplayName = "Generated endpoint registrations reject duplicates")]
    public void RegisterAssembly_ShouldRejectDuplicateAssembly()
    {
        var assembly = typeof(EndpointRegistryTests).Assembly;

        Should.Throw<ArgumentException>(() => EndpointRegistry.RegisterAssembly(assembly, _ => { }, (_, _) => null!, (_, _) => []));
    }

    [Fact(DisplayName = "Missing endpoint generation fails with an actionable message")]
    public void MapGroups_ShouldRejectMissingRegistration()
    {
        using var app = WebApplication.CreateBuilder().Build();

        var exception = Should.Throw<InvalidOperationException>(() => EndpointRegistry.MapGroups(app, typeof(string).Assembly));

        exception.Message.ShouldContain("Generated endpoint registration was not found");
        exception.Message.ShouldContain("with its analyzers");
        Should.Throw<ArgumentNullException>(() => EndpointRegistry.MapGroups(null!, typeof(string).Assembly)).ParamName.ShouldBe("endpoints");
        Should.Throw<ArgumentNullException>(() => EndpointRegistry.MapGroups(app, null!)).ParamName.ShouldBe("assembly");
    }

    [Fact(DisplayName = "Generated registration supports groups without endpoints and rejects unknown factories")]
    public void GeneratedRegistration_ShouldHandleEmptyGroups()
    {
        using var app = WebApplication.CreateBuilder().Build();

        EndpointRegistry.CreateEndpoints<ADiscoveredEndpointGroup>(app.Services).ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => EndpointRegistry.CreateGroup<IEndpointGroup>(app.Services))
            .Message.ShouldContain("Generated factory was not found");
    }
}
