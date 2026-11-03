using System.Text.Json.Nodes;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Grundet.Api.Tests;

public sealed class OpenApiSchemaIntegrationTests
{
    [Fact]
    public async Task OpenApiDocument_ExposesTypedSchemasForPartialUpdateBodies()
    {
        var document = await GetOpenApiDocumentAsync();
        var schemas = document["components"]?["schemas"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI document did not contain component schemas.");

        AssertSchemaHasProperties(schemas, "UpdateFeatureRequest", "name", "description");
        AssertSchemaHasProperties(schemas, "UpdateProjectRequest", "name", "description");
        AssertSchemaHasProperties(
            schemas,
            "UpdateRequirementRequest",
            "text",
            "status",
            "priority",
            "labels",
            "acceptanceCriteria",
            "rationale",
            "reason",
            "targetFeatureId");
        AssertSchemaHasProperties(
            schemas,
            "UpdateWorkItemRequest",
            "title",
            "description",
            "status",
            "priority",
            "labels",
            "assignedToUserId",
            "blockedReason",
            "reason",
            "type");
        AssertSchemaHasProperties(
            schemas,
            "BatchRequirementSaveRequest",
            "requirementId",
            "text",
            "status",
            "priority",
            "labels",
            "acceptanceCriteria",
            "rationale",
            "reason",
            "targetFeatureId");

        // Create flow carries the durable Rationale (and no longer the change Reason — ADR-006).
        AssertSchemaHasProperties(schemas, "CreateRequirementRequest", "text", "rationale");

        var batchSaveRequest = schemas["BatchSaveRequirementsRequest"]?.AsObject()
            ?? throw new InvalidOperationException("BatchSaveRequirementsRequest schema was missing.");
        Assert.NotNull(batchSaveRequest["properties"]?["saves"]?["items"]);
    }

    [Fact]
    public async Task OpenApiDocument_ExposesTypedGeneratedClientRouteContracts()
    {
        var document = await GetOpenApiDocumentAsync();
        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI document did not contain paths.");

        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/teams/{teamId}/projects/{projectId}",
            "put",
            "UpdateProjectRequest");
        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/projects/{projectId}/features/{featureId}",
            "put",
            "UpdateFeatureRequest");
        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/projects/{projectId}/requirements/{requirementId}",
            "put",
            "UpdateRequirementRequest");
        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/projects/{projectId}/work-items/{workItemId}",
            "put",
            "UpdateWorkItemRequest");
        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/projects/{projectId}/requirements/batch-save",
            "post",
            "BatchSaveRequirementsRequest");
        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/teams/{teamId}/jira/project",
            "put",
            "SetJiraProjectRequest");
        AssertOperationRequestBodySchema(
            paths,
            "/api/v1/projects/{projectId}/jira/project",
            "put",
            "SetJiraProjectRequest");

        AssertOperationQueryParameters(
            paths,
            "/api/v1/teams/{teamId}/projects",
            "get",
            "page",
            "pageSize",
            "lastSeenId");
        AssertOperationQueryParameters(
            paths,
            "/api/v1/projects/{projectId}/features",
            "get",
            "page",
            "pageSize",
            "lastSeenId");
        AssertOperationQueryParameters(
            paths,
            "/api/v1/features/{featureId}/requirements",
            "get",
            "status",
            "priority",
            "labels",
            "page",
            "pageSize",
            "q",
            "sort");
        AssertOperationQueryParameters(
            paths,
            "/api/v1/projects/{projectId}/requirements",
            "get",
            "status",
            "priority",
            "labels",
            "page",
            "pageSize",
            "q",
            "sort",
            "featureId");
        AssertOperationQueryParameters(
            paths,
            "/api/v1/projects/{projectId}/work-items",
            "get",
            "status",
            "priority",
            "labels",
            "assignedTo",
            "blocked",
            "featureId",
            "page",
            "pageSize",
            "q",
            "sort");
        AssertOperationQueryParameters(
            paths,
            "/api/v1/projects/{projectId}/search",
            "get",
            "query",
            "q");
        AssertOperationQueryParameters(
            paths,
            "/api/v1/projects/{projectId}/changelog",
            "get",
            "entityTypes",
            "changeTypes",
            "featureId",
            "authorId",
            "from",
            "to",
            "page",
            "pageSize");
    }

    private static async Task<JsonNode> GetOpenApiDocumentAsync()
    {
        using var factory = new GrundetApiFactory(
            "Host=invalid;Port=5432;Database=invalid;Username=invalid;Password=invalid",
            new TestEmailSink(),
            Path.GetTempPath(),
            Path.GetTempPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        return JsonNode.Parse(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("OpenAPI document response was empty.");
    }

    private static void AssertSchemaHasProperties(
        JsonObject schemas,
        string schemaName,
        params string[] propertyNames)
    {
        var schema = schemas[schemaName]?.AsObject()
            ?? throw new InvalidOperationException($"Schema '{schemaName}' was missing.");
        var properties = schema["properties"]?.AsObject()
            ?? throw new InvalidOperationException($"Schema '{schemaName}' did not contain properties.");

        foreach (string propertyName in propertyNames)
        {
            Assert.True(
                properties.ContainsKey(propertyName),
                $"Schema '{schemaName}' did not contain property '{propertyName}'.");
        }
    }

    private static void AssertOperationRequestBodySchema(
        JsonObject paths,
        string path,
        string method,
        string schemaName)
    {
        var operation = GetOperation(paths, path, method);
        string? schemaRef = operation["requestBody"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();

        Assert.Equal($"#/components/schemas/{schemaName}", schemaRef);
    }

    private static void AssertOperationQueryParameters(
        JsonObject paths,
        string path,
        string method,
        params string[] parameterNames)
    {
        var operation = GetOperation(paths, path, method);
        var parameters = operation["parameters"]?.AsArray()
            ?? throw new InvalidOperationException($"Operation '{method.ToUpperInvariant()} {path}' did not contain parameters.");

        foreach (string parameterName in parameterNames)
        {
            bool exists = parameters.Any(parameter =>
                string.Equals(
                    parameter?["name"]?.GetValue<string>(),
                    parameterName,
                    StringComparison.Ordinal));

            Assert.True(exists, $"Operation '{method.ToUpperInvariant()} {path}' did not expose query parameter '{parameterName}'.");
        }
    }

    private static JsonObject GetOperation(JsonObject paths, string path, string method)
        => paths[path]?[method]?.AsObject()
           ?? throw new InvalidOperationException($"Operation '{method.ToUpperInvariant()} {path}' was missing.");
}
