using System.Net;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class ChangelogIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task Changelog_UnionsIntentWorkAndLink_NewestFirst()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent one");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work one");
        await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, requirement.Requirement.RequirementId);

        var result = await TestApi.GetProjectChangelogAsync(lead, project.Id);

        Assert.Equal(3, result.Total);
        Assert.Equal(["Link", "Work", "Intent"], result.Items.Select(item => item.EntityKind).ToArray());
        AssertDescendingByChangedAt(result);
        Assert.All(result.Items, item => Assert.NotNull(item.ChangedByName));

        // Link rows expose both ends so the frontend can link the WO- reference and IN- title to
        // their detail pages; Intent/Work rows address their own item and carry no linked ids.
        var link = result.Items[0];
        Assert.Equal(workItem.WorkItemId, link.LinkedWorkItemId);
        Assert.Equal(requirement.Requirement.RequirementId, link.LinkedRequirementId);
        Assert.All(result.Items.Where(item => item.EntityKind != "Link"), item =>
        {
            Assert.Null(item.LinkedWorkItemId);
            Assert.Null(item.LinkedRequirementId);
        });
    }

    [Fact]
    public async Task Changelog_EntityTypeFilter_ReturnsOnlyRequestedKinds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent one");
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Work one");

        var result = await TestApi.GetProjectChangelogAsync(lead, project.Id, entityTypes: ["Intent"]);

        Assert.All(result.Items, item => Assert.Equal("Intent", item.EntityKind));
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Changelog_ChangeTypeFilter_SeparatesCreatedFromUpdated()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Original text");
        await TestApi.UpdateRequirementAsync(lead, requirement.Requirement.RequirementId, "Updated text");

        var updated = await TestApi.GetProjectChangelogAsync(lead, project.Id, changeTypes: ["Updated"]);
        var created = await TestApi.GetProjectChangelogAsync(lead, project.Id, changeTypes: ["Created"]);

        Assert.Single(updated.Items);
        Assert.Equal("Updated", updated.Items[0].ChangeType);
        Assert.Single(created.Items);
        Assert.Equal("Created", created.Items[0].ChangeType);
    }

    [Fact]
    public async Task Changelog_FeatureFilter_ScopesToFeatureIntentAndExcludesWorkAndLinks()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(lead, project.Id);
        var featureB = await TestApi.CreateFeatureAsync(lead, project.Id);

        var requirementA = await TestApi.CreateRequirementAsync(lead, featureA.Id, "Feature A intent");
        await TestApi.CreateRequirementAsync(lead, featureB.Id, "Feature B intent");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work one");
        await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, requirementA.Requirement.RequirementId);

        var result = await TestApi.GetProjectChangelogAsync(lead, project.Id, featureId: featureA.Id);

        Assert.Single(result.Items);
        Assert.Equal("Intent", result.Items[0].EntityKind);
        Assert.Equal(featureA.Id, result.Items[0].FeatureId);
    }

    [Fact]
    public async Task Changelog_AuthorFilter_ReturnsOnlyMatchingAuthor()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var contributor = await TestApi.SignInAsync(fixture);
        string contributorEmail = await TestApi.InviteContributorAsync(lead, team.Id, contributor.Email);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Lead intent");
        await TestApi.CreateRequirementAsync(contributor, feature.Id, "Contributor intent");

        var byContributor = await TestApi.GetProjectChangelogAsync(lead, project.Id, authorId: contributor.UserId);

        Assert.Single(byContributor.Items);
        Assert.Equal(contributor.UserId, byContributor.Items[0].ChangedByUserId);
    }

    [Fact]
    public async Task Changelog_AfterAuthorLeavesTeam_KeepsAttributionAndMarksFormerMember()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var contributor = await TestApi.SignInAsync(fixture);
        string contributorEmail = await TestApi.InviteContributorAsync(lead, team.Id, contributor.Email);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Lead intent");
        await TestApi.CreateRequirementAsync(contributor, feature.Id, "Contributor intent");

        await TestApi.RemoveTeamMemberAsync(lead, team.Id, contributor.UserId);

        var result = await TestApi.GetProjectChangelogAsync(lead, project.Id);

        var contributorEntry = result.Items.Single(item => item.ChangedByUserId == contributor.UserId);
        Assert.NotNull(contributorEntry.ChangedByName);
        Assert.False(contributorEntry.ChangedByIsActiveMember);

        var leadEntry = result.Items.Single(item => item.ChangedByUserId == lead.UserId);
        Assert.True(leadEntry.ChangedByIsActiveMember);
    }

    [Fact]
    public async Task ChangelogAuthors_IncludeFormerMembers_WithActiveFlag()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var contributor = await TestApi.SignInAsync(fixture);
        string contributorEmail = await TestApi.InviteContributorAsync(lead, team.Id, contributor.Email);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Lead intent");
        await TestApi.CreateRequirementAsync(contributor, feature.Id, "Contributor intent");

        await TestApi.RemoveTeamMemberAsync(lead, team.Id, contributor.UserId);

        var authors = await TestApi.GetProjectChangelogAuthorsAsync(lead, project.Id);

        Assert.Equal(2, authors.Count);
        Assert.True(authors.Single(author => author.UserId == lead.UserId).IsActiveMember);
        Assert.False(authors.Single(author => author.UserId == contributor.UserId).IsActiveMember);
    }

    [Fact]
    public async Task Changelog_NonMemberAuthorFilter_ReturnsEmptyNotError()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Lead intent");

        var result = await TestApi.GetProjectChangelogAsync(lead, project.Id, authorId: Guid.NewGuid());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Changelog_Pagination_ReturnsCorrectTotalAndHasMore()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        for (int index = 0; index < 5; index++)
        {
            await TestApi.CreateRequirementAsync(lead, feature.Id, $"Intent {index}");
        }

        var firstPage = await TestApi.GetProjectChangelogAsync(lead, project.Id, page: 1, pageSize: 2);

        Assert.Equal(5, firstPage.Total);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.True(firstPage.HasMore);
        Assert.Equal(2, firstPage.Items.Count);
    }

    [Fact]
    public async Task Changelog_PageBeyondEnd_ReturnsEmptyItemsWithAccurateTotal()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Only intent");

        var result = await TestApi.GetProjectChangelogAsync(lead, project.Id, page: 5, pageSize: 10);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.Total);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task Changelog_DateRange_FiltersByChangedAt()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Recent intent");

        string future = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var afterFuture = await TestApi.GetProjectChangelogAsync(lead, project.Id, from: future);
        var beforeFuture = await TestApi.GetProjectChangelogAsync(lead, project.Id, to: future);

        Assert.Empty(afterFuture.Items);
        Assert.Single(beforeFuture.Items);
    }

    [Fact]
    public async Task Changelog_CrossProjectFeatureFilter_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var projectA = await TestApi.CreateProjectAsync(lead, team.Id);
        var projectB = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureInB = await TestApi.CreateFeatureAsync(lead, projectB.Id);

        var response = await TestApi.GetProjectChangelogRawAsync(lead, projectA.Id, featureId: featureInB.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Changelog_NonMemberProjectAccess_IsDenied()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var outsider = await TestApi.SignInAsync(fixture);
        var response = await TestApi.GetProjectChangelogRawAsync(outsider, project.Id);

        // A non-member gets 404 rather than 403 so project existence is not leaked across tenants.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("entityTypes")]
    [InlineData("changeTypes")]
    public async Task Changelog_NumericEnumToken_IsRejected(string parameter)
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var response = parameter == "entityTypes"
            ? await TestApi.GetProjectChangelogRawAsync(lead, project.Id, entityTypes: ["0"])
            : await TestApi.GetProjectChangelogRawAsync(lead, project.Id, changeTypes: ["0"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changelog_UnknownEnumToken_IsRejected()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var response = await TestApi.GetProjectChangelogRawAsync(lead, project.Id, entityTypes: ["Bogus"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void AssertDescendingByChangedAt(PagedChangelogResponse result)
    {
        for (int index = 1; index < result.Items.Count; index++)
        {
            Assert.True(
                result.Items[index - 1].ChangedAt >= result.Items[index].ChangedAt,
                "Changelog entries must be ordered newest-first by ChangedAt.");
        }
    }
}
