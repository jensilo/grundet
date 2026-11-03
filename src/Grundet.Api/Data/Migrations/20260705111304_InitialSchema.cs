using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Grundet.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Immutable helper functions used by generated (stored) tsvector columns.
            // Must exist before the tables that reference them in computedColumnSql.
            migrationBuilder.Sql(@"
CREATE FUNCTION public.immutable_text_array_to_string(input_values text[], delimiter text) RETURNS text
    LANGUAGE sql IMMUTABLE STRICT
    AS $$ SELECT array_to_string(input_values, delimiter) $$;
CREATE FUNCTION public.immutable_to_tsvector(document text) RETURNS tsvector
    LANGUAGE sql IMMUTABLE STRICT
    AS $_$ SELECT to_tsvector('pg_catalog.english'::regconfig, $1) $_$;
");

            migrationBuilder.CreateTable(
                name: "AlphaWaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailOriginal = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AccountType = table.Column<string>(type: "text", nullable: false),
                    TeamSize = table.Column<string>(type: "text", nullable: true),
                    LineOfWork = table.Column<string>(type: "text", nullable: true),
                    OrganizationName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OrganizationCountry = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DeclinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclinedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlphaWaitlistEntries", x => x.Id);
                    table.CheckConstraint("ck_alpha_waitlist_business_fields", "\"AccountType\" <> 'Business' OR (\"TeamSize\" IS NOT NULL AND \"LineOfWork\" IS NOT NULL AND length(trim(coalesce(\"OrganizationName\", ''))) > 0 AND length(trim(coalesce(\"OrganizationCountry\", ''))) > 0)");
                    table.CheckConstraint("ck_alpha_waitlist_email_normalized_not_empty", "length(trim(\"EmailNormalized\")) > 0");
                    table.CheckConstraint("ck_alpha_waitlist_email_original_not_empty", "length(trim(\"EmailOriginal\")) > 0");
                    table.CheckConstraint("ck_alpha_waitlist_name_not_empty", "length(trim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: false, defaultValue: "Solo"),
                    Region = table.Column<string>(type: "text", nullable: false, defaultValue: "eu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnboardingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JiraConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloudId = table.Column<string>(type: "text", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JiraProjectKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JiraConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JiraConnections_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.UniqueConstraint("AK_Projects_Id_TeamId", x => new { x.Id, x.TeamId });
                    table.ForeignKey(
                        name: "FK_Projects_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamUsageSnapshots",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: false),
                    ProjectCount = table.Column<int>(type: "integer", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    RequirementCount = table.Column<int>(type: "integer", nullable: false),
                    WorkItemCount = table.Column<int>(type: "integer", nullable: false),
                    RequirementWorkItemStatus = table.Column<string>(type: "text", nullable: false),
                    OverLimitSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnforcementStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamUsageSnapshots", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_TeamUsageSnapshots_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftContent = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drafts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PagePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ViewportWidth = table.Column<int>(type: "integer", nullable: true),
                    ViewportHeight = table.Column<int>(type: "integer", nullable: true),
                    Locale = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadByOperator = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByOperator = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackSubmissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MagicLinkTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagicLinkTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MagicLinkTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamApiKeys_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamApiKeys_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamApiKeys_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamInvites_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamInvites_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemLinks_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(immutable_to_tsvector(coalesce(\"Name\", '')), 'A') ||\nsetweight(immutable_to_tsvector(coalesce(\"Description\", '')), 'B')", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Features_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JiraProjectMappings",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    JiraProjectKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JiraProjectMappings", x => new { x.TeamId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_JiraProjectMappings_Projects_ProjectId_TeamId",
                        columns: x => new { x.ProjectId, x.TeamId },
                        principalTable: "Projects",
                        principalColumns: new[] { "Id", "TeamId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JiraProjectMappings_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEntityCounters",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextId = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEntityCounters", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectEntityCounters_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlockedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Labels = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "text", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(immutable_to_tsvector(coalesce(\"Title\", '')), 'A') ||\nsetweight(immutable_to_tsvector(coalesce(\"Description\", '')), 'B') ||\nsetweight(immutable_to_tsvector(coalesce(immutable_text_array_to_string(\"Labels\", ' '), '')), 'C') ||\nsetweight(immutable_to_tsvector(coalesce(\"BlockedReason\", '')), 'C') ||\nsetweight(immutable_to_tsvector(coalesce(\"Reason\", '')), 'C')", stored: true),
                    // AttachmentIds placed last to match the physical column order produced by the
                    // original incremental AddColumn migration (AddProjectAttachments).
                    AttachmentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'::uuid[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackAttachments_FeedbackSubmissions_FeedbackSubmissionId",
                        column: x => x.FeedbackSubmissionId,
                        principalTable: "FeedbackSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Labels = table.Column<string[]>(type: "text[]", nullable: false),
                    AcceptanceCriteria = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                    Rationale = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "text", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(immutable_to_tsvector(coalesce(\"Text\", '')), 'A') ||\nsetweight(immutable_to_tsvector(coalesce(\"Rationale\", '')), 'B') ||\nsetweight(immutable_to_tsvector(coalesce(immutable_text_array_to_string(\"AcceptanceCriteria\", ' '), '')), 'B') ||\nsetweight(immutable_to_tsvector(coalesce(immutable_text_array_to_string(\"Labels\", ' '), '')), 'C')", stored: true),
                    // AttachmentIds placed last to match the physical column order produced by the
                    // original incremental AddColumn migration (AddProjectAttachments).
                    AttachmentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'::uuid[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requirements_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requirements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requirements_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlphaWaitlistEntries_EmailNormalized",
                table: "AlphaWaitlistEntries",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_attachment_project",
                table: "Attachments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "idx_attachment_project_sha256",
                table: "Attachments",
                columns: new[] { "ProjectId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_draft_unique",
                table: "Drafts",
                columns: new[] { "UserId", "Kind", "ScopeId", "EntityId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "idx_draft_updated_at",
                table: "Drafts",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "idx_feature_proj",
                table: "Features",
                column: "ProjectId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_feature_search",
                table: "Features",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_feedback_attachment_submission",
                table: "FeedbackAttachments",
                column: "FeedbackSubmissionId");

            migrationBuilder.CreateIndex(
                name: "idx_feedback_admin_state",
                table: "FeedbackSubmissions",
                columns: new[] { "DeletedAt", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_feedback_created",
                table: "FeedbackSubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "idx_feedback_user_created",
                table: "FeedbackSubmissions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JiraConnections_TeamId",
                table: "JiraConnections",
                column: "TeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JiraProjectMappings_ProjectId",
                table: "JiraProjectMappings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JiraProjectMappings_ProjectId_TeamId",
                table: "JiraProjectMappings",
                columns: new[] { "ProjectId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "idx_magiclink",
                table: "MagicLinkTokens",
                column: "TokenHash",
                filter: "\"UsedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinkTokens_UserId",
                table: "MagicLinkTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_project_team",
                table: "Projects",
                column: "TeamId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_req_feature",
                table: "Requirements",
                columns: new[] { "FeatureId", "RequirementId", "ChangedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "idx_req_logical",
                table: "Requirements",
                columns: new[] { "RequirementId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_req_project",
                table: "Requirements",
                columns: new[] { "ProjectId", "RequirementId", "ChangedAt", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_req_search",
                table: "Requirements",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_ChangedByUserId",
                table: "Requirements",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "idx_team_api_key_hash",
                table: "TeamApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_team_api_key_team",
                table: "TeamApiKeys",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamApiKeys_CreatedByUserId",
                table: "TeamApiKeys",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamApiKeys_RevokedByUserId",
                table: "TeamApiKeys",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "idx_team_invite_token",
                table: "TeamInvites",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_team_invites_pending_by_team",
                table: "TeamInvites",
                columns: new[] { "TeamId", "AcceptedAt", "RevokedAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_RevokedByUserId",
                table: "TeamInvites",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_TeamId_Email",
                table: "TeamInvites",
                columns: new[] { "TeamId", "Email" },
                unique: true,
                filter: "\"AcceptedAt\" IS NULL AND \"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_team_members_user_team",
                table: "TeamMembers",
                columns: new[] { "UserId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_wil_logical",
                table: "WorkItemLinks",
                columns: new[] { "WorkItemLinkId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_wil_req",
                table: "WorkItemLinks",
                columns: new[] { "RequirementId", "WorkItemLinkId", "ChangedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "idx_wil_workitem",
                table: "WorkItemLinks",
                columns: new[] { "WorkItemId", "WorkItemLinkId", "ChangedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_ChangedByUserId",
                table: "WorkItemLinks",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "idx_wi_logical",
                table: "WorkItems",
                columns: new[] { "WorkItemId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_wi_project",
                table: "WorkItems",
                columns: new[] { "ProjectId", "WorkItemId", "ChangedAt", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_wi_search",
                table: "WorkItems",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssignedToUserId",
                table: "WorkItems",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ChangedByUserId",
                table: "WorkItems",
                column: "ChangedByUserId");

            migrationBuilder.Sql(@"
-- RLS + trigger helper functions (reference tables; created after CreateTable).
-- Order matters: LANGUAGE sql bodies are validated at CREATE time, so a function must
-- appear after every function it calls.
CREATE FUNCTION public.rls_current_user_id() RETURNS uuid
    LANGUAGE sql STABLE
    AS $$ SELECT NULLIF(current_setting('app.current_user_id', true), '')::uuid $$;
CREATE FUNCTION public.rls_is_team_member(target_team_id uuid) RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
  SELECT EXISTS (
    SELECT 1
    FROM ""TeamMembers"" tm
    WHERE tm.""TeamId"" = target_team_id
      AND tm.""UserId"" = rls_current_user_id()
  );
$$;
CREATE FUNCTION public.rls_can_access_project(target_project_id uuid) RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
  SELECT EXISTS (
    SELECT 1
    FROM ""Projects"" p
    WHERE p.""Id"" = target_project_id
      AND rls_is_team_member(p.""TeamId"")
  );
$$;
CREATE FUNCTION public.rls_can_access_feature(target_feature_id uuid) RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
  SELECT EXISTS (
    SELECT 1
    FROM ""Features"" f
    JOIN ""Projects"" p ON p.""Id"" = f.""ProjectId""
    WHERE f.""Id"" = target_feature_id
      AND f.""DeletedAt"" IS NULL
      AND p.""DeletedAt"" IS NULL
      AND rls_is_team_member(p.""TeamId"")
  );
$$;
CREATE FUNCTION public.rls_can_access_requirement(target_requirement_id uuid) RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
  SELECT EXISTS (
    SELECT 1
    FROM (
      SELECT r.""ProjectId""
      FROM ""Requirements"" r
      WHERE r.""RequirementId"" = target_requirement_id
      ORDER BY r.""ChangedAt"" DESC, r.""Id"" DESC
      LIMIT 1
    ) latest
    WHERE rls_can_access_project(latest.""ProjectId"")
  );
$$;
CREATE FUNCTION public.rls_can_access_work_item(target_work_item_id uuid) RETURNS boolean
    LANGUAGE sql STABLE
    AS $$
  SELECT EXISTS (
    SELECT 1
    FROM (
      SELECT w.""ProjectId""
      FROM ""WorkItems"" w
      WHERE w.""WorkItemId"" = target_work_item_id
      ORDER BY w.""ChangedAt"" DESC, w.""Id"" DESC
      LIMIT 1
    ) latest
    WHERE rls_can_access_project(latest.""ProjectId"")
  );
$$;
CREATE FUNCTION public.prevent_version_table_mutation() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
  RAISE EXCEPTION 'Table % is append-only. UPDATE and DELETE are not permitted.', TG_TABLE_NAME;
END;
$$;

-- Enable + force row level security
ALTER TABLE public.""Projects"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""Projects"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""Features"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""Features"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""Requirements"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""Requirements"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""WorkItems"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""WorkItems"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""WorkItemLinks"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""WorkItemLinks"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""Drafts"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""Drafts"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""ProjectEntityCounters"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""ProjectEntityCounters"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""TeamInvites"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""TeamInvites"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""JiraConnections"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""JiraConnections"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""JiraProjectMappings"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""JiraProjectMappings"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""MagicLinkTokens"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""MagicLinkTokens"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""FeedbackSubmissions"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""FeedbackSubmissions"" FORCE ROW LEVEL SECURITY;
ALTER TABLE public.""FeedbackAttachments"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""FeedbackAttachments"" FORCE ROW LEVEL SECURITY;

-- Row level security policies
CREATE POLICY rls_drafts_delete ON public.""Drafts"" FOR DELETE USING ((((""UserId"" = public.rls_current_user_id()) AND (((""Kind"" = 'Requirement'::text) AND public.rls_can_access_feature(""ScopeId"")) OR ((""Kind"" = 'WorkItem'::text) AND public.rls_can_access_project(""ScopeId"")))) OR ((public.rls_current_user_id() IS NULL) AND (""UpdatedAt"" < (now() - '1 year'::interval)))));
CREATE POLICY rls_drafts_insert ON public.""Drafts"" FOR INSERT WITH CHECK (((""UserId"" = public.rls_current_user_id()) AND (((""Kind"" = 'Requirement'::text) AND public.rls_can_access_feature(""ScopeId"")) OR ((""Kind"" = 'WorkItem'::text) AND public.rls_can_access_project(""ScopeId"")))));
CREATE POLICY rls_drafts_select ON public.""Drafts"" FOR SELECT USING (((""UserId"" = public.rls_current_user_id()) AND (((""Kind"" = 'Requirement'::text) AND public.rls_can_access_feature(""ScopeId"")) OR ((""Kind"" = 'WorkItem'::text) AND public.rls_can_access_project(""ScopeId"")))));
CREATE POLICY rls_drafts_update ON public.""Drafts"" FOR UPDATE USING (((""UserId"" = public.rls_current_user_id()) AND (((""Kind"" = 'Requirement'::text) AND public.rls_can_access_feature(""ScopeId"")) OR ((""Kind"" = 'WorkItem'::text) AND public.rls_can_access_project(""ScopeId""))))) WITH CHECK (((""UserId"" = public.rls_current_user_id()) AND (((""Kind"" = 'Requirement'::text) AND public.rls_can_access_feature(""ScopeId"")) OR ((""Kind"" = 'WorkItem'::text) AND public.rls_can_access_project(""ScopeId"")))));
CREATE POLICY rls_features_isolation ON public.""Features"" USING ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId"")))) WITH CHECK ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))));
CREATE POLICY rls_feedback_attachments_admin ON public.""FeedbackAttachments"" USING (((CURRENT_USER = 'grundet_admin'::name) OR (NULLIF(current_setting('app.admin_cli'::text, true), ''::text) = 'true'::text))) WITH CHECK (((CURRENT_USER = 'grundet_admin'::name) OR (NULLIF(current_setting('app.admin_cli'::text, true), ''::text) = 'true'::text)));
CREATE POLICY rls_feedback_attachments_owner ON public.""FeedbackAttachments"" USING ((EXISTS ( SELECT 1
   FROM public.""FeedbackSubmissions"" f
  WHERE ((f.""Id"" = ""FeedbackAttachments"".""FeedbackSubmissionId"") AND (f.""UserId"" = public.rls_current_user_id()))))) WITH CHECK ((EXISTS ( SELECT 1
   FROM public.""FeedbackSubmissions"" f
  WHERE ((f.""Id"" = ""FeedbackAttachments"".""FeedbackSubmissionId"") AND (f.""UserId"" = public.rls_current_user_id())))));
CREATE POLICY rls_feedback_submissions_admin ON public.""FeedbackSubmissions"" USING (((CURRENT_USER = 'grundet_admin'::name) OR (NULLIF(current_setting('app.admin_cli'::text, true), ''::text) = 'true'::text))) WITH CHECK (((CURRENT_USER = 'grundet_admin'::name) OR (NULLIF(current_setting('app.admin_cli'::text, true), ''::text) = 'true'::text)));
CREATE POLICY rls_feedback_submissions_owner ON public.""FeedbackSubmissions"" USING ((""UserId"" = public.rls_current_user_id())) WITH CHECK ((""UserId"" = public.rls_current_user_id()));
CREATE POLICY rls_jira_connections_isolation ON public.""JiraConnections"" USING (public.rls_is_team_member(""TeamId"")) WITH CHECK (public.rls_is_team_member(""TeamId""));
CREATE POLICY rls_jira_project_mappings_isolation ON public.""JiraProjectMappings"" USING ((public.rls_is_team_member(""TeamId"") AND (""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))))) WITH CHECK ((public.rls_is_team_member(""TeamId"") AND (""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId"")))));
CREATE POLICY rls_magic_link_tokens_delete ON public.""MagicLinkTokens"" FOR DELETE USING ((""ExpiresAt"" < now()));
CREATE POLICY rls_magic_link_tokens_insert ON public.""MagicLinkTokens"" FOR INSERT WITH CHECK (true);
CREATE POLICY rls_magic_link_tokens_select ON public.""MagicLinkTokens"" FOR SELECT USING (((""TokenHash"" = NULLIF(current_setting('app.magic_link_token_hash'::text, true), ''::text)) OR ((NULLIF(current_setting('app.magic_link_cleanup'::text, true), ''::text) = 'true'::text) AND (""ExpiresAt"" < now()))));
CREATE POLICY rls_magic_link_tokens_update ON public.""MagicLinkTokens"" FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY rls_project_entity_counters_isolation ON public.""ProjectEntityCounters"" USING ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId"")))) WITH CHECK ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))));
CREATE POLICY rls_projects_isolation ON public.""Projects"" USING (public.rls_is_team_member(""TeamId"")) WITH CHECK (public.rls_is_team_member(""TeamId""));
CREATE POLICY rls_requirements_insert ON public.""Requirements"" FOR INSERT WITH CHECK ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))));
CREATE POLICY rls_requirements_select ON public.""Requirements"" FOR SELECT USING ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))));
CREATE POLICY rls_team_invites_isolation ON public.""TeamInvites"" USING ((public.rls_is_team_member(""TeamId"") OR (EXISTS ( SELECT 1
   FROM public.""Users"" u
  WHERE ((u.""Id"" = public.rls_current_user_id()) AND (lower(u.""Email"") = lower(u.""Email""))))))) WITH CHECK ((public.rls_is_team_member(""TeamId"") OR (EXISTS ( SELECT 1
   FROM public.""Users"" u
  WHERE ((u.""Id"" = public.rls_current_user_id()) AND (lower(u.""Email"") = lower(u.""Email"")))))));
