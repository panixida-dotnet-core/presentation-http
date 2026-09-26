using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using PANiXiDA.Core.Application.Querying.Sorting;

namespace PANiXiDA.Core.Presentation.Http.Transformers;

internal sealed class SortFieldArrayOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string ParameterDescription =
        $"Repeat this query parameter for multiple criteria, in order of precedence. Format: field[:{nameof(SortDirection.Asc).ToLowerInvariant()}|{nameof(SortDirection.Desc).ToLowerInvariant()}]. "
        + $"Direction defaults to {nameof(SortDirection.Asc).ToLowerInvariant()}. Field paths and directions are case-insensitive; nested paths use dots.";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var names = context.Description.ParameterDescriptions
            .Where(parameter => parameter.Source == BindingSource.Query && parameter.Type == typeof(SortField[]))
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parameter.In != ParameterLocation.Query || parameter.Name is not { } name || !names.Contains(name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(parameter.Description))
            {
                parameter.Description = ParameterDescription;
            }
            parameter.Required = false;
            parameter.Style = ParameterStyle.Form;
            parameter.Explode = true;
            parameter.Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String }
            };
        }

        return Task.CompletedTask;
    }
}
