using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RequirementAttachmentIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task Upload_ReferenceAtCreate_PersistsServesAndHydrates()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Assert.Equal("image/jpeg", uploaded.ContentType);

        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, feature.Id, "Spec with image", [uploaded.Id]);

        // Detail hydration returns the referenced attachment, ordered.
        var attachments = await GetAttachmentsAsync(user, project.Id, requirementId);
        Assert.Single(attachments);
        Assert.Equal(uploaded.Id, attachments[0].Id);

        // Bytes are served to an authorized member with the right content type.
        var contentResponse = await user.Client.GetAsync(ContentUrl(project.Id, uploaded.Id));
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        Assert.Equal("image/jpeg", contentResponse.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await contentResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);

        // Version row carries the attachment id.
        await fixture.WithDbContextAsync(async db =>
        {
            var version = await db.Requirements.SingleAsync(r => r.RequirementId == requirementId);
            Assert.Equal(new[] { uploaded.Id }, version.AttachmentIds);
        });
    }

    [Fact]
    public async Task TextEdit_KeepsAttachments_AndRemovalPreservesOldVersions()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, feature.Id, "Original", [uploaded.Id]);

        // Text-only edit (no attachmentIds field) must keep the image set.
        var textEdit = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/requirements/{requirementId}",
            new { text = "Edited text only", reason = "tweak" });
        Assert.Equal(HttpStatusCode.OK, textEdit.StatusCode);

        var afterTextEdit = await GetAttachmentsAsync(user, project.Id, requirementId);
        Assert.Single(afterTextEdit);

        // Removing the image (explicit empty array) drops it from the new version only.
        var removeEdit = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/requirements/{requirementId}",
            new { attachmentIds = Array.Empty<Guid>(), reason = "remove image" });
        Assert.Equal(HttpStatusCode.OK, removeEdit.StatusCode);

        var afterRemoval = await GetAttachmentsAsync(user, project.Id, requirementId);
        Assert.Empty(afterRemoval);

        // Append-only proof: the earlier versions still reference the image.
        await fixture.WithDbContextAsync(async db =>
        {
            var versions = await db.Requirements
                .Where(r => r.RequirementId == requirementId)
                .OrderBy(r => r.ChangedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            Assert.Equal(3, versions.Count);
            Assert.Equal(new[] { uploaded.Id }, versions[0].AttachmentIds); // created
            Assert.Equal(new[] { uploaded.Id }, versions[1].AttachmentIds); // text edit kept it
            Assert.Empty(versions[2].AttachmentIds); // removal
        });
    }

    [Fact]
    public async Task CrossTenantReference_AtCreateAndEdit_Rejected()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var projectA = await TestApi.CreateProjectAsync(user, team.Id);
        var projectB = await TestApi.CreateProjectAsync(user, team.Id);
        var featureB = await TestApi.CreateFeatureAsync(user, projectB.Id);

        // Attachment belongs to project A.
        UploadedAttachment foreign = await UploadOneAsync(user, projectA.Id);

        // Referencing it while authoring in project B is rejected at create.
        var createResponse = await user.Client.PostAsJsonAsync(
            $"/api/v1/features/{featureB.Id}/requirements",
            new { text = "Tries to steal an image", status = "Draft", priority = "Must", attachmentIds = new[] { foreign.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        // And at edit.
        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, featureB.Id, "Clean requirement", []);
        var editResponse = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectB.Id}/requirements/{requirementId}",
            new { attachmentIds = new[] { foreign.Id }, reason = "attach" });
        Assert.Equal(HttpStatusCode.BadRequest, editResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteRequirement_TombstoneCopiesAttachmentIds()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, feature.Id, "To delete", [uploaded.Id]);

        var deleteResponse = await user.Client.DeleteAsync($"/api/v1/projects/{project.Id}/requirements/{requirementId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await AssertDeletedTombstoneKeepsAttachmentAsync(requirementId, uploaded.Id);
    }

    [Fact]
    public async Task ProjectDeleteCascade_TombstoneCopiesAttachmentIds()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, feature.Id, "Project cascade", [uploaded.Id]);

        var deleteResponse = await user.Client.DeleteAsync($"/api/v1/teams/{team.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await AssertDeletedTombstoneKeepsAttachmentAsync(requirementId, uploaded.Id);
    }

    [Fact]
    public async Task FeatureDeleteCascade_TombstoneCopiesAttachmentIds()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, feature.Id, "Feature cascade", [uploaded.Id]);

        var deleteResponse = await user.Client.DeleteAsync($"/api/v1/projects/{project.Id}/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await AssertDeletedTombstoneKeepsAttachmentAsync(requirementId, uploaded.Id);
    }

    [Fact]
    public async Task ServeContent_EnforcesAuthorization()
    {
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        UploadedAttachment uploaded = await UploadOneAsync(owner, project.Id);

        // No cookie → 401.
        var anonymous = fixture.CreateClient();
        var anonResponse = await anonymous.GetAsync(ContentUrl(project.Id, uploaded.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);

        // Authenticated user from another team has no access to the project → 403/404.
        var outsider = await TestApi.SignInAsync(fixture);
        var outsiderResponse = await outsider.Client.GetAsync(ContentUrl(project.Id, uploaded.Id));
        Assert.Contains(outsiderResponse.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });

        // Unknown attachment id within an accessible project → 404.
        var unknownResponse = await owner.Client.GetAsync(ContentUrl(project.Id, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsAttachmentBearingRequirements_WithoutPerItemHydration()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        // Several attachment-bearing requirements. The list endpoint never queries attachments
        // per row (it excludes attachment metadata), so there is no N+1 by construction.
        for (int i = 0; i < 3; i++)
        {
            UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
            await CreateRequirementWithAttachmentsAsync(user, feature.Id, $"Listed {i}", [uploaded.Id]);
        }

        var listed = await TestApi.ListProjectRequirementsAsync(user, project.Id);
        Assert.Equal(3, listed.Total);
    }

    private async Task AssertDeletedTombstoneKeepsAttachmentAsync(Guid requirementId, Guid attachmentId)
        => await fixture.WithDbContextAsync(async db =>
        {
            var versions = await db.Requirements
                .Where(r => r.RequirementId == requirementId)
                .OrderBy(r => r.ChangedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            Requirement tombstone = versions.Last();
            Assert.Equal(RequirementChange.Deleted, tombstone.ChangeType);
            Assert.Equal(new[] { attachmentId }, tombstone.AttachmentIds);
        });

    [Fact]
    public async Task Upload_IdenticalBytes_DedupsWithinProjectButNotAcrossProjects()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var projectA = await TestApi.CreateProjectAsync(user, team.Id);
        var projectB = await TestApi.CreateProjectAsync(user, team.Id);

        byte[] png = CreatePng(40, 40);

        // Same bytes uploaded in two separate requests reuse the same blob.
        var first = await UploadBytesAsync(user, projectA.Id, ("a.png", png));
        var second = await UploadBytesAsync(user, projectA.Id, ("b.png", png));
        Assert.Equal(first.Single().Id, second.Single().Id);

        // Two identical files in a single batch collapse to one blob, returned per-file.
        var batch = await UploadBytesAsync(user, projectA.Id, ("c.png", png), ("d.png", png));
        Assert.Equal(2, batch.Count);
        Assert.Equal(batch[0].Id, batch[1].Id);
        Assert.Equal(first.Single().Id, batch[0].Id);

        // A different project with identical bytes gets its own blob — no cross-tenant sharing.
        var other = await UploadBytesAsync(user, projectB.Id, ("a.png", png));
        Assert.NotEqual(first.Single().Id, other.Single().Id);

        // Exactly one stored row per project for that content.
        await fixture.WithDbContextAsync(async db =>
        {
            int inA = await db.Attachments.CountAsync(a => a.ProjectId == projectA.Id);
            int inB = await db.Attachments.CountAsync(a => a.ProjectId == projectB.Id);
            Assert.Equal(1, inA);
            Assert.Equal(1, inB);
        });
    }

    [Fact]
    public async Task ServeContent_ThumbnailVariant_IsSmallerAndInvalidVariantsRejected()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);

        // Source larger than the 400px thumbnail cap so the thumb is genuinely downscaled.
        var uploaded = (await UploadBytesAsync(user, project.Id, ("big.png", CreatePng(800, 600)))).Single();

        byte[] full = await ReadContentAsync(user, project.Id, uploaded.Id, variant: null);
        byte[] thumb = await ReadContentAsync(user, project.Id, uploaded.Id, variant: "thumb");
        byte[] explicitFull = await ReadContentAsync(user, project.Id, uploaded.Id, variant: "full");

        Assert.True(thumb.Length > 0);
        Assert.True(thumb.Length < full.Length, "thumbnail JPEG should be smaller than the full image");
        Assert.Equal(full.Length, explicitFull.Length);

        // Variant is a name-based contract: unknown names and numeric tokens are rejected.
        foreach (string invalid in new[] { "bogus", "0", "1" })
        {
            var response = await user.Client.GetAsync($"{ContentUrl(project.Id, uploaded.Id)}?variant={invalid}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static async Task<byte[]> ReadContentAsync(AuthSession user, Guid projectId, Guid attachmentId, string? variant)
    {
        string url = variant is null
            ? ContentUrl(projectId, attachmentId)
            : $"{ContentUrl(projectId, attachmentId)}?variant={variant}";
        var response = await user.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task EditAddingImage_ProducesAttachmentChangelogDiff_AndIsNotConflatedWithFirstVersion()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);
        var feature = await TestApi.CreateFeatureAsync(user, project.Id);

        Guid requirementId = await CreateRequirementWithAttachmentsAsync(user, feature.Id, "No image yet", []);
        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);

        // An edit that ONLY adds an image. Before the attachment diff existed, this yielded an empty
        // change set, which the UI mislabeled as "first version – nothing to compare".
        var edit = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/requirements/{requirementId}",
            new { attachmentIds = new[] { uploaded.Id }, reason = "add chart" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var history = await user.Client.GetFromJsonAsync<List<VersionEntry>>(
            $"/api/v1/requirements/{requirementId}/versions");
        Assert.NotNull(history);

        // v2's change set is non-null and carries the added attachment.
        VersionEntry v2 = history!.Single(v => v.VersionNumber == 2);
        Assert.NotNull(v2.Changes?.Attachments);
        Assert.Contains(uploaded.Id, v2.Changes!.Attachments!.Added);
        Assert.Empty(v2.Changes.Attachments.Removed);

        // v1 is the genuine initial version — null changes (the UI's only legitimate "first version").
        Assert.Null(history.Single(v => v.VersionNumber == 1).Changes);
    }

    private sealed record VersionEntry(int VersionNumber, VersionChangesDto? Changes);

    private sealed record VersionChangesDto(AttachmentsDiffDto? Attachments);

    private sealed record AttachmentsDiffDto(Guid[] Added, Guid[] Removed);

    private static string ContentUrl(Guid projectId, Guid attachmentId)
        => $"/api/v1/projects/{projectId}/attachments/{attachmentId}/content";

    private static async Task<UploadedAttachment> UploadOneAsync(AuthSession user, Guid projectId)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(CreatePng(48, 32)), "files", "spec.png" }
        };

        var response = await user.Client.PostAsync(
            $"/api/v1/projects/{projectId}/attachments", form);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Upload failed: {response.StatusCode}: {body}");
        }

        var attachments = await response.Content.ReadFromJsonAsync<List<UploadedAttachment>>();
        Assert.NotNull(attachments);
        return attachments!.Single();
    }

    private static async Task<List<UploadedAttachment>> UploadBytesAsync(
        AuthSession user,
        Guid projectId,
        params (string Name, byte[] Bytes)[] files)
    {
        using var form = new MultipartFormDataContent();
        foreach ((string name, byte[] bytes) in files)
        {
            form.Add(new ByteArrayContent(bytes), "files", name);
        }

        var response = await user.Client.PostAsync($"/api/v1/projects/{projectId}/attachments", form);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Upload failed: {response.StatusCode}: {body}");
        }

        var attachments = await response.Content.ReadFromJsonAsync<List<UploadedAttachment>>();
        Assert.NotNull(attachments);
        return attachments!;
    }

    private static async Task<Guid> CreateRequirementWithAttachmentsAsync(
        AuthSession user,
        Guid featureId,
        string text,
        Guid[] attachmentIds)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/features/{featureId}/requirements",
            new { text, status = "Draft", priority = "Must", attachmentIds });
        if (response.StatusCode != HttpStatusCode.Created)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Create failed: {response.StatusCode}: {body}");
        }

        var created = await response.Content.ReadFromJsonAsync<SaveRequirementResponse>();
        Assert.NotNull(created);
        return created!.Requirement.RequirementId;
    }

    private static async Task<List<UploadedAttachment>> GetAttachmentsAsync(
        AuthSession user,
        Guid projectId,
        Guid requirementId)
    {
        var response = await user.Client.GetAsync(
            $"/api/v1/projects/{projectId}/requirements/{requirementId}/attachments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attachments = await response.Content.ReadFromJsonAsync<List<UploadedAttachment>>();
        Assert.NotNull(attachments);
        return attachments!;
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed record UploadedAttachment(Guid Id, string? OriginalFileName, string ContentType, int Width, int Height);
}
