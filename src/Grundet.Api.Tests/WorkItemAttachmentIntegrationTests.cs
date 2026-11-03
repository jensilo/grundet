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
public sealed class WorkItemAttachmentIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task Create_WithAttachment_PersistsServesAndHydrates()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid workItemId = await CreateWorkItemWithAttachmentsAsync(user, project.Id, "Bug with evidence", [uploaded.Id]);

        // Hydration endpoint returns the referenced attachment.
        var attachments = await GetAttachmentsAsync(user, project.Id, workItemId);
        Assert.Single(attachments);
        Assert.Equal(uploaded.Id, attachments[0].Id);

        // Bytes serve (thumb + full) to an authorized member.
        var content = await user.Client.GetAsync(
            $"/api/v1/projects/{project.Id}/attachments/{uploaded.Id}/content?variant=thumb");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);

        // Version row carries the attachment id.
        await fixture.WithDbContextAsync(async db =>
        {
            var version = await db.WorkItems.SingleAsync(w => w.WorkItemId == workItemId);
            Assert.Equal(new[] { uploaded.Id }, version.AttachmentIds);
        });
    }

    [Fact]
    public async Task Update_KeepsAttachmentsOnTextEdit_AndRemovalPreservesOldVersions()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid workItemId = await CreateWorkItemWithAttachmentsAsync(user, project.Id, "Original", [uploaded.Id]);

        // Title-only edit (no attachmentIds field) keeps the image set (WasProvided).
        var textEdit = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/work-items/{workItemId}",
            new { title = "Edited title only", reason = "tweak" });
        Assert.Equal(HttpStatusCode.OK, textEdit.StatusCode);
        Assert.Single(await GetAttachmentsAsync(user, project.Id, workItemId));

        // Explicit empty array drops it from the new version only.
        var removeEdit = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/work-items/{workItemId}",
            new { attachmentIds = Array.Empty<Guid>(), reason = "remove image" });
        Assert.Equal(HttpStatusCode.OK, removeEdit.StatusCode);
        Assert.Empty(await GetAttachmentsAsync(user, project.Id, workItemId));

        // Append-only proof: earlier versions still reference the image.
        await fixture.WithDbContextAsync(async db =>
        {
            var versions = await db.WorkItems
                .Where(w => w.WorkItemId == workItemId)
                .OrderBy(w => w.ChangedAt)
                .ThenBy(w => w.Id)
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

        // Attachment belongs to project A.
        UploadedAttachment foreign = await UploadOneAsync(user, projectA.Id);

        // Referencing it while authoring in project B is rejected at create.
        var createResponse = await user.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectB.Id}/work-items",
            new { title = "Tries to steal an image", attachmentIds = new[] { foreign.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        // And at edit.
        Guid workItemId = await CreateWorkItemWithAttachmentsAsync(user, projectB.Id, "Clean work", []);
        var editResponse = await user.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectB.Id}/work-items/{workItemId}",
            new { attachmentIds = new[] { foreign.Id }, reason = "attach" });
        Assert.Equal(HttpStatusCode.BadRequest, editResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkItem_TombstoneCopiesAttachmentIds()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid workItemId = await CreateWorkItemWithAttachmentsAsync(user, project.Id, "To delete", [uploaded.Id]);

        var deleteResponse = await user.Client.DeleteAsync($"/api/v1/projects/{project.Id}/work-items/{workItemId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await AssertDeletedTombstoneKeepsAttachmentAsync(workItemId, uploaded.Id);
    }

    [Fact]
    public async Task ProjectDeleteCascade_TombstoneCopiesAttachmentIds()
    {
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);
        var project = await TestApi.CreateProjectAsync(user, team.Id);

        UploadedAttachment uploaded = await UploadOneAsync(user, project.Id);
        Guid workItemId = await CreateWorkItemWithAttachmentsAsync(user, project.Id, "Project cascade", [uploaded.Id]);

        var deleteResponse = await user.Client.DeleteAsync($"/api/v1/teams/{team.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await AssertDeletedTombstoneKeepsAttachmentAsync(workItemId, uploaded.Id);
    }

    private async Task AssertDeletedTombstoneKeepsAttachmentAsync(Guid workItemId, Guid attachmentId)
        => await fixture.WithDbContextAsync(async db =>
        {
            var versions = await db.WorkItems
                .Where(w => w.WorkItemId == workItemId)
                .OrderBy(w => w.ChangedAt)
                .ThenBy(w => w.Id)
                .ToListAsync();

            WorkItem tombstone = versions.Last();
            Assert.Equal(WorkItemChange.Deleted, tombstone.ChangeType);
            Assert.Equal(new[] { attachmentId }, tombstone.AttachmentIds);
        });

    private static async Task<Guid> CreateWorkItemWithAttachmentsAsync(
        AuthSession user,
        Guid projectId,
        string title,
        Guid[] attachmentIds)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new { title, status = "Todo", priority = "Medium", type = "Bug", attachmentIds });
        if (response.StatusCode != HttpStatusCode.Created)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Create failed: {response.StatusCode}: {body}");
        }

        var created = await response.Content.ReadFromJsonAsync<CreatedWorkItem>();
        Assert.NotNull(created);
        return created!.WorkItemId;
    }

    private static async Task<UploadedAttachment> UploadOneAsync(AuthSession user, Guid projectId)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(CreatePng(48, 32)), "files", "evidence.png" }
        };

        var response = await user.Client.PostAsync($"/api/v1/projects/{projectId}/attachments", form);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Upload failed: {response.StatusCode}: {body}");
        }

        var attachments = await response.Content.ReadFromJsonAsync<List<UploadedAttachment>>();
        Assert.NotNull(attachments);
        return attachments!.Single();
    }

    private static async Task<List<UploadedAttachment>> GetAttachmentsAsync(
        AuthSession user,
        Guid projectId,
        Guid workItemId)
    {
        var response = await user.Client.GetAsync(
            $"/api/v1/projects/{projectId}/work-items/{workItemId}/attachments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attachments = await response.Content.ReadFromJsonAsync<List<UploadedAttachment>>();
        Assert.NotNull(attachments);
        return attachments!;
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.OrangeRed);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed record UploadedAttachment(Guid Id, string? OriginalFileName, string ContentType, int Width, int Height);

    private sealed record CreatedWorkItem(Guid WorkItemId);
}
