using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Grundet.Api.Common.OpenApi;

/// <summary>
/// Declares a <c>type: string, format: binary</c> schema on the 200 response of endpoints that
/// stream raw bytes. Minimal API's <c>Produces(contentType: "image/...")</c> only emits the media
/// type description with an empty schema, which makes the generated TypeScript client fall back to
/// <c>unknown</c> for the body. Declaring the binary format lets the generator emit a concrete
/// <c>Blob | File</c> response type, keeping the API contract chain typed end-to-end.
/// </summary>
public sealed class BinaryResponseOperationTransformer : IOpenApiOperationTransformer
{
    // Endpoints whose 200 body is a raw binary stream (keyed by WithName/operationId).
    private static readonly HashSet<string> BinaryResponseOperationIds =
    [
        "GetAttachmentContent",
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.OperationId is null
            || !BinaryResponseOperationIds.Contains(operation.OperationId))
        {
            return Task.CompletedTask;
        }

        if (operation.Responses?.TryGetValue("200", out var response) is true
            && response is OpenApiResponse okResponse)
        {
            okResponse.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["image/jpeg"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "binary",
                    },
                },
            };
        }

        return Task.CompletedTask;
    }
}