CREATE POLICY rls_work_item_links_insert ON public.""WorkItemLinks"" FOR INSERT WITH CHECK ((public.rls_can_access_work_item(""WorkItemId"") AND public.rls_can_access_requirement(""RequirementId"")));
CREATE POLICY rls_work_item_links_select ON public.""WorkItemLinks"" FOR SELECT USING ((public.rls_can_access_work_item(""WorkItemId"") AND public.rls_can_access_requirement(""RequirementId"")));
CREATE POLICY rls_work_items_insert ON public.""WorkItems"" FOR INSERT WITH CHECK ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))));
CREATE POLICY rls_work_items_select ON public.""WorkItems"" FOR SELECT USING ((""ProjectId"" IN ( SELECT p.""Id""
   FROM public.""Projects"" p
  WHERE public.rls_is_team_member(p.""TeamId""))));

-- TeamApiKeys row level security (added in AddTeamApiKeys migration).
-- Two access paths, mirroring MagicLinkTokens: team-member management, and a pre-auth
-- lookup that matches the row by a transaction-local app.api_key_hash GUC.
ALTER TABLE public.""TeamApiKeys"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""TeamApiKeys"" FORCE ROW LEVEL SECURITY;
CREATE POLICY rls_team_api_keys_select ON public.""TeamApiKeys"" FOR SELECT USING (
    public.rls_is_team_member(""TeamId"")
    OR (""KeyHash"" = NULLIF(current_setting('app.api_key_hash', true), ''))
);
CREATE POLICY rls_team_api_keys_insert ON public.""TeamApiKeys"" FOR INSERT WITH CHECK (
    public.rls_is_team_member(""TeamId"")
);
CREATE POLICY rls_team_api_keys_update ON public.""TeamApiKeys"" FOR UPDATE USING (
    public.rls_is_team_member(""TeamId"")
    OR (""KeyHash"" = NULLIF(current_setting('app.api_key_hash', true), ''))
) WITH CHECK (
    public.rls_is_team_member(""TeamId"")
    OR (""KeyHash"" = NULLIF(current_setting('app.api_key_hash', true), ''))
);
CREATE POLICY rls_team_api_keys_delete ON public.""TeamApiKeys"" FOR DELETE USING (
    public.rls_is_team_member(""TeamId"")
);

