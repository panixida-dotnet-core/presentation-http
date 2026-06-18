# PANiXiDA.Core.Presentation.Http

`PANiXiDA.Core.Presentation.Http` is a reusable ASP.NET Core HTTP presentation package for PANiXiDA applications.

It provides common Minimal API endpoint conventions, API versioning, OpenAPI setup, Problem Details handling, health checks, request logging, exception handling, forwarded headers configuration, and helpers for mapping `PANiXiDA.Core.ResultPattern` results to HTTP responses.

## Status

[![CI](https://github.com/panixida-dotnet-core/presentation-http/actions/workflows/ci.yml/badge.svg)](https://github.com/panixida-dotnet-core/presentation-http/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PANiXiDA.Core.Presentation.Http.svg)](https://www.nuget.org/packages/PANiXiDA.Core.Presentation.Http)
[![NuGet downloads](https://img.shields.io/nuget/dt/PANiXiDA.Core.Presentation.Http.svg)](https://www.nuget.org/packages/PANiXiDA.Core.Presentation.Http)
[![Target Framework](https://img.shields.io/badge/target-net10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/panixida-dotnet-core/presentation-http.svg)](LICENSE)

## Features

- `AddHttp` registers the default HTTP presentation services.
- `UseHttp` adds the default middleware pipeline and maps discovered endpoint groups.
- Health checks are registered by `AddHttp` and exposed at `/health` by `UseHttp`.
- `IEndpointGroup` defines route, resource name, and API version metadata for Minimal API endpoint groups.
- `IEndpoint<TGroup>` defines route, name, and summary metadata for endpoints that belong to a specific group.
- `EndpointMapper` discovers and maps endpoints in a deterministic type-name order.
- `EndpointConstants.EndpointPrefix` defines `/api/v{version:apiVersion}`.
- `ResultHttpMapper` maps `Result` and `Result<T>` to `IResult`.

## Requirements

- .NET 10 SDK.
- ASP.NET Core Minimal API application.

## Installation

```xml
<ItemGroup>
  <PackageReference Include="PANiXiDA.Core.Presentation.Http" Version="2.0.0-preview" />
</ItemGroup>
```

## Quick Start

```csharp
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttp(builder.Configuration);

var app = builder.Build();

app.UseHttp(typeof(Program).Assembly);

app.Run();
```

Pass `null` to `AddHttp` if forwarded headers should use the package defaults.

```csharp
builder.Services.AddHttp(configuration: null);
```

## Forwarded Headers

The package configures these forwarded headers by default:

```csharp
ForwardedHeaders.XForwardedFor |
ForwardedHeaders.XForwardedHost |
ForwardedHeaders.XForwardedProto
```

Additional values can be bound from the standard ASP.NET Core `ForwardedHeadersOptions` model by adding a `ForwardedHeaders` section to the application configuration.

```json
{
  "ForwardedHeaders": {
    "ForwardedHeaders": "XForwardedFor, XForwardedHost, XForwardedProto",
    "ForwardLimit": 2,
    "RequireHeaderSymmetry": true,
    "AllowedHosts": [
      "api.example.com"
    ]
  }
}
```

For advanced scenarios, configure `ForwardedHeadersOptions` directly after `AddHttp`.

```csharp
using Microsoft.AspNetCore.HttpOverrides;

builder.Services.AddHttp(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});
```

## Health Checks

`AddHttp` registers ASP.NET Core health check services, and `UseHttp` maps the health check endpoint at `/health`.

```text
GET /health
```

Services can add their own checks after `AddHttp`.

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());
```

## Endpoint Groups

An endpoint group owns a route prefix, resource name, API version, and the call to map endpoints that belong to the group.

```csharp
using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

public sealed class OrdersEndpointGroup : IEndpointGroup
{
    public string Route { get; } = "/orders";

    public string Name { get; } = "Orders";

    public ApiVersion ApiVersion { get; } = new(1, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMapper.MapGroupEndpoints<OrdersEndpointGroup>(endpoints);
    }
}
```

The final route prefix is `/api/v{version}/orders`.

## Endpoints

An endpoint implements `IEndpoint<TGroup>`, where `TGroup` is the endpoint group it belongs to.
Endpoint metadata is declared as public properties so it can be required by the interface and applied by `EndpointMapper`.

```csharp
using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.Presentation.Http.Endpoints;

public sealed class GetOrderEndpoint : IEndpoint<OrdersEndpointGroup>
{
    public string Route { get; } = "/{id:guid}";

    public string Name { get; } = "GetOrder";

    public string Summary { get; } = "Gets an order by identifier.";

    public void Map(EndpointMapBuilder builder)
    {
        builder.MapGet((Guid id) =>
            {
                return TypedResults.Ok(new OrderResponse(id));
            });
    }
}

public sealed record OrderResponse(Guid Id);
```

## Result Mapping

Successful results are mapped through the provided success factory.

```csharp
using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.Presentation.Http.Helpers;
using PANiXiDA.Core.ResultPattern;

public static IResult GetOrder(Guid id)
{
    Result<OrderResponse> result = Result.Success(new OrderResponse(id));

    return result.ToHttpResult(value =>
    {
        return TypedResults.Ok(value);
    });
}
```

Failed results are mapped to `ProblemDetails` or `ValidationProblem`.

```csharp
using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.Presentation.Http.Helpers;
using PANiXiDA.Core.ResultPattern;

public static IResult CreateOrder()
{
    Result result = Result.Failure(Error.Validation("Email is required").WithField("Email"));

    return result.ToHttpProblem();
}
```

## HTTP Error Mapping

Unhandled exceptions are mapped to `ProblemDetails` in every environment.
In `Development`, the response includes the exception message in `detail`.

| Error type | HTTP status | Title |
| --- | ---: | --- |
| `Validation` | 400 | `One or more validation errors occurred.` |
| `NotFound` | 404 | `Resource not found` |
| `Conflict` | 409 | `Conflict` |
| `Unauthorized` | 401 | `Unauthorized` |
| `Forbidden` | 403 | `Forbidden` |
| `Failure` | 400 | `Request failed` |
| `Unexpected` | 500 | `Server error` |

Validation error fields are used as `ValidationProblem` keys. If a validation error has no field, the key is `general`.

## OpenAPI

In `Development`, `UseHttp` exposes:

- OpenAPI document at `/openapi/v1.json`;
- Scalar API reference at `/scalar`.

OpenAPI registration also enables Scalar transformers for Scalar-specific document extensions.

OpenAPI is not mapped automatically outside `Development`.

## API Versioning

The package configures URL segment API versioning:

```text
/api/v1/orders
```

The default API version is `1.0`, and the version must be present in the route.

## Project Structure

```text
src/
  PANiXiDA.Core.Presentation.Http/
    Configurations/
    DependencyInjection/
    Endpoints/
    Helpers/
    Middlewares/
tests/
  PANiXiDA.Core.Presentation.Http.UnitTests/
```

## Development

Run the standard validation before publishing:

```powershell
dotnet restore
dotnet format
dotnet build --configuration Release
dotnet test --configuration Release
dotnet pack --configuration Release
```

Run coverage:

```powershell
dotnet test --configuration Release -- --coverage --coverage-output coverage.xml --coverage-output-format xml
```

The source files under `src/PANiXiDA.Core.Presentation.Http` are covered by unit tests. Coverage excludes generated files under `obj/` from ASP.NET Core and validation source generators.

## Package Contents

The NuGet package includes:

- compiled library for `net10.0`;
- XML documentation;
- README;
- package icon;
- Source Link metadata;
- symbols package when packed with repository settings.

## License

This project is licensed under the Apache-2.0 license. See [LICENSE](LICENSE) for details.
