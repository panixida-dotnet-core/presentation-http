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
- `UseHttp` adds the default middleware pipeline and maps source-generated endpoint registrations.
- HTTP JSON contracts enforce required constructor parameters, nullable annotations, and strict number handling.
- Module assemblies can be mapped to separate OpenAPI documents and Scalar sources for each API version through the `HttpModules` configuration section.
- Health checks are registered by `AddHttp` and exposed at `/health` by `UseHttp`.
- `IEndpointGroup` defines route, resource name, and API version metadata for Minimal API endpoint groups.
- `IEndpoint<TGroup>` defines route, name, and summary metadata for endpoints that belong to a specific group.
- The bundled source generator registers endpoints in deterministic type-name order.
- `EndpointConstants.EndpointPrefix` defines `/api/v{version:apiVersion}`.
- `ResultHttpMapper` maps `Result` and `Result<T>` to `IResult`.

## Requirements

- .NET 10 SDK 10.0.401 or later.
- ASP.NET Core Minimal API application.

Full Native AOT support is currently blocked by API Versioning's OpenAPI/MVC integration.

## Installation

Reference the package in each endpoint project with its analyzer assets enabled.

```xml
<ItemGroup>
  <PackageReference Include="PANiXiDA.Core.Presentation.Http" Version="4.0.0" />
</ItemGroup>
```

## Quick Start

```csharp
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttp(builder.Configuration);
builder.Services.AddValidation();

var app = builder.Build();

app.UseHttp(typeof(Program).Assembly);

app.Run();
```

Call `AddValidation()` in each endpoint/DTO assembly to generate validation metadata for its DTO properties. `AddHttp` registers the shared validation services.

## Forwarded Headers

The package configures these forwarded headers by default:

```csharp
ForwardedHeaders.XForwardedFor |
ForwardedHeaders.XForwardedHost |
ForwardedHeaders.XForwardedProto
```

The package also clears the default loopback-only `KnownIPNetworks` and `KnownProxies` restrictions so applications behind Kubernetes ingress or Gateway API proxies can process forwarded headers without per-service proxy registration.

Configure the standard `ForwardedHeadersOptions` values through the `ForwardedHeaders` section.

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

`KnownIPNetworks` and the legacy `KnownNetworks` key accept objects such as `{ "Prefix": "10.0.0.0", "PrefixLength": 8 }`. Both fields are required; an explicit `PrefixLength: 0` is allowed. Invalid addresses and networks are rejected. Restart the application to apply forwarded-header configuration changes.

For stricter trust boundaries, configure `ForwardedHeadersOptions` directly after `AddHttp`.

```csharp
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

builder.Services.AddHttp(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
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

Groups that require a custom root route can map their endpoints through an explicit `RouteGroupBuilder`.
Custom groups retain their HTTP module metadata for OpenAPI documents.

```csharp
public void Map(IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/connect")
        .WithTags(Name);

    EndpointMapper.MapGroupEndpoints<OAuthEndpointGroup>(
        group,
        endpoints.ServiceProvider);
}
```

## Endpoints

An endpoint implements `IEndpoint<TGroup>`, where `TGroup` is the endpoint group it belongs to.
Endpoint metadata is declared as public properties so it can be required by the interface and applied by `EndpointMapper`.

Keep each group and its endpoints in the same assembly. Use non-generic `public` or `internal` classes with one public constructor; for multiple constructors, mark one with `[ActivatorUtilitiesConstructor]`. Dependencies are resolved from DI, including keyed services and optional parameters.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.Presentation.Http.Endpoints;

public sealed class GetOrderEndpoint : IEndpoint<OrdersEndpointGroup>
{
    public string Route { get; } = "/{id:guid}";

    public string Name { get; } = "GetOrder";

    public string Summary { get; } = "Gets an order by identifier.";

    public void Map(EndpointMapBuilder builder)
    {
        builder.MapGet(builder.Route, (Guid id) =>
        {
            return TypedResults.Ok(new OrderResponse(id));
        });
    }
}

public sealed record OrderResponse(Guid Id);
```

Use standard ASP.NET Core mapping methods, for example `builder.MapGet(builder.Route, Handle)` or `builder.MapMethods(builder.Route, ["HEAD", "OPTIONS"], Handle)`. Name and summary are applied automatically; standard endpoint conventions can override them.

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

