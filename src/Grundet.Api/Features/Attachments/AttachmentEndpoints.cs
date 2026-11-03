using System.Globalization;
using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Images;
using Grundet.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.Attachments;

public static class AttachmentEndpoints
{
    private const long MaxUploadRequestBytes = 60 * 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapPost("/projects/{projectId:guid}/attachments", async (
                Guid projectId,
                [FromForm] CreateAttachmentsRequest? request,
                ClaimsPrincipal principal,
                HttpRequest httpRequest,
                IResourceAuthorizationService authorizationService,
                AttachmentService attachmentService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var attachments = await attachmentService.UploadAsync(
                    authorizedProjectId,
                    ResolveFiles(request, httpRequest),
                    cancellationToken);

                return Results.Ok(attachments);
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes))
            .WithName("CreateAttachments")
            .WithSummary("Upload image attachments for a project")
            .Produces<List<AttachmentResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        api.MapGet("/projects/{projectId:guid}/attachments/{attachmentId:guid}/content", async (
                Guid projectId,
                Guid attachmentId,
                [FromQuery] string? variant,
                ClaimsPrincipal principal,
                HttpResponse httpResponse,
                IResourceAuthorizationService authorizationService,
                AttachmentService attachmentService,
                CancellationToken cancellationToken) =>
            {
                ImageVariant imageVariant = ParseVariant(variant);
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                AttachmentContent content = await attachmentService.GetContentAsync(
                    authorizedProjectId,
                    attachmentId,
                    imageVariant,
                    cancellationToken);

                // Blob bytes are immutable (content-addressed by id) but tenant-scoped, so cache
                // privately and long.
                httpResponse.Headers.CacheControl = "private, max-age=31536000, immutable";
                return Results.Stream(content.Stream, content.ContentType, enableRangeProcessing: true);
            })
            .WithName("GetAttachmentContent")
            .WithSummary("Stream the bytes of a project image attachment (variant=full|thumb)")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // Name-based enum parsing for the optional `variant` query param. Absent → full. Numeric tokens
    // are rejected so the public contract stays name-based (Enum.TryParse would otherwise accept "0").
    private static ImageVariant ParseVariant(string? raw)
    {
        string trimmed = raw?.Trim() ?? string.Empty;

        return trimmed.Length == 0
            ? ImageVariant.Full
            : !int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && Enum.TryParse(trimmed, ignoreCase: true, out ImageVariant variant)
                && Enum.IsDefined(variant)
            ? variant
            : throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "variant must be 'full' or 'thumb'.");
    }

    // Minimal API form binding leaves the complex DTO null when the multipart body carries only
    // files and no scalar fields, so read straight from the form in that case. The DTO still shapes
    // the OpenAPI body so the generated client exposes a typed `files` field.
    private static IReadOnlyList<IFormFile> ResolveFiles(CreateAttachmentsRequest? request, HttpRequest httpRequest)
        => request?.Files is { Count: > 0 }
            ? request.Files
            : httpRequest.Form.Files.GetFiles("files");
}

public sealed class CreateAttachmentsRequest
{
    public List<IFormFile> Files { get; set; } = [];
}
