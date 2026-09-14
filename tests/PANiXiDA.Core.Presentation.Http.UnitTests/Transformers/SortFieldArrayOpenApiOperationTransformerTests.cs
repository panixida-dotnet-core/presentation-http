using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using PANiXiDA.Core.Application.Querying.Sorting;
using PANiXiDA.Core.Presentation.Http.Transformers;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Transformers;

public sealed class SortFieldArrayOpenApiOperationTransformerTests
{
    [Fact(DisplayName = "Sorting transformer preserves custom descriptions and unrelated parameters")]
    public async Task TransformAsync_WhenParametersHaveMetadata_PreservesCustomizations()
    {
        var sorting = new OpenApiParameter { Name = "sort", In = ParameterLocation.Query, Description = "Custom sorting description" };
        var unrelated = new OpenApiParameter { Name = "page", In = ParameterLocation.Query, Required = true };
        var header = new OpenApiParameter { Name = "sort", In = ParameterLocation.Header, Required = true };
        var nameless = new OpenApiParameter { In = ParameterLocation.Query, Required = true };
        var reference = new OpenApiParameterReference("Shared");
        var operation = new OpenApiOperation { Parameters = [sorting, unrelated, header, nameless, reference] };
        var description = new ApiDescription();
        description.ParameterDescriptions.Add(new ApiParameterDescription { Name = "sort", Source = BindingSource.Query, Type = typeof(SortField[]) });
        description.ParameterDescriptions.Add(new ApiParameterDescription { Name = "page", Source = BindingSource.Query, Type = typeof(int) });
        description.ParameterDescriptions.Add(new ApiParameterDescription { Name = "sort", Source = BindingSource.Header, Type = typeof(SortField[]) });
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new OpenApiOperationTransformerContext { DocumentName = "v1", Description = description, ApplicationServices = services };

        await new SortFieldArrayOpenApiOperationTransformer().TransformAsync(operation, context, TestContext.Current.CancellationToken);

        sorting.Description.ShouldBe("Custom sorting description");
        sorting.Required.ShouldBeFalse();
        sorting.Schema!.Items!.Type.ShouldBe(JsonSchemaType.String);
        unrelated.Required.ShouldBeTrue();
        unrelated.Schema.ShouldBeNull();
        header.Required.ShouldBeTrue();
        header.Schema.ShouldBeNull();
        nameless.Required.ShouldBeTrue();
        nameless.Schema.ShouldBeNull();
        operation.Parameters[4].ShouldBeSameAs(reference);
    }

    [Theory(DisplayName = "Sorting transformer accepts operations without parameters")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransformAsync_WhenParametersAreAbsent_LeavesOperationUnchanged(bool emptyCollection)
    {
        var operation = new OpenApiOperation { Parameters = emptyCollection ? [] : null };
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new OpenApiOperationTransformerContext { DocumentName = "v1", Description = new ApiDescription(), ApplicationServices = services };

        await new SortFieldArrayOpenApiOperationTransformer().TransformAsync(operation, context, TestContext.Current.CancellationToken);

        (operation.Parameters?.Count ?? 0).ShouldBe(0);
    }
}