-- Attachments row level security (added in AddProjectAttachments migration).
-- Append-only blobs: no UPDATE/DELETE policy.
ALTER TABLE public.""Attachments"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.""Attachments"" FORCE ROW LEVEL SECURITY;
CREATE POLICY rls_attachments_insert ON public.""Attachments"" FOR INSERT WITH CHECK (public.rls_can_access_project(""ProjectId""));
CREATE POLICY rls_attachments_select ON public.""Attachments"" FOR SELECT USING (public.rls_can_access_project(""ProjectId""));

-- Append-only enforcement triggers
CREATE TRIGGER trg_requirements_append_only BEFORE DELETE OR UPDATE ON public.""Requirements"" FOR EACH ROW EXECUTE FUNCTION public.prevent_version_table_mutation();
CREATE TRIGGER trg_work_item_links_append_only BEFORE DELETE OR UPDATE ON public.""WorkItemLinks"" FOR EACH ROW EXECUTE FUNCTION public.prevent_version_table_mutation();
CREATE TRIGGER trg_work_items_append_only BEFORE DELETE OR UPDATE ON public.""WorkItems"" FOR EACH ROW EXECUTE FUNCTION public.prevent_version_table_mutation();

-- Current* views (security_invoker). Include AttachmentIds (added in AddProjectAttachments).
CREATE VIEW public.""CurrentRequirements"" WITH (security_invoker='true') AS
 SELECT ""Id"",
    ""RequirementId"",
    ""ProjectId"",
    ""FeatureId"",
    ""SequenceNumber"",
    ""Text"",
    ""Status"",
    ""Priority"",
    ""Labels"",
    ""AcceptanceCriteria"",
    ""AttachmentIds"",
    ""Reason"",
    ""ChangedAt"",
    ""ChangedByUserId"",
    ""ChangeType"",
    ""Rationale"",
    ""SearchVector""
   FROM ( SELECT DISTINCT ON (""Requirements"".""RequirementId"") ""Requirements"".""Id"",
            ""Requirements"".""RequirementId"",
            ""Requirements"".""ProjectId"",
            ""Requirements"".""FeatureId"",
            ""Requirements"".""SequenceNumber"",
            ""Requirements"".""Text"",
            ""Requirements"".""Status"",
            ""Requirements"".""Priority"",
            ""Requirements"".""Labels"",
            ""Requirements"".""AcceptanceCriteria"",
            ""Requirements"".""AttachmentIds"",
            ""Requirements"".""Reason"",
            ""Requirements"".""ChangedAt"",
            ""Requirements"".""ChangedByUserId"",
            ""Requirements"".""ChangeType"",
            ""Requirements"".""Rationale"",
            ""Requirements"".""SearchVector""
           FROM public.""Requirements""
          ORDER BY ""Requirements"".""RequirementId"", ""Requirements"".""ChangedAt"" DESC, ""Requirements"".""Id"" DESC) latest
  WHERE (""ChangeType"" <> 'Deleted'::text);
