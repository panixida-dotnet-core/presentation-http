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
- JSON numeric values use strict number handling.
- Module assemblies can be mapped to separate OpenAPI documents and Scalar sources for each API version through the `HttpModules` configuration section.
- Health checks are registered by `AddHttp` and exposed at `/health` by `UseHttp`.
- `IEndpointGroup` defines route, resource name, and API version metadata for Minimal API endpoint groups.
- `IEndpoint<TGroup>` defines route, name, and summary metadata for endpoints that belong to a specific group.
- The bundled Roslyn generator discovers endpoint types at compile time and emits constructor factories in deterministic type-name order.
- `EndpointConstants.EndpointPrefix` defines `/api/v{version:apiVersion}`.
- `ResultHttpMapper` maps `Result` and `Result<T>` to `IResult`.

## Requirements

- .NET 10 SDK 10.0.401 or later. The packaged generator uses Roslyn 5.9 and requires a compatible compiler in endpoint projects.
- ASP.NET Core Minimal API application.

## Installation

```xml
<ItemGroup>
  <PackageReference Include="PANiXiDA.Core.Presentation.Http" Version="3.0.0" />
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

Reference the package directly in each project that declares endpoints, and keep its `analyzers` assets enabled.
`AddHttp` retains the MVC-based API Versioning explorer and is not supported in a trimmed or Native AOT application; see [Native AOT](#native-aot) for the supported boundary.
`AddHttp` registers the standard validation infrastructure, including validation attributes on handler parameters. Also call `AddValidation()` in each assembly containing endpoint DTOs, so ASP.NET Core can generate and register metadata for their properties. A call inside this package cannot generate metadata for a consuming assembly. This is a source-generator discovery requirement, not an AOT incompatibility of `AddValidation`; see [validation across assemblies](https://learn.microsoft.com/aspnet/core/fundamentals/validation?view=aspnetcore-10.0#register-validation-across-assemblies).

## Forwarded Headers

The package configures these forwarded headers by default:

```csharp
ForwardedHeaders.XForwardedFor |
ForwardedHeaders.XForwardedHost |
ForwardedHeaders.XForwardedProto
```

The package also clears the default loopback-only `KnownIPNetworks` and `KnownProxies` restrictions so applications behind Kubernetes ingress or Gateway API proxies can process forwarded headers without per-service proxy registration.

Forwarded header names, original header names, `ForwardedHeaders`, `ForwardLimit`, `RequireHeaderSymmetry`, `AllowedHosts`, `KnownProxies`, and network lists can be supplied through a `ForwardedHeaders` section. Configuration values are applied directly to the standard `ForwardedHeadersOptions`, without an intermediate settings model. Scalar and string-array reads use generated binding; IP addresses and networks use explicit parsing, without reflection-based configuration binding.

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

`KnownProxies` contains IP address strings. `KnownIPNetworks` contains objects such as `{ "Prefix": "10.0.0.0", "PrefixLength": 8 }`; the legacy `KnownNetworks` key accepts the same shape. Invalid addresses or network prefixes fail when options are resolved instead of being silently ignored. Options reload notifications are preserved.

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
The registered HTTP module metadata is attached to custom groups as well. Routes without a version appear in every document of their module. A module containing only unversioned routes has one common document, such as `/openapi/identity.json`.

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

Groups and their endpoints must be in the same assembly. Concrete implementations must be accessible from generated code (public or internal, including accessible nested types), non-generic, and have one public constructor. For multiple public constructors, mark exactly one with `[ActivatorUtilitiesConstructor]`. Constructor dependencies are resolved from DI, including explicit `[FromKeyedServices(key)]` keys and optional parameter defaults. Missing required services still fail when endpoints are mapped. Constructor selection no longer depends on which services happen to be registered at runtime.

Abstract types and interfaces are ignored. Private, protected, file-local and open generic implementations, unsupported constructors, and endpoints targeting a group in another assembly produce `PANHTTPSG001`–`PANHTTPSG004` compiler errors. No runtime scanning or activation fallback is used if the analyzer is missing.

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

`EndpointMapBuilder` implements `IEndpointRouteBuilder`, so these are the standard ASP.NET Core `MapGet`, `MapPost`, `MapPut`, `MapPatch`, `MapDelete`, and `MapMethods` extensions. Pass `builder.Route` and the concrete handler to keep it visible to ASP.NET Core's Request Delegate Generator. For a method group, use `builder.MapGet(builder.Route, Handle)`; for multiple HTTP methods, use `builder.MapMethods(builder.Route, ["HEAD", "OPTIONS"], Handle)`.

The endpoint name and summary are applied automatically through an isolated route group without changing the URL or sibling endpoints. Standard ASP.NET Core metadata precedence applies: handler attributes and endpoint conventions can override these defaults. Calls such as `.RequireAuthorization()`, `.WithName()`, `.WithSummary()`, and `.AddEndpointFilter()` remain available. `Group` still exposes the original group; `ApplyMetadata` remains available for routes mapped directly on it.

## Migrating from 2.x

- Rebuild every endpoint assembly with the included analyzer. Runtime assembly scanning and `ActivatorUtilities` endpoint activation have been removed.
- Replace `builder.MapGet(handler)` with `builder.MapGet(builder.Route, handler)`, and likewise for other HTTP methods. `MapMethods` now takes `builder.Route` before the HTTP methods and handler. No separate metadata call is needed.
- Endpoint names and summaries are now route group defaults. Handler-level `EndpointName` and `EndpointSummary` attributes can override them; in 2.x the wrapper applied the endpoint properties after handler attributes. Explicit fluent overrides remain supported.
- Resolve ambiguous constructors explicitly with `[ActivatorUtilitiesConstructor]`. Private and open generic endpoint implementations now fail at compilation.
- `AddHttp` retains the standard validation infrastructure. Additionally call `AddValidation()` in each endpoint/DTO assembly to generate that assembly's model validation metadata. On .NET 10, use public request DTOs for automatic validation discovery; the smoke application checks that an invalid request returns `400`.
- Review configured proxy/network values: malformed values now fail explicitly. The existing configuration keys, defaults and reload behavior are retained.
- Core dependencies are Application `4.0.3`, ResultPattern `1.0.4`, and transitive Domain `3.0.1`.
- API Versioning is `10.2.3`, ASP.NET Core OpenAPI is `10.0.12`, and Scalar is `2.17.10`.
- OpenAPI documents are now separated by module and API version: replace links such as `/openapi/orders.json` with `/openapi/orders-v1.json` or `/openapi/orders-v2.json`. Without modules, use `/openapi/v1.json`, `/openapi/v2.json`, and so on. Configured module names and titles stay unchanged. A module containing only unversioned routes retains its common `/openapi/{module}.json` document.

OpenAPI uses API Versioning's `AddOpenApi()` integration and `WithDocumentPerVersion()`. Diagnostics `AV0029` and `AV0030` are no longer suppressed. The module description provider extends the upstream version descriptions with common documents for unversioned modules; it reads endpoint metadata without runtime type scanning.

## Native AOT

The runtime project enables `IsAotCompatible`. Our endpoint discovery and construction use generated registrations; configuration binding uses the .NET Roslyn generator. The registry uses assembly identity and module initialization, as in the Core EF registry, without scanning types or invoking constructors through reflection.

The standard `AddHttp` setup calls `Asp.Versioning.OpenApi` `10.2.3` and its transitive `Asp.Versioning.Mvc.ApiExplorer` `10.2.1`. The OpenAPI integration uses runtime assembly discovery, and `AddApiExplorer()` registers MVC. Both public `AddHttp` overloads propagate the trimming restriction with `RequiresUnreferencedCode`. A warning-free library build is not a claim that this dependency path is AOT-compatible.

One-time checks on September 26, 2026 used the packaged library, SDK `10.0.401`, ASP.NET Core `10.0.12`, Linux x64, `PublishAot=true`, Request Delegate Generator, and source-generated JSON metadata with reflection-based JSON disabled. The resulting native executables were run; successful publication alone was not treated as compatibility.

| Consumer setup | Result |
| --- | --- |
| Unmodified `AddHttp` / `UseHttp`, ordinary Release execution | All 19 checks passed, including separate module/version documents and an unversioned route shared by both versions. |
| Unmodified `AddHttp` / `UseHttp`, Native AOT | Registration failed: API Versioning's `AddOpenApi()` calls unsupported `Assembly.GetCallingAssembly()`. |
| Native AOT with an explicit empty `ApplicationPartManager`, diagnostic setup only | The same `Assembly.GetCallingAssembly()` failure; changing MVC application parts does not bypass it. |

The API checks covered generated registration, singleton/keyed/optional constructor injection, two API versions, unsupported versions, DTO/array JSON, valid and invalid request validation, parameter binding, exception ProblemDetails, sorting, forwarded headers, and options reload. OpenAPI checks covered module grouping, substituted version paths, and DTO schemas. Serving Scalar HTML does not establish that its OpenAPI document works.

Earlier diagnostic checks, before enabling the official OpenAPI integration, also identified MVC `ApplicationPartManager` dynamic assembly loading and `VersionedModelMetadataProvider` failures under AOT. The latter is tracked in [API Versioning issue #1226](https://github.com/dotnet/aspnet-api-versioning/issues/1226) and was reproduced on .NET 10. Removing those MVC components allowed the generated endpoint/configuration paths to pass 17 checks, but removed version-aware model metadata support. That result does not establish compatibility of the current full setup. Native compilation still reports MVC trimming/dynamic-code warnings, including `IL2026` and `IL3050`.

Full compatibility requires an explicit assembly/XML-document source instead of `Assembly.GetCallingAssembly()` and an AOT-safe versioned explorer for Minimal APIs, preserving route substitution, version-aware model metadata, module/version documents, and schema generation without MVC model discovery. Disabling model metadata is not a functionality-preserving fix. These are remaining dependency/integration changes, not fixes supplied by this version update.

The consuming application must enable the Request Delegate Generator in every endpoint project, register a `JsonSerializerContext` for its request/response DTOs, and register validation in the appropriate assembly. Source generation does not remove all reflection inside ASP.NET Core, DI, API Versioning, Scalar, or their generators; those are dependency-owned paths.

```xml
<PropertyGroup>
  <EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>