Invalid HTTP requests represented by `BadHttpRequestException`, including JSON body binding failures, preserve their framework status code and are mapped to `ProblemDetails`.
Other unhandled exceptions are mapped to status 500 in every environment.
In `Development`, both responses include the exception message in `detail`.

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

## JSON

`AddHttp` enables `RespectRequiredConstructorParameters`, `RespectNullableAnnotations`, and strict number handling for Minimal API JSON requests and responses. Missing constructor parameters without defaults and explicit `null` for non-nullable reference properties or parameters produce HTTP `400`.

Use defaults for optional constructor parameters:

```csharp
public sealed record CreateRequest(
    string Name,
    string? RequiredNote,
    int? Shots = null);
```

`name` and `requiredNote` must be present. `name` must not be `null`; `requiredNote` may be `null`. `shots` may be omitted or set to `null`. For property-based DTOs, use `required` or `[JsonRequired]` to require presence.

Non-nullable reference properties in responses must contain non-null values. Validate collection elements, generic members, and business rules separately. Send numbers as JSON numbers; enums use strings. These settings do not affect form/query binding or separately configured serializers.

## OpenAPI

In `Development`, `UseHttp` exposes:

- OpenAPI documents at `/openapi/v1.json`, `/openapi/v2.json`, and so on for the mapped API versions;
- Scalar API reference at `/scalar`.

OpenAPI registration also enables Scalar transformers for Scalar-specific document extensions.

### Bearer authentication

`AddHttp` adds an HTTP `Bearer` security requirement to operations whose resolved authorization
policy accepts a Bearer authentication scheme. This enables token input and authenticated requests
in Scalar. Enter the access token without the `Bearer` prefix; Scalar sends it as
`Authorization: Bearer <access_token>`.

Endpoint and route-group metadata (`RequireAuthorization`, `[Authorize]`, and explicit policies)
are combined with named, default, and fallback policies through ASP.NET Core's policy provider.
When the resulting policy does not select authentication schemes, the default authenticate scheme
is used. `AllowAnonymous` endpoints are excluded. Cookie-only policies and other unmapped schemes
do not receive a Bearer requirement, even when Bearer is the host's default scheme.

The standard scheme names `Bearer` and `BearerToken` are recognized by convention.
Declare additional Bearer scheme names in `ScalarConfiguration:BearerAuthenticationSchemes`.
For an OpenIddict host using validation for API requests and the server handler for UserInfo:

```json
{
  "ScalarConfiguration": {
    "BearerAuthenticationSchemes": [
      "OpenIddict.Validation.AspNetCore",
      "OpenIddict.Server.AspNetCore"
    ]
  }
}
```

Names are case-sensitive and must match the host's authentication registrations. Include a forwarding
policy scheme only if it accepts Bearer tokens. The library does not infer protocols from custom
handlers or forwarding selectors, and it does not generate documentation for Cookie or certificate
authentication. The Bearer component is added only to documents with matching operations, including
versioned and unversioned module documents.

The host must configure authentication, authorization services, and middleware. This configuration
only maps authentication scheme names to OpenAPI Bearer security; it does not register handlers or
change server-side access checks. Both JWT and opaque tokens are supported without prescribing a
token format.

### Dynamic sorting

Accept `SortingParameters` directly with `[AsParameters]` and pass it to the application layer:

```csharp
using Microsoft.AspNetCore.Http;
using PANiXiDA.Core.Application.Querying.Sorting;

app.MapGet("/users", ([AsParameters] SortingParameters sorting) =>
{
    return TypedResults.Ok(sorting.Fields);
});
```

Requests use repeated query parameters: `?Fields=name:desc&Fields=department.name:asc`.
Omitting `Fields` produces empty sorting. Directions default to `asc`.

`AddHttp` describes sorting as an optional string array with `style: form` and `explode: true`
in every OpenAPI document. Swagger, Scalar, and generated clients can send the repeated values;
client generators expose `Fields` as a string collection. JSON request and response schemas remain unchanged.

### Module documents

Applications composed from multiple presentation modules expose one OpenAPI document per module and API version.
Register the presentation assemblies in code and configure their document names and display titles in `appsettings.json`.
`UseHttp` automatically maps endpoint groups from registered module assemblies.