CREATE VIEW public.""CurrentWorkItemLinks"" WITH (security_invoker='true') AS
 SELECT ""Id"",
    ""WorkItemLinkId"",
    ""WorkItemId"",
    ""RequirementId"",
    ""ChangedAt"",
    ""ChangedByUserId"",
    ""ChangeType""
   FROM ( SELECT DISTINCT ON (""WorkItemLinks"".""WorkItemLinkId"") ""WorkItemLinks"".""Id"",
            ""WorkItemLinks"".""WorkItemLinkId"",
            ""WorkItemLinks"".""WorkItemId"",
            ""WorkItemLinks"".""RequirementId"",
            ""WorkItemLinks"".""ChangedAt"",
            ""WorkItemLinks"".""ChangedByUserId"",
            ""WorkItemLinks"".""ChangeType""
           FROM public.""WorkItemLinks""
          ORDER BY ""WorkItemLinks"".""WorkItemLinkId"", ""WorkItemLinks"".""ChangedAt"" DESC, ""WorkItemLinks"".""Id"" DESC) latest
  WHERE (""ChangeType"" <> 'Unlinked'::text);
CREATE VIEW public.""CurrentWorkItems"" WITH (security_invoker='true') AS
 SELECT ""Id"",
    ""WorkItemId"",
    ""ProjectId"",
    ""SequenceNumber"",
    ""Title"",
    ""Description"",
    ""Status"",
    ""Priority"",
    ""Type"",
    ""AssignedToUserId"",
    ""BlockedReason"",
    ""Labels"",
    ""AttachmentIds"",
    ""Reason"",
    ""ChangedAt"",
    ""ChangedByUserId"",
    ""ChangeType"",
    ""SearchVector""
   FROM ( SELECT DISTINCT ON (""WorkItems"".""WorkItemId"") ""WorkItems"".""Id"",
            ""WorkItems"".""WorkItemId"",
            ""WorkItems"".""ProjectId"",
            ""WorkItems"".""SequenceNumber"",
            ""WorkItems"".""Title"",
            ""WorkItems"".""Description"",
            ""WorkItems"".""Status"",
            ""WorkItems"".""Priority"",
            ""WorkItems"".""Type"",
            ""WorkItems"".""AssignedToUserId"",
            ""WorkItems"".""BlockedReason"",
            ""WorkItems"".""Labels"",
            ""WorkItems"".""AttachmentIds"",
            ""WorkItems"".""Reason"",
            ""WorkItems"".""ChangedAt"",
            ""WorkItems"".""ChangedByUserId"",
            ""WorkItems"".""ChangeType"",
            ""WorkItems"".""SearchVector""
           FROM public.""WorkItems""
          ORDER BY ""WorkItems"".""WorkItemId"", ""WorkItems"".""ChangedAt"" DESC, ""WorkItems"".""Id"" DESC) latest
  WHERE (""ChangeType"" <> 'Deleted'::text);