</PropertyGroup>
```

Compatibility probes are temporary and are not included as a test project or CI job. The checks do not certify every application-specific DTO, handler, or dependency feature.

Native compilation requires the platform toolchain, including Visual Studio C++ build tools on Windows. See the [ASP.NET Core Native AOT documentation](https://learn.microsoft.com/aspnet/core/fundamentals/native-aot?view=aspnetcore-10.0).

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

`AddHttp` configures strict JSON number handling. Numeric properties in JSON request bodies must be encoded as JSON numbers rather than quoted strings. This also keeps numeric OpenAPI schemas typed as `integer` or `number` instead of an `integer | string` or `number | string` union.

String properties and enums configured for string serialization are unaffected.

## OpenAPI

In `Development`, `UseHttp` exposes:

- OpenAPI documents at `/openapi/v1.json`, `/openapi/v2.json`, and so on for the mapped API versions;
- Scalar API reference at `/scalar`.

OpenAPI registration also enables Scalar transformers for Scalar-specific document extensions.

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

Each document includes only that module's endpoints for the selected version, plus its routes without a version, such as `/connect`. A module with only unversioned routes gets one common document, such as `/openapi/identity.json`, titled `Identity API`. Empty modules produce no documents. API route URLs and the endpoint mapping API are unchanged by document grouping.
Document names are compared case-insensitively, and a presentation assembly can belong to only one module.
Version names include minor versions and status suffixes when present, for example `identity-v1.1` or `identity-v2-beta`. When no modules are registered, documents are separated by version (`v1`, `v2`, and so on). An application with only unversioned endpoints and no modules uses `v1`.

The Scalar browser tab title can be configured from application configuration.
If the title is not configured or is blank, Scalar uses its default document title.

```json
{
  "ScalarConfiguration": {
    "Title": "Orders API Reference"
  }
}
```

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
  PANiXiDA.Core.Presentation.Http.Generators/
    Endpoints/
      ConstructorFactoryBuilder.cs
      EndpointRegistrationGenerator.cs
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
SonarQube analysis. Publishing from `main` requires all checks, including the SonarQube Quality Gate.

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
