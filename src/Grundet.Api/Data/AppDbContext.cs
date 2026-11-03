using Grundet.Api.Common.Validation;
using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Grundet.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();
    public DbSet<TeamUsageSnapshot> TeamUsageSnapshots => Set<TeamUsageSnapshot>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEntityCounter> ProjectEntityCounters => Set<ProjectEntityCounter>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemLink> WorkItemLinks => Set<WorkItemLink>();
    public DbSet<JiraConnection> JiraConnections => Set<JiraConnection>();
    public DbSet<JiraProjectMapping> JiraProjectMappings => Set<JiraProjectMapping>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<TeamApiKey> TeamApiKeys => Set<TeamApiKey>();
    public DbSet<AlphaWaitlistEntry> AlphaWaitlistEntries => Set<AlphaWaitlistEntry>();
    public DbSet<FeedbackSubmission> FeedbackSubmissions => Set<FeedbackSubmission>();
    public DbSet<FeedbackAttachment> FeedbackAttachments => Set<FeedbackAttachment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<CurrentRequirement> CurrentRequirements => Set<CurrentRequirement>();
    public DbSet<CurrentWorkItem> CurrentWorkItems => Set<CurrentWorkItem>();
    public DbSet<CurrentWorkItemLink> CurrentWorkItemLinks => Set<CurrentWorkItemLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Teams");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => x.DeletedAt == null);
            entity.Property(x => x.Plan).HasConversion<string>().HasDefaultValue(TeamPlan.Solo);
            entity.Property(x => x.Region).HasDefaultValue("eu");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.ToTable("TeamMembers");
            entity.HasKey(x => new { x.TeamId, x.UserId });
            entity.Property(x => x.Role).HasConversion<string>();

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.TeamId })
                .HasDatabaseName("idx_team_members_user_team");
        });

        modelBuilder.Entity<TeamInvite>(entity =>
        {
            entity.ToTable("TeamInvites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasConversion<string>();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.RevokedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.TeamId, x.Email })
                .IsUnique()
                .HasFilter("\"AcceptedAt\" IS NULL AND \"RevokedAt\" IS NULL");

            entity.HasIndex(x => new { x.TeamId, x.AcceptedAt, x.RevokedAt, x.ExpiresAt })
                .HasDatabaseName("idx_team_invites_pending_by_team");

            entity.HasIndex(x => x.TokenHash)
                .HasDatabaseName("idx_team_invite_token")
                .IsUnique();
        });

        modelBuilder.Entity<TeamUsageSnapshot>(entity =>
        {
            entity.ToTable("TeamUsageSnapshots");
            entity.HasKey(x => x.TeamId);
            entity.Property(x => x.Plan).HasConversion<string>();
            entity.Property(x => x.RequirementWorkItemStatus).HasConversion<string>();

            entity.HasOne<Team>()
                .WithOne()
                .HasForeignKey<TeamUsageSnapshot>(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.TeamId });
            entity.HasQueryFilter(x => x.DeletedAt == null);

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TeamId)
                .HasDatabaseName("idx_project_team")
                .HasFilter("\"DeletedAt\" IS NULL");
        });

        modelBuilder.Entity<ProjectEntityCounter>(entity =>
        {
            entity.ToTable("ProjectEntityCounters");
            entity.HasKey(x => x.ProjectId);
            entity.Property(x => x.NextId).HasDefaultValue(1);

            entity.HasOne<Project>()
                .WithOne()
                .HasForeignKey<ProjectEntityCounter>(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Feature>(entity =>
        {
            entity.ToTable("Features");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => x.DeletedAt == null);
            var featureSearchVector = entity.Property(x => x.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """
                    setweight(immutable_to_tsvector(coalesce("Name", '')), 'A') ||
                    setweight(immutable_to_tsvector(coalesce("Description", '')), 'B')
                    """,
                    stored: true)
                .ValueGeneratedOnAddOrUpdate();
            featureSearchVector.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            featureSearchVector.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ProjectId)
                .HasDatabaseName("idx_feature_proj")
                .HasFilter("\"DeletedAt\" IS NULL");

            entity.HasIndex(x => x.SearchVector)
                .HasDatabaseName("idx_feature_search")
                .HasMethod("gin");
        });

        modelBuilder.Entity<Requirement>(entity =>
        {
            entity.ToTable("Requirements");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.Property(x => x.ChangeType).HasConversion<string>();
            entity.Property(x => x.Labels).HasColumnType("text[]");
            entity.Property(x => x.AcceptanceCriteria)
                .HasColumnType("text[]")
                .HasDefaultValueSql("'{}'::text[]");
            entity.Property(x => x.AttachmentIds)
                .HasColumnType("uuid[]")
                .HasDefaultValueSql("'{}'::uuid[]");
            var requirementSearchVector = entity.Property(x => x.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """
                    setweight(immutable_to_tsvector(coalesce("Text", '')), 'A') ||
                    setweight(immutable_to_tsvector(coalesce("Rationale", '')), 'B') ||
                    setweight(immutable_to_tsvector(coalesce(immutable_text_array_to_string("AcceptanceCriteria", ' '), '')), 'B') ||
                    setweight(immutable_to_tsvector(coalesce(immutable_text_array_to_string("Labels", ' '), '')), 'C')
                    """,
                    stored: true)
                .ValueGeneratedOnAddOrUpdate();
            requirementSearchVector.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            requirementSearchVector.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Feature>()
                .WithMany()
                .HasForeignKey(x => x.FeatureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.RequirementId, x.ChangedAt })
                .HasDatabaseName("idx_req_logical")
                .IsDescending(false, true);

            // Browse reads a project-scoped DISTINCT ON (RequirementStore.CurrentByProjectSql).
            // (ProjectId, RequirementId, ChangedAt DESC, Id DESC) is an exact prefix of that scan's
            // WHERE + ORDER BY, so it serves browse and the per-logical-row latest lookup.
            entity.HasIndex(x => new { x.ProjectId, x.RequirementId, x.ChangedAt, x.Id })
                .HasDatabaseName("idx_req_project")
                .IsDescending(false, false, true, true);

            entity.HasIndex(x => new { x.FeatureId, x.RequirementId, x.ChangedAt })
                .HasDatabaseName("idx_req_feature")
                .IsDescending(false, false, true);

            entity.HasIndex(x => x.SearchVector)
                .HasDatabaseName("idx_req_search")
                .HasMethod("gin");
        });

        modelBuilder.Entity<Draft>(entity =>
        {
            entity.ToTable("Drafts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DraftContent).HasColumnType("jsonb");
            entity.Property(x => x.Kind).HasConversion<string>();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One draft per user per scope/entity. NULL EntityId (new-entity draft) must collide
            // with itself, so NULLs are treated as not-distinct (PG15+).
            entity.HasIndex(x => new { x.UserId, x.Kind, x.ScopeId, x.EntityId })
                .IsUnique()
                .AreNullsDistinct(false)
                .HasDatabaseName("idx_draft_unique");

            entity.HasIndex(x => x.UpdatedAt)
                .HasDatabaseName("idx_draft_updated_at");
        });

        modelBuilder.Entity<WorkItem>(entity =>
        {
            entity.ToTable("WorkItems");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.ChangeType).HasConversion<string>();
            entity.Property(x => x.BlockedReason).HasMaxLength(500);
            entity.Property(x => x.Labels)
                .HasColumnType("text[]")
                .HasDefaultValueSql("'{}'::text[]");
            entity.Property(x => x.AttachmentIds)
                .HasColumnType("uuid[]")
                .HasDefaultValueSql("'{}'::uuid[]");
            var workItemSearchVector = entity.Property(x => x.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """
                    setweight(immutable_to_tsvector(coalesce("Title", '')), 'A') ||
                    setweight(immutable_to_tsvector(coalesce("Description", '')), 'B') ||
                    setweight(immutable_to_tsvector(coalesce(immutable_text_array_to_string("Labels", ' '), '')), 'C') ||
                    setweight(immutable_to_tsvector(coalesce("BlockedReason", '')), 'C') ||
                    setweight(immutable_to_tsvector(coalesce("Reason", '')), 'C')
                    """,
                    stored: true)
                .ValueGeneratedOnAddOrUpdate();
            workItemSearchVector.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            workItemSearchVector.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.WorkItemId, x.ChangedAt })
                .HasDatabaseName("idx_wi_logical")
                .IsDescending(false, true);

            // Browse reads a project-scoped DISTINCT ON (WorkItemStore.CurrentByProjectSql).
            // (ProjectId, WorkItemId, ChangedAt DESC, Id DESC) is an exact prefix of that scan's
            // WHERE + ORDER BY, so it serves browse and the per-logical-row latest lookup.
            entity.HasIndex(x => new { x.ProjectId, x.WorkItemId, x.ChangedAt, x.Id })
                .HasDatabaseName("idx_wi_project")
                .IsDescending(false, false, true, true);

            entity.HasIndex(x => x.SearchVector)
                .HasDatabaseName("idx_wi_search")
                .HasMethod("gin");
        });

        modelBuilder.Entity<WorkItemLink>(entity =>
        {
            entity.ToTable("WorkItemLinks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangeType).HasConversion<string>();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.WorkItemLinkId, x.ChangedAt })
                .HasDatabaseName("idx_wil_logical")
                .IsDescending(false, true);

            entity.HasIndex(x => new { x.WorkItemId, x.WorkItemLinkId, x.ChangedAt })
                .HasDatabaseName("idx_wil_workitem")
                .IsDescending(false, false, true);

            entity.HasIndex(x => new { x.RequirementId, x.WorkItemLinkId, x.ChangedAt })
                .HasDatabaseName("idx_wil_req")
                .IsDescending(false, false, true);
        });

        modelBuilder.Entity<JiraConnection>(entity =>
        {
            entity.ToTable("JiraConnections");
            entity.HasKey(x => x.Id);

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TeamId)
                .IsUnique();
        });

        modelBuilder.Entity<JiraProjectMapping>(entity =>
        {
            entity.ToTable("JiraProjectMappings");
            entity.HasKey(x => new { x.TeamId, x.ProjectId });

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.TeamId })
                .HasPrincipalKey(x => new { x.Id, x.TeamId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ProjectId)
                .IsUnique();
        });

        modelBuilder.Entity<MagicLinkToken>(entity =>
        {
            entity.ToTable("MagicLinkTokens");
            entity.HasKey(x => x.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.TokenHash)
                .HasDatabaseName("idx_magiclink")
                .HasFilter("\"UsedAt\" IS NULL");
        });

        modelBuilder.Entity<TeamApiKey>(entity =>
        {
            entity.ToTable("TeamApiKeys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasConversion<string>();
            entity.Property(x => x.Name).HasMaxLength(TextInput.NameMaxLength);
            entity.Property(x => x.Prefix).HasMaxLength(20);

            // Restrict: the key acts as this user; deleting the issuer must not silently orphan
            // an acts-as credential. Mirrors Requirement.ChangedByUserId.
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.RevokedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.KeyHash)
                .HasDatabaseName("idx_team_api_key_hash")
                .IsUnique();

            entity.HasIndex(x => x.TeamId)
                .HasDatabaseName("idx_team_api_key_team");
        });

        modelBuilder.Entity<AlphaWaitlistEntry>(entity =>
        {
            entity.ToTable("AlphaWaitlistEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmailOriginal).HasMaxLength(320);
            entity.Property(x => x.EmailNormalized).HasMaxLength(320);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.AccountType).HasConversion<string>();
            entity.Property(x => x.TeamSize).HasConversion<string>();
            entity.Property(x => x.LineOfWork).HasConversion<string>();
            entity.Property(x => x.OrganizationName).HasMaxLength(160);
            entity.Property(x => x.OrganizationCountry).HasMaxLength(120);
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.ApprovedBy).HasMaxLength(120);
            entity.Property(x => x.DeclinedBy).HasMaxLength(120);

            entity.HasIndex(x => x.EmailNormalized).IsUnique();

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_alpha_waitlist_email_original_not_empty", "length(trim(\"EmailOriginal\")) > 0");
                table.HasCheckConstraint("ck_alpha_waitlist_email_normalized_not_empty", "length(trim(\"EmailNormalized\")) > 0");
                table.HasCheckConstraint("ck_alpha_waitlist_name_not_empty", "length(trim(\"Name\")) > 0");
                table.HasCheckConstraint(
                    "ck_alpha_waitlist_business_fields",
                    "\"AccountType\" <> 'Business' OR (\"TeamSize\" IS NOT NULL AND \"LineOfWork\" IS NOT NULL AND length(trim(coalesce(\"OrganizationName\", ''))) > 0 AND length(trim(coalesce(\"OrganizationCountry\", ''))) > 0)");
            });
        });

        modelBuilder.Entity<FeedbackSubmission>(entity =>
        {
            entity.ToTable("FeedbackSubmissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<string>();
            entity.Property(x => x.Message).HasMaxLength(4000);
            entity.Property(x => x.PagePath).HasMaxLength(2048);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.ReadByOperator).HasMaxLength(128);
            entity.Property(x => x.DeletedByOperator).HasMaxLength(128);
            entity.Property(x => x.Locale).HasMaxLength(64);
            entity.Property(x => x.TimeZone).HasMaxLength(128);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.CreatedAt })
                .HasDatabaseName("idx_feedback_user_created");

            entity.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("idx_feedback_created");

            entity.HasIndex(x => new { x.DeletedAt, x.ReadAt, x.CreatedAt })
                .HasDatabaseName("idx_feedback_admin_state");
        });

        modelBuilder.Entity<FeedbackAttachment>(entity =>
        {
            entity.ToTable("FeedbackAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.StoragePath).HasMaxLength(512);

            entity.HasOne<FeedbackSubmission>()
                .WithMany()
                .HasForeignKey(x => x.FeedbackSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.FeedbackSubmissionId)
                .HasDatabaseName("idx_feedback_attachment_submission");
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("Attachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.StoragePath).HasMaxLength(512);

            // Blob metadata is scoped to a project for auth without a join to an owner version row.
            // Restrict so deleting a project can never wipe attachment metadata (project delete is
            // soft anyway).
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ProjectId)
                .HasDatabaseName("idx_attachment_project");

            // Content-addressed dedup within a project: an identical upload reuses the existing blob.
            // Scoped to ProjectId (never global) so a hash can't leak existence across tenants.
            entity.HasIndex(x => new { x.ProjectId, x.Sha256 })
                .IsUnique()
                .HasDatabaseName("idx_attachment_project_sha256");
        });

        modelBuilder.Entity<CurrentRequirement>(entity =>
        {
            entity.HasNoKey().ToView("CurrentRequirements");
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.Property(x => x.ChangeType).HasConversion<string>();
            entity.Property(x => x.Labels).HasColumnType("text[]");
            entity.Property(x => x.AcceptanceCriteria).HasColumnType("text[]");
            entity.Property(x => x.AttachmentIds).HasColumnType("uuid[]");
            entity.Property(x => x.SearchVector).HasColumnType("tsvector");
        });

        modelBuilder.Entity<CurrentWorkItem>(entity =>
        {
            entity.HasNoKey().ToView("CurrentWorkItems");
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.ChangeType).HasConversion<string>();
            entity.Property(x => x.Labels).HasColumnType("text[]");
            entity.Property(x => x.SearchVector).HasColumnType("tsvector");
        });

        modelBuilder.Entity<CurrentWorkItemLink>(entity =>
        {
            entity.HasNoKey().ToView("CurrentWorkItemLinks");
            entity.Property(x => x.ChangeType).HasConversion<string>();
        });

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateImmutableVersionRows();
        NormalizeDateTimeValuesToUtc();

        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateImmutableVersionRows();
        NormalizeDateTimeValuesToUtc();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ValidateImmutableVersionRows()
    {
        // Bulk operators (ExecuteDeleteAsync/ExecuteUpdateAsync) bypass ChangeTracker and this guard.
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity is Requirement or WorkItem or WorkItemLink)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} rows are immutable. Use INSERT (EntityState.Added).");
            }
        }
    }

    private void NormalizeDateTimeValuesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTime))
                {
                    if (property.CurrentValue is DateTime value)
                    {
                        property.CurrentValue = EnsureUtc(value);
                    }

                    continue;
                }

                if (property.Metadata.ClrType == typeof(DateTime?) &&
                    property.CurrentValue is DateTime nullableValue)
                {
                    property.CurrentValue = EnsureUtc(nullableValue);
                }
            }
        }
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