-- Append-only tables: API role may insert new versions but never mutate prior rows
REVOKE UPDATE, DELETE ON ""Requirements"" FROM grundet_api;
REVOKE UPDATE, DELETE ON ""WorkItems"" FROM grundet_api;
REVOKE UPDATE, DELETE ON ""WorkItemLinks"" FROM grundet_api;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS public.""CurrentRequirements"";
DROP VIEW IF EXISTS public.""CurrentWorkItemLinks"";
DROP VIEW IF EXISTS public.""CurrentWorkItems"";
");

            migrationBuilder.DropTable(
                name: "AlphaWaitlistEntries");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "Drafts");

            migrationBuilder.DropTable(
                name: "FeedbackAttachments");

            migrationBuilder.DropTable(
                name: "JiraConnections");

            migrationBuilder.DropTable(
                name: "JiraProjectMappings");

            migrationBuilder.DropTable(
                name: "MagicLinkTokens");

            migrationBuilder.DropTable(
                name: "ProjectEntityCounters");

            migrationBuilder.DropTable(
                name: "Requirements");

            migrationBuilder.DropTable(
                name: "TeamApiKeys");

            migrationBuilder.DropTable(
                name: "TeamInvites");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "TeamUsageSnapshots");

            migrationBuilder.DropTable(
                name: "WorkItemLinks");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.DropTable(
                name: "FeedbackSubmissions");

            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS public.prevent_version_table_mutation() CASCADE;
DROP FUNCTION IF EXISTS public.rls_can_access_feature(uuid) CASCADE;
DROP FUNCTION IF EXISTS public.rls_can_access_project(uuid) CASCADE;
DROP FUNCTION IF EXISTS public.rls_can_access_requirement(uuid) CASCADE;
DROP FUNCTION IF EXISTS public.rls_can_access_work_item(uuid) CASCADE;
DROP FUNCTION IF EXISTS public.rls_current_user_id() CASCADE;
DROP FUNCTION IF EXISTS public.rls_is_team_member(uuid) CASCADE;
DROP FUNCTION IF EXISTS public.immutable_to_tsvector(text) CASCADE;
DROP FUNCTION IF EXISTS public.immutable_text_array_to_string(text[], text) CASCADE;
");
        }
    }
}
