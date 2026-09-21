using Asp.Versioning;

using PANiXiDA.Core.Presentation.Http.Endpoints;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PANiXiDA.Core.Presentation.Http.AotSmoke;

internal sealed class ProbeGroup : IEndpointGroup
{
    public string Route => "/probe";
    public string Name => "Probe";
    public ApiVersion ApiVersion => new(1, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMapper.MapGroupEndpoints<ProbeGroup>(endpoints);
    }
}

internal sealed class GetProbeEndpoint(ProbeDependency dependency) : IEndpoint<ProbeGroup>
{
    public string Route => "/{id:int}";
    public string Name => "ProbeGet";
    public string Summary => "Gets a generated endpoint response.";

    public void Map(EndpointMapBuilder builder)
    {
        var group = builder.Group;
        var route = group.MapGet(builder.Route, (int id) => TypedResults.Ok(new ProbeResponse(id, dependency.Value)));
        builder.ApplyMetadata(route);
    }
}

internal sealed class PostProbeEndpoint : IEndpoint<ProbeGroup>
{
    public string Route => "";
    public string Name => "ProbePost";
    public string Summary => "Validates a request using generated metadata.";

    public void Map(EndpointMapBuilder builder)
    {
        var group = builder.Group;
        var route = group.MapPost(builder.Route, (ProbeRequest request) => TypedResults.Ok(new ProbeResponse(1, request.Name)));
        builder.ApplyMetadata(route);
    }
}

internal sealed class ErrorProbeEndpoint : IEndpoint<ProbeGroup>
{
    public string Route => "/error";
    public string Name => "ProbeError";
    public string Summary => "Exercises the exception middleware.";

    public void Map(EndpointMapBuilder builder)
    {
        var group = builder.Group;
        var route = group.MapGet(builder.Route, Fail);
        builder.ApplyMetadata(route);
    }

    private static IResult Fail()
    {
        throw new InvalidOperationException("Probe error.");
    }
}

internal sealed record ProbeDependency(string Value);
internal sealed record ProbeResponse(int Id, string Name);
public sealed record ProbeRequest([property: Required] string Name);

[JsonSerializable(typeof(ProbeResponse))]
[JsonSerializable(typeof(ProbeRequest))]
internal partial class ProbeJsonContext : JsonSerializerContext;