```csharp
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttp(
    builder.Configuration,
    typeof(IdentityPresentationAssembly).Assembly,
    typeof(CompendiumPresentationAssembly).Assembly);

var app = builder.Build();

app.UseHttp();

app.Run();
```

Each key under `HttpModules` must match the simple name returned by `Assembly.GetName().Name`.
Both `Name` and `Title` are required for every registered module assembly.

```json
{
  "HttpModules": {
    "PANiXiDA.TacticalHeroes.Identity.Presentation": {
      "Name": "identity",
      "Title": "Identity API"
    },
    "PANiXiDA.TacticalHeroes.Compendium.Presentation": {
      "Name": "compendium",
      "Title": "Compendium API"
    }
  }
}
```

For Identity endpoints in versions 1 and 2 and Compendium endpoints in version 1, this configuration exposes:

- `/openapi/identity-v1.json` with the title `Identity API v1`;
- `/openapi/identity-v2.json` with the title `Identity API v2`;
- `/openapi/compendium-v1.json` with the title `Compendium API v1`;
- `/scalar` with a selector for these three documents.

Routes without a version appear in every document of their module. A module with only unversioned routes uses a common document, such as `/openapi/identity.json`. Empty modules produce no documents.
Final document names must be unique ignoring case, including common module documents and versioned documents. A presentation assembly can belong to only one module.
Without modules, documents follow visible API versions (`v1`, `v2`, and so on); when only unversioned endpoints are visible, they use the configured default version (`v1` by default).

The Scalar browser tab title and favicon can be configured from application configuration.
If either value is missing or blank, Scalar keeps its corresponding default.

```json
{
  "ScalarConfiguration": {
    "Title": "Orders API Reference",
    "Favicon": "/favicon.ico"
  }
}
```

For a local favicon, place the file in the consuming web application's `wwwroot` directory.
A nonblank `Favicon` also makes `UseHttp` call `MapStaticAssets()` in `Development`.
This uses the consuming application's static assets manifest, generated by `Microsoft.NET.Sdk.Web`,
and maps all assets in that manifest. The HTTP package does not include the application's icon.
The static asset endpoints use `ShortCircuit()`: matching public assets are served during routing,
without running middleware placed after `UseRouting()`. Scalar HTML and API endpoints still use the normal pipeline.
Only use this automatic mapping for public assets; authorization and CORS middleware after routing do not run for them.

Place middleware required for static assets before an explicit `UseRouting()` call in the host.
For example, when the host uses authentication:

```csharp
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttp();
```

Place HSTS, exception handling, and required response-header middleware before routing as well.
`UseHttp` keeps its existing middleware registrations and does not move the host's routing or authentication.
Without explicit routing, ASP.NET Core inserts routing before the application's middleware,
so short-circuited assets also bypass the middleware registered by `UseHttp`.

`WebApplication.CreateBuilder` initializes static web assets in `Development` automatically.
When using `WebApplication.CreateSlimBuilder`, explicitly call `UseStaticWebAssets()` before `Build()`:

```csharp
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseStaticWebAssets();
}

builder.Services.AddHttp(builder.Configuration);
var app = builder.Build();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseHttp();
await app.RunAsync();
```

Without this initialization, a slim host can return an empty `200 OK` for the favicon during development
([ASP.NET Core issue #58986](https://github.com/dotnet/aspnetcore/issues/58986)).
`UseHttp` maps endpoints after `Build()` and does not replace this host initialization.

OpenAPI, Scalar, and this automatic static asset mapping are not enabled outside `Development`.
Applications that also serve static assets in other environments should call `MapStaticAssets()` themselves.

## API Versioning

The package configures URL segment API versioning:

```text
/api/v1/orders
```

The default API version is `1.0`, and the version must be present in the route.

## Project Structure

```text
src/
  PANiXiDA.Core.Presentation.Http.Generators/
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

### Continuous integration

Every pull request and push to `main` runs formatting, tests, and mandatory
SonarQube analysis. Publishing from `main` starts only after the SonarQube
Quality Gate succeeds.

## Package Contents

The NuGet package includes:

- compiled library for `net10.0`;
- the Roslyn generator for `netstandard2.0`, packaged under `analyzers/dotnet/cs`;
- XML documentation;
- README;
- package icon;
- Source Link metadata;
- symbols package when packed with repository settings.

## License

This project is licensed under the Apache-2.0 license. See [LICENSE](LICENSE) for details.
