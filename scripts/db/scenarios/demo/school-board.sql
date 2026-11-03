-- Dev-only realistic product scenario for authenticated UI work.
--
-- Run with:
--   make db-scenarios SCENARIOS=demo/school-board
--
-- Creates a school-board classroom software decision project with enough current
-- Intent and Work rows to exercise pagination, filters, history, assignments,
-- stale snapshots, and project/team navigation.

\set now_utc 'now() AT TIME ZONE ''UTC'''

BEGIN;

CREATE OR REPLACE FUNCTION pg_temp.demo_uuid(prefix text, n int)
RETURNS uuid
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT (prefix || lpad(n::text, 12, '0'))::uuid;
$$;

-- Reset only this deterministic scenario subtree. Version tables are append-only
-- in normal app code, so the dev scenario temporarily disables those triggers.
ALTER TABLE "WorkItemLinks" DISABLE TRIGGER trg_work_item_links_append_only;
ALTER TABLE "WorkItems" DISABLE TRIGGER trg_work_items_append_only;
ALTER TABLE "Requirements" DISABLE TRIGGER trg_requirements_append_only;

DELETE FROM "WorkItemLinks"
WHERE "WorkItemId" IN (
    SELECT pg_temp.demo_uuid('60000000-0000-0000-0000-', n)
    FROM generate_series(1, 65) AS n
)
OR "RequirementId" IN (
    SELECT pg_temp.demo_uuid('50000000-0000-0000-0000-', n)
    FROM generate_series(1, 65) AS n
);

DELETE FROM "WorkItems"
WHERE "ProjectId" = '30000000-0000-0000-0000-000000000001';

DELETE FROM "Requirements"
WHERE "ProjectId" = '30000000-0000-0000-0000-000000000001';

ALTER TABLE "Requirements" ENABLE TRIGGER trg_requirements_append_only;
ALTER TABLE "WorkItems" ENABLE TRIGGER trg_work_items_append_only;
ALTER TABLE "WorkItemLinks" ENABLE TRIGGER trg_work_item_links_append_only;

DELETE FROM "Drafts"
WHERE "Kind" = 'Requirement'
AND "ScopeId" IN (
    SELECT pg_temp.demo_uuid('40000000-0000-0000-0000-', n)
    FROM generate_series(1, 5) AS n
);

DELETE FROM "Features"
WHERE "ProjectId" = '30000000-0000-0000-0000-000000000001';

DELETE FROM "ProjectEntityCounters"
WHERE "ProjectId" = '30000000-0000-0000-0000-000000000001';

DELETE FROM "TeamUsageSnapshots"
WHERE "TeamId" = '20000000-0000-0000-0000-000000000001';

DELETE FROM "JiraProjectMappings"
WHERE "ProjectId" = '30000000-0000-0000-0000-000000000001';

DELETE FROM "Projects"
WHERE "Id" = '30000000-0000-0000-0000-000000000001';

DELETE FROM "TeamMembers"
WHERE "TeamId" = '20000000-0000-0000-0000-000000000001';

DELETE FROM "Teams"
WHERE "Id" = '20000000-0000-0000-0000-000000000001';

INSERT INTO "AlphaWaitlistEntries" (
    "Id", "EmailOriginal", "EmailNormalized", "Name", "AccountType", "Status",
    "CreatedAt", "UpdatedAt", "ApprovedAt", "ApprovedBy")
SELECT
    gen_random_uuid(),
    seed."Email",
    lower(seed."Email"),
    seed."Name",
    'Personal',
    'Approved',
    :now_utc,
    :now_utc,
    :now_utc,
    'school-board-scenario'
FROM (VALUES
    ('alex@grundet.test', 'Alex Morgan'),
    ('sam@grundet.test', 'Sam Rivera')
) AS seed("Email", "Name")
ON CONFLICT ("EmailNormalized") DO UPDATE SET
    "Status" = 'Approved',
    "UpdatedAt" = EXCLUDED."UpdatedAt",
    "ApprovedAt" = COALESCE("AlphaWaitlistEntries"."ApprovedAt", EXCLUDED."ApprovedAt"),
    "ApprovedBy" = COALESCE("AlphaWaitlistEntries"."ApprovedBy", EXCLUDED."ApprovedBy");

INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES
    ('10000000-0000-0000-0000-000000000001', 'alex@grundet.test', 'Alex Morgan', :now_utc),
    ('10000000-0000-0000-0000-000000000002', 'sam@grundet.test', 'Sam Rivera', :now_utc)
ON CONFLICT ("Email") DO UPDATE SET
    "DisplayName" = COALESCE("Users"."DisplayName", EXCLUDED."DisplayName");

INSERT INTO "Teams" ("Id", "Name", "Plan", "Region", "CreatedAt", "DeletedAt")
VALUES (
    '20000000-0000-0000-0000-000000000001',
    'Northbridge School Board',
    'TeamS',
    'eu',
    :now_utc - INTERVAL '35 days',
    NULL)
ON CONFLICT ("Id") DO UPDATE SET
    "Name" = EXCLUDED."Name",
    "Plan" = EXCLUDED."Plan",
    "DeletedAt" = NULL;

INSERT INTO "TeamMembers" ("TeamId", "UserId", "Role")
SELECT
    '20000000-0000-0000-0000-000000000001'::uuid,
    u."Id",
    seed."Role"
FROM (VALUES
    ('alex@grundet.test', 'Lead'),
    ('sam@grundet.test', 'Contributor')
) AS seed("Email", "Role")
JOIN "Users" u ON u."Email" = seed."Email"
ON CONFLICT ("TeamId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role";

INSERT INTO "Projects" ("Id", "TeamId", "Name", "Description", "CreatedAt", "DeletedAt")
VALUES (
    '30000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000001',
    'Classroom Software Decision',
    'A real board-level decision project for selecting and piloting classroom software across Northbridge schools.',
    :now_utc - INTERVAL '34 days',
    NULL);

INSERT INTO "ProjectEntityCounters" ("ProjectId", "NextId")
VALUES ('30000000-0000-0000-0000-000000000001', 131);

CREATE TEMP TABLE demo_features (
    "FeatureNo" int PRIMARY KEY,
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "Label" text NOT NULL
) ON COMMIT DROP;

INSERT INTO demo_features ("FeatureNo", "Id", "Name", "Description", "Label")
VALUES
    (1, '40000000-0000-0000-0000-000000000001', 'Classroom instruction', 'Teacher-facing planning, presentation, and in-class use during normal lessons.', 'instruction'),
    (2, '40000000-0000-0000-0000-000000000002', 'Assignments and assessment', 'How pupils receive work, submit evidence, and get timely feedback.', 'assessment'),
    (3, '40000000-0000-0000-0000-000000000003', 'Guardian communication', 'What families can see and how schools communicate progress without extra burden.', 'guardians'),
    (4, '40000000-0000-0000-0000-000000000004', 'Accessibility and equity', 'Requirements that keep the decision fair across devices, languages, abilities, and school contexts.', 'equity'),
    (5, '40000000-0000-0000-0000-000000000005', 'Privacy, procurement, and rollout', 'Decision evidence for data protection, procurement, support, migration, and phased rollout.', 'rollout');

INSERT INTO "Features" ("Id", "ProjectId", "Name", "Description", "CreatedAt", "DeletedAt")
SELECT
    "Id",
    '30000000-0000-0000-0000-000000000001',
    "Name",
    "Description",
    :now_utc - INTERVAL '33 days' + ("FeatureNo" || ' hours')::interval,
    NULL
FROM demo_features;

CREATE TEMP TABLE demo_requirement_seed (
    "SequenceNumber" int PRIMARY KEY,
    "FeatureNo" int NOT NULL,
    "Text" text NOT NULL,
    "Status" text NOT NULL,
    "Priority" text NOT NULL,
    "Labels" text[] NOT NULL,
    "AcceptanceCriteria" text[] NOT NULL,
    "UpdatedText" text NULL,
    "UpdatedAcceptanceCriteria" text[] NULL,
    "Reason" text NULL,
    "Rationale" text NULL
) ON COMMIT DROP;

INSERT INTO demo_requirement_seed (
    "SequenceNumber", "FeatureNo", "Text", "Status", "Priority", "Labels", "AcceptanceCriteria", "UpdatedText", "UpdatedAcceptanceCriteria", "Reason", "Rationale")
VALUES
    (1, 1, 'Teachers can open a prepared lesson plan in under two minutes from the classroom device.', 'Active', 'Must', ARRAY['instruction','teacher-workflow','pilot']::text[], ARRAY['A pilot teacher can find today''s lesson without searching by file name.', 'The plan opens on a managed classroom laptop and projector.', 'Cold-start timing is measured during at least three pilot lessons.']::text[], 'Teachers can open today''s prepared lesson plan in under two minutes from the managed classroom device, including projector setup.', ARRAY['A pilot teacher can find today''s lesson without searching by file name.', 'The plan opens on a managed classroom laptop and projector.', 'Cold-start timing is measured during at least three pilot lessons.', 'The timer includes sign-in only when the teacher starts from a signed-out device.']::text[], NULL, 'Lost instructional minutes at class start are the highest teacher complaint in the pilot, so fast access directly protects teaching time.'),
    (2, 1, 'Teachers can present lesson material without exposing private pupil records on the projector.', 'Active', 'Must', ARRAY['instruction','privacy','classroom']::text[], ARRAY['Private notes and assessment records are hidden in presentation mode.', 'The teacher can switch between presentation and planning views deliberately.', 'A privacy check is included in pilot observation notes.']::text[], 'Teachers can present lesson material without exposing private pupil records, guardian messages, or assessment notes on the projector.', NULL, NULL, 'Accidental display of private records to the whole class is a critical privacy risk during instruction.'),
    (3, 1, 'The system supports offline continuation for a lesson already opened before a network interruption.', 'Active', 'Should', ARRAY['instruction','resilience','network']::text[], ARRAY['A teacher can continue using an opened lesson for at least 30 minutes without internet.', 'The UI clearly indicates that sync is paused.', 'No pupil data is lost when the connection returns.']::text[], 'The system supports offline continuation for a lesson opened before a network interruption, with clear sync status for the teacher.', NULL, NULL, 'Many schools have intermittent network issues, so offline resilience is essential to lesson continuity.'),
    (4, 1, 'Teachers can attach district-approved resources to a lesson without leaving the planning workflow.', 'Active', 'Should', ARRAY['instruction','resources']::text[], ARRAY['Approved resource libraries are searchable from lesson planning.', 'Attached resources retain attribution and licence metadata.', 'Unapproved external links are visibly marked for review.']::text[], 'Teachers can attach district-approved resources to a lesson without leaving the planning workflow or bypassing licence metadata.', NULL, NULL, 'Friction in adding resources causes teachers to work around approvals, undermining the governance the district needs.'),
    (5, 1, 'Lesson templates can be shared within a school while preserving who last changed them.', 'Active', 'Should', ARRAY['instruction','collaboration','history']::text[], ARRAY['A template can be shared with all teachers in one school.', 'The last editor and last change time are visible.', 'Teachers can copy a shared template without changing the original.']::text[], 'Lesson templates can be shared within a school while preserving attribution, last editor, and last change time.', NULL, NULL, 'Clear attribution of changes strengthens trust in shared templates and accountability for quality.'),
    (6, 1, 'Teachers can mark lesson segments as completed during class to help with later reflection.', 'Active', 'Could', ARRAY['instruction','reflection']::text[], ARRAY['A teacher can mark a segment complete with one action.', 'Completion markers are private to the teacher by default.', 'The markers are visible in the next planning session.']::text[], NULL, NULL, NULL, 'Reflection on lesson progress is known to improve teacher planning effectiveness in subsequent cycles.'),
    (7, 1, 'The selected product shows a simple pupil view that works on tablets used in lower grades.', 'Active', 'Must', ARRAY['instruction','pupil-view','devices']::text[], ARRAY['The pupil view is usable on the district standard tablet.', 'Primary-grade pupils can identify the current activity in usability testing.', 'The view avoids teacher-only controls.']::text[], NULL, NULL, NULL, 'Younger pupils cannot navigate complexity, so simplicity on available devices is non-negotiable for lower grades.'),
    (8, 1, 'Teachers can reuse a lesson from a previous term without copying stale due dates.', 'Active', 'Should', ARRAY['instruction','reuse','calendar']::text[], ARRAY['Reused lessons prompt for new dates.', 'Old pupil submissions are not copied.', 'The teacher can review all date changes before saving.']::text[], NULL, NULL, NULL, 'Reuse friction is a common reason teachers rebuild lessons instead of improving existing ones.'),
    (9, 1, 'In-class polls can be run without requiring pupils to create separate vendor accounts.', 'Active', 'Should', ARRAY['instruction','polling','identity']::text[], ARRAY['Pupils can join using district identity or a classroom-safe session code.', 'No additional vendor account is created for a pupil.', 'Poll results are exportable for teacher reflection.']::text[], NULL, NULL, NULL, 'Account friction delays lessons and creates untracked shadow accounts, defeating adoption goals.'),
    (10, 1, 'Teachers can see which pupils have not opened the current activity without showing grades publicly.', 'Active', 'Must', ARRAY['instruction','monitoring','privacy']::text[], ARRAY['The teacher dashboard shows activity access status.', 'Grades and private comments are not shown in whole-class view.', 'The status refreshes during the lesson without manual reload.']::text[], NULL, NULL, NULL, 'Monitoring is essential to real-time instruction, but public grade exposure prevents equitable participation.'),
    (11, 1, 'The product supports co-teaching so two teachers can manage the same live lesson.', 'Draft', 'Could', ARRAY['instruction','co-teaching']::text[], ARRAY['A second teacher can be added to a lesson.', 'Both teachers can see live activity status.', 'Conflicting edits are handled without data loss.']::text[], 'The product supports co-teaching so two authorized teachers can manage the same live lesson without overwriting each other.', NULL, NULL, 'Co-teaching is a widespread model in the district, so the product must support it without edit conflicts.'),
    (12, 1, 'Teachers can flag a lesson as needing curriculum review before it is reused district-wide.', 'Active', 'Could', ARRAY['instruction','curriculum-review']::text[], ARRAY['A review flag can be added from the lesson.', 'Curriculum reviewers can list flagged lessons.', 'The flag has a short explanation field.']::text[], 'Teachers can flag a lesson as needing curriculum review before it is reused district-wide, including a short reason for reviewers.', NULL, NULL, 'Quality assurance before reuse prevents weak lessons from spreading and protects the district''s instructional consistency.'),
    (13, 1, 'Classroom use can continue when a substitute teacher needs temporary access for the day.', 'Active', 'Should', ARRAY['instruction','substitute-access']::text[], ARRAY['A school administrator can grant time-boxed substitute access.', 'Access expires automatically.', 'The access grant is auditable.']::text[], 'Classroom use can continue when a substitute teacher needs auditable, time-boxed access for the school day.', NULL, NULL, 'Substitutes are frequent, so access control that expires automatically prevents accidental over-provisioning.'),
    (14, 2, 'Pupils can see all assigned work for the week in one place.', 'Active', 'Must', ARRAY['assessment','pupil-workflow','calendar']::text[], ARRAY['Assignments are grouped by due date.', 'Completed and missing work are visually distinct.', 'The weekly view works on tablet and laptop.']::text[], 'Pupils can see all assigned work for the week in one place, grouped by due date and clearly separated by completion state.', NULL, NULL, 'A single view of all work reduces pupil anxiety and helps them plan their time across classes.'),
    (15, 2, 'Teachers can create an assignment from a lesson activity without re-entering the same instructions.', 'Active', 'Must', ARRAY['assessment','teacher-workflow']::text[], ARRAY['Lesson activity instructions can be reused as assignment instructions.', 'The teacher can edit the assignment before publishing.', 'The original lesson content is not changed by assignment edits.']::text[], 'Teachers can create an assignment from a lesson activity without re-entering instructions or changing the original lesson.', NULL, NULL, 'Re-entry friction causes teachers to diverge lesson and assignment intent, creating confusion about what is current.'),
    (16, 2, 'Submitted pupil work keeps the original submission timestamp even if feedback is added later.', 'Active', 'Must', ARRAY['assessment','audit','feedback']::text[], ARRAY['Submission timestamps are immutable.', 'Feedback timestamps are recorded separately.', 'Teachers can see late submissions according to school policy.']::text[], 'Submitted pupil work keeps the original submission timestamp even when feedback, rubric scores, or comments are added later.', NULL, NULL, 'Immutable submission timestamps are legally required for fair grading and complaint resolution.'),
    (17, 2, 'Teachers can give rubric-based feedback that pupils can understand without opening a separate document.', 'Active', 'Should', ARRAY['assessment','feedback','rubrics']::text[], ARRAY['Rubric levels and comments appear next to the submitted work.', 'Pupils can view feedback on the same device used for submission.', 'The rubric can be copied for similar assignments.']::text[], 'Teachers can give rubric-based feedback that pupils can understand in context, without opening a separate document.', NULL, NULL, 'In-context feedback increases uptake; separate documents are often ignored or misplaced by pupils.'),
    (18, 2, 'A teacher can return work for revision while preserving the previous submitted version.', 'Active', 'Should', ARRAY['assessment','revision','history']::text[], ARRAY['Returned work has a clear revision request.', 'Previous submissions remain visible to the teacher.', 'The pupil can resubmit without losing the teacher comment.']::text[], 'A teacher can return work for revision while preserving previous submissions and the revision request.', NULL, NULL, 'Visible revision history demonstrates iterative learning and helps teachers track progress through multiple attempts.'),
    (19, 2, 'The system highlights missing work without ranking pupils publicly.', 'Active', 'Must', ARRAY['assessment','privacy','missing-work']::text[], ARRAY['Teachers can filter for missing work.', 'Pupils only see their own missing work.', 'No public leaderboard or ranking is shown.']::text[], 'The system highlights missing work for teachers and individual pupils without ranking pupils publicly.', NULL, NULL, 'Missing work visibility is needed for intervention, but public ranking triggers shame and disengagement.'),
    (20, 2, 'Assessment exports include enough context for term-end reporting without manual spreadsheet cleanup.', 'Active', 'Should', ARRAY['assessment','reporting','exports']::text[], ARRAY['Exports include pupil, assignment, due date, status, and rubric level.', 'Column names are stable across exports.', 'A pilot administrator can import the export into the current reporting spreadsheet.']::text[], 'Assessment exports include enough context for term-end reporting without manual spreadsheet cleanup by school administrators.', NULL, NULL, 'Manual cleanup is error-prone and delays reporting, so the export must include all necessary fields with stable column names.'),
    (21, 2, 'Teachers can set differentiated due dates for selected pupils when an accommodation requires it.', 'Active', 'Must', ARRAY['assessment','accommodation','due-dates']::text[], ARRAY['A due date can differ for selected pupils.', 'Other pupils cannot see the accommodation.', 'The teacher can audit which pupils have adjusted dates.']::text[], NULL, NULL, NULL, 'Accommodation equity is a legal requirement, and hidden dates prevent stigma that public differentiation would create.'),
    (22, 2, 'Pupils receive a clear confirmation after submitting work.', 'Active', 'Must', ARRAY['assessment','submission','pupil-workflow']::text[], ARRAY['The confirmation includes assignment name and submission time.', 'The confirmation remains available after navigating away.', 'Duplicate accidental submissions are handled gracefully.']::text[], NULL, NULL, NULL, 'Submissions are often ambiguous to pupils; clear confirmation prevents duplicate submissions and anxiety.'),
    (23, 2, 'Teachers can schedule assignments to publish at a future date.', 'Active', 'Should', ARRAY['assessment','scheduling']::text[], ARRAY['A future publish time can be selected.', 'Scheduled assignments are visible to the teacher before publishing.', 'Pupils cannot access unpublished scheduled work.']::text[], NULL, NULL, NULL, 'Advance scheduling lets teachers prepare assignments ahead of time without pupils seeing drafts.'),
    (24, 2, 'Group assignments can identify both the group submission and individual participation notes.', 'Draft', 'Could', ARRAY['assessment','groups']::text[], ARRAY['A group submission can be linked to multiple pupils.', 'The teacher can add private individual participation notes.', 'Pupils see group feedback but not private notes.']::text[], NULL, NULL, NULL, 'Individual accountability in group work is essential for fair grading and identifying pupils who need support.'),
    (25, 2, 'Teachers can quickly duplicate a successful assignment for another class section.', 'Active', 'Should', ARRAY['assessment','reuse']::text[], ARRAY['Duplication preserves instructions and rubric.', 'Class-specific dates are reset.', 'The teacher reviews recipients before publishing.']::text[], NULL, NULL, NULL, 'Duplication efficiency encourages reuse of proven assignments across sections.'),
    (26, 2, 'Assignment attachments are scanned or blocked according to district safety policy.', 'Active', 'Must', ARRAY['assessment','attachments','safety']::text[], ARRAY['Unsafe attachment types are blocked.', 'Blocked uploads explain the reason to the pupil.', 'Teachers can see that an attempted upload was blocked.']::text[], NULL, NULL, NULL, 'Unscanned attachments are a malware vector; blocking must be transparent to pupils so they can recover.'),
    (27, 3, 'Guardians can see current assignments and due dates without seeing other pupils'' information.', 'Active', 'Must', ARRAY['guardians','privacy','assignments']::text[], ARRAY['Guardian view shows only linked pupils.', 'Assignment status and due dates are visible.', 'No class roster or other pupil data is exposed.']::text[], NULL, NULL, NULL, 'Information isolation prevents accidental exposure of class rosters and protects family privacy.'),
    (28, 3, 'Schools can send a class announcement to guardians with translation support.', 'Active', 'Should', ARRAY['guardians','translation','announcements']::text[], ARRAY['Announcements support district-approved translation.', 'The original message remains available.', 'Delivery status can be reviewed by school staff.']::text[], NULL, NULL, NULL, 'Language barriers prevent guardians from participating; translation removes that obstacle while maintaining the original for records.'),
    (29, 3, 'Teachers can choose whether feedback comments are visible to guardians.', 'Active', 'Should', ARRAY['guardians','feedback','privacy']::text[], ARRAY['Feedback visibility can be set per assignment.', 'The default matches school policy.', 'Teachers see a preview of guardian-visible content.']::text[], NULL, NULL, NULL, 'Some feedback is sensitive or incomplete; teacher control prevents misinterpretation by families.'),
    (30, 3, 'Guardian access can be paused immediately when custody or contact records change.', 'Active', 'Must', ARRAY['guardians','access-control','safety']::text[], ARRAY['School administrators can pause access for a guardian.', 'Paused access takes effect immediately.', 'The pause action is recorded for audit.']::text[], NULL, NULL, NULL, 'Custody changes require immediate action; delayed access revocation is a safety risk.'),
    (31, 3, 'The product provides a low-noise digest so guardians are not notified for every small classroom update.', 'Active', 'Should', ARRAY['guardians','notifications']::text[], ARRAY['Guardians can receive a daily or weekly digest.', 'Urgent school messages can still be sent immediately.', 'Digest settings are understandable during onboarding.']::text[], NULL, NULL, NULL, 'Digest options reduce notification fatigue while preserving access to urgent messages.'),
    (32, 3, 'Guardians can acknowledge school forms or policy notices when the board requires confirmation.', 'Active', 'Could', ARRAY['guardians','forms','compliance']::text[], ARRAY['A guardian can acknowledge a notice.', 'Staff can report who has not acknowledged.', 'The acknowledgement text and time are retained.']::text[], NULL, NULL, NULL, 'Acknowledgement records are needed for legal and policy compliance.'),
    (33, 3, 'Family communication logs are visible to authorized school staff but not to pupils.', 'Active', 'Must', ARRAY['guardians','communication-log','privacy']::text[], ARRAY['Authorized staff can review guardian messages.', 'Pupils cannot access guardian communication logs.', 'Access follows team and school permissions.']::text[], NULL, NULL, NULL, 'Staff need visibility for safeguarding, but pupil access would undermine parent-school confidentiality.'),
    (34, 3, 'Guardians can update preferred communication language without contacting the district help desk.', 'Active', 'Should', ARRAY['guardians','language','self-service']::text[], ARRAY['A guardian can select a preferred language.', 'The preference is visible to school staff.', 'Changes are reflected in future announcements.']::text[], NULL, NULL, NULL, 'Self-service language selection reduces support load and respects family preferences.'),
    (35, 3, 'Teachers can message guardians from assignment context without copying pupil details into email.', 'Active', 'Should', ARRAY['guardians','teacher-workflow','messages']::text[], ARRAY['The message can reference an assignment.', 'Sensitive pupil records are not automatically copied.', 'The sent message is recorded in the communication log.']::text[], NULL, NULL, NULL, 'Copying pupil details to email creates uncontrolled shadow records outside the secure system.'),
    (36, 3, 'Guardian accounts use district-approved identity verification before any pupil information is shown.', 'Active', 'Must', ARRAY['guardians','identity','security']::text[], ARRAY['A guardian must be linked through district records.', 'Unverified accounts cannot see pupil data.', 'Failed verification attempts are logged.']::text[], NULL, NULL, NULL, 'Verification prevents impostor guardians from accessing pupil records.'),
    (37, 3, 'Schools can export communication evidence for a specific pupil meeting.', 'Active', 'Could', ARRAY['guardians','exports','meetings']::text[], ARRAY['Authorized staff can export messages for one pupil.', 'The export has date range controls.', 'The export includes who sent each message.']::text[], NULL, NULL, NULL, 'Pupil-specific exports are needed for meetings without revealing communication with other families.'),
    (38, 3, 'Guardians can opt into SMS reminders only where district policy allows it.', 'Draft', 'Could', ARRAY['guardians','sms','policy']::text[], ARRAY['SMS can be disabled district-wide.', 'Opt-in language is explicit.', 'Phone numbers are not used for unrelated vendor marketing.']::text[], NULL, NULL, NULL, 'SMS is a sensitive channel; opt-in and governance prevent misuse.'),
    (39, 3, 'Emergency announcements remain clearly separate from routine classroom updates.', 'Active', 'Must', ARRAY['guardians','emergency','notifications']::text[], ARRAY['Emergency announcements have distinct visual treatment.', 'Routine digest settings do not delay emergency messages.', 'Staff can confirm delivery status.']::text[], NULL, NULL, NULL, 'Clear differentiation and prioritization ensure urgent messages reach guardians immediately.'),
    (40, 4, 'The product works on district Chromebooks that are four years old.', 'Active', 'Must', ARRAY['equity','devices','performance']::text[], ARRAY['Pilot testing includes older managed Chromebooks.', 'Common pupil tasks remain responsive.', 'Unsupported device limitations are documented before procurement.']::text[], NULL, NULL, NULL, 'Device diversity is a reality in schools; performance on older hardware must be proven before committing.'),
    (41, 4, 'Core pupil workflows meet WCAG 2.2 AA expectations before district-wide rollout.', 'Active', 'Must', ARRAY['equity','accessibility','wcag']::text[], ARRAY['Keyboard navigation is tested for core pupil workflows.', 'Screen reader checks cover assignments and feedback.', 'Known exceptions have mitigation plans.']::text[], NULL, NULL, NULL, 'Accessibility is a legal requirement; testing and documented mitigations prove good-faith compliance.'),
    (42, 4, 'Pupils can use translated instructions without losing teacher-provided context.', 'Active', 'Should', ARRAY['equity','translation','pupil-workflow']::text[], ARRAY['Translated instructions keep teacher formatting.', 'The original text is still available.', 'Translation limitations are communicated to teachers.']::text[], NULL, NULL, NULL, 'Translations without the original or formatting create confusion about what the teacher intended.'),
    (43, 4, 'The system supports accommodations without visibly labelling pupils to classmates.', 'Active', 'Must', ARRAY['equity','accommodation','privacy']::text[], ARRAY['Accommodation controls are private to staff and the pupil.', 'Classmates cannot infer accommodations from shared views.', 'Audit evidence shows who changed accommodation settings.']::text[], NULL, NULL, NULL, 'Visible accommodations stigmatize pupils; privacy protects dignity and equal participation.'),
    (44, 4, 'Low-bandwidth households can still review assignments and teacher feedback.', 'Active', 'Should', ARRAY['equity','home-access','network']::text[], ARRAY['Assignment pages load acceptably on a constrained connection.', 'Large media has a lower-bandwidth alternative.', 'Guardians can identify when a resource requires high bandwidth.']::text[], NULL, NULL, NULL, 'Low-bandwidth homes are common; transparency lets families plan data use and access strategies.'),
    (45, 4, 'The board can compare pilot feedback across schools with different demographics.', 'Active', 'Should', ARRAY['equity','pilot','reporting']::text[], ARRAY['Pilot feedback can be grouped by school.', 'Reports avoid exposing individual pupil identities.', 'The board can see participation gaps.']::text[], NULL, NULL, NULL, 'Demographic analysis reveals inequities; the board needs this evidence to make equitable rollout decisions.'),
    (46, 4, 'The product does not require paid home software for pupils to complete assigned work.', 'Active', 'Must', ARRAY['equity','home-access','cost']::text[], ARRAY['All required pupil tasks work with district-provided tools.', 'Optional third-party tools are clearly marked optional.', 'No assignment requires a family to buy software.']::text[], NULL, NULL, NULL, 'Cost barriers exclude families and violate equity commitments; free-software-only is non-negotiable.'),
    (47, 4, 'Teachers can provide audio instructions for pupils who need them.', 'Active', 'Could', ARRAY['equity','audio','accessibility']::text[], ARRAY['A teacher can attach audio instructions.', 'Audio has a text alternative.', 'Pupils can replay audio without leaving the task.']::text[], NULL, NULL, NULL, 'Audio supports emergent readers and pupils with visual processing differences.'),
    (48, 4, 'Accessibility issues found during pilot have an owner and decision deadline.', 'Active', 'Must', ARRAY['equity','pilot','risk']::text[], ARRAY['Each issue has an owner.', 'Each issue has a mitigation or rejection decision.', 'Unresolved must-fix issues block rollout recommendation.']::text[], NULL, NULL, NULL, 'Accountability for accessibility prevents deferral; documented decisions show good-faith engagement with accessibility.'),
    (49, 4, 'The product supports right-to-left language display where required by families in the district.', 'Draft', 'Could', ARRAY['equity','language','rtl']::text[], ARRAY['RTL content renders correctly in guardian and pupil views.', 'Mixed LTR/RTL text remains readable.', 'Known gaps are documented for board review.']::text[], NULL, NULL, NULL, 'Right-to-left language support is required for inclusive family engagement.'),
    (50, 4, 'Pupil-facing error messages are understandable without teacher interpretation.', 'Active', 'Should', ARRAY['equity','usability','pupil-workflow']::text[], ARRAY['Common errors use pupil-friendly language.', 'Errors explain what to do next.', 'Pilot pupils can recover from a failed upload without adult help.']::text[], NULL, NULL, NULL, 'Confusing errors frustrate pupils and create support burden; clear recovery instructions build independence.'),
    (51, 4, 'The software can be used by a teacher with only the standard district training session.', 'Active', 'Should', ARRAY['equity','training','teacher-workflow']::text[], ARRAY['A new pilot teacher completes setup after standard training.', 'Help content is available in the product.', 'Training needs are captured for procurement scoring.']::text[], NULL, NULL, NULL, 'Training burden affects adoption; in-product help reduces teacher dependency and reveals training gaps.'),
    (52, 5, 'Vendor data processing terms satisfy the district privacy review before any pupil pilot.', 'Active', 'Must', ARRAY['rollout','privacy','procurement']::text[], ARRAY['The DPA is reviewed by district counsel.', 'Data subprocessors are documented.', 'The pilot does not start until privacy sign-off is recorded.']::text[], NULL, NULL, NULL, 'Legal review and signed documentation protect the district and pupils from privacy liability.'),
    (53, 5, 'The board can see total first-year cost including licences, training, migration, and support.', 'Active', 'Must', ARRAY['rollout','cost','board-decision']::text[], ARRAY['Cost summary includes all known first-year categories.', 'One-time and recurring costs are separated.', 'Unknown cost assumptions are explicitly listed.']::text[], NULL, NULL, NULL, 'Complete cost visibility is essential to fair board comparison and prevents hidden costs from derailing the decision.'),
    (54, 5, 'Procurement scoring links each vendor claim back to observed pilot evidence.', 'Active', 'Should', ARRAY['rollout','procurement','evidence']::text[], ARRAY['Each scored category has evidence notes.', 'Pilot observations can contradict vendor claims.', 'Board reviewers can trace why a score changed.']::text[], NULL, NULL, NULL, 'Evidence-based scoring prevents vendor bias and supports defensible board decisions.'),
    (55, 5, 'Rollout can be phased by school so support load is manageable.', 'Active', 'Must', ARRAY['rollout','support','schools']::text[], ARRAY['A phased rollout plan exists before board approval.', 'Support staffing assumptions are visible.', 'Schools can be grouped by readiness.']::text[], NULL, NULL, NULL, 'Phased rollout reduces support overload and lets early adopters build expertise for later schools.'),
    (56, 5, 'Existing classroom content can be migrated selectively rather than all at once.', 'Active', 'Should', ARRAY['rollout','migration','content']::text[], ARRAY['Teachers can identify content that should be migrated.', 'Old content can remain archived.', 'Migration effort is estimated before procurement decision.']::text[], NULL, NULL, NULL, 'Selective migration is faster and less risky than all-at-once moves.'),
    (57, 5, 'District administrators can disable integrations that are not approved for pupil data.', 'Active', 'Must', ARRAY['rollout','integrations','privacy']::text[], ARRAY['Unapproved integrations can be disabled centrally.', 'Teachers cannot re-enable disabled integrations.', 'The disabled state is included in pilot configuration evidence.']::text[], NULL, NULL, NULL, 'Governance prevents unauthorized pupil-data sharing by well-meaning teachers.'),
    (58, 5, 'Support tickets during the pilot are categorized so the board can see operational risk.', 'Active', 'Should', ARRAY['rollout','support','pilot']::text[], ARRAY['Pilot tickets are tagged by category.', 'Repeated issues are visible in weekly review.', 'Vendor response times are recorded.']::text[], NULL, NULL, NULL, 'Categorization reveals patterns (training vs. product vs. infrastructure); tracking response times holds vendors accountable.'),
    (59, 5, 'The final board recommendation includes explicit trade-offs, not only a vendor ranking.', 'Active', 'Must', ARRAY['rollout','board-decision','tradeoffs']::text[], ARRAY['Recommendation lists major benefits and risks.', 'Rejected alternatives include rationale.', 'Open issues are visible at decision time.']::text[], NULL, NULL, NULL, 'Transparent trade-off documentation supports board confidence and prevents buyer''s remorse.'),
    (60, 5, 'School leaders can confirm readiness before their school enters the rollout phase.', 'Active', 'Should', ARRAY['rollout','readiness','schools']::text[], ARRAY['Each school has a readiness checklist.', 'A school leader can mark readiness with notes.', 'Blocked schools are visible to the district rollout owner.']::text[], NULL, NULL, NULL, 'Readiness assessment prevents chaos; visibility lets the district provide support to blocked schools.'),
    (61, 5, 'The district can import historical grades from every legacy gradebook.', 'Archived', 'Wont', ARRAY['rollout','migration','legacy']::text[], ARRAY['The board can see why full historical import is not in the pilot scope.', 'Critical reporting data is handled through the existing archive process.', 'The decision is visible in version history.']::text[], 'The district will not require full historical grade import from every legacy gradebook for the pilot decision.', NULL, 'Archived after board scope review: full historical grade migration is too large for the first decision cycle.', 'Full historical migration is too large for the first decision cycle.'),
    (62, 5, 'Teachers can use any consumer video platform inside classroom assignments.', 'WontDo', 'Wont', ARRAY['rollout','privacy','video']::text[], ARRAY['The decision record explains why unrestricted consumer video tools are excluded.', 'Approved media options remain available.', 'Teachers get guidance on compliant alternatives.']::text[], 'Teachers cannot embed arbitrary consumer video platforms inside classroom assignments; only district-approved media sources are in scope.', NULL, 'Rejected because unrestricted consumer video creates avoidable privacy and moderation risk.', 'Unrestricted consumer video creates avoidable privacy and moderation risk.'),
    (63, 5, 'The pilot exit report distinguishes product gaps from training gaps.', 'Active', 'Should', ARRAY['rollout','pilot','reporting']::text[], ARRAY['Pilot issues can be tagged as product, training, policy, or infrastructure.', 'The final report summarizes each category.', 'Training gaps have proposed mitigation before rollout.']::text[], NULL, NULL, NULL, 'Categorization prevents blaming the product for training or policy issues; it clarifies what the product actually needs to fix.'),
    (64, 5, 'Vendor support commitments are documented before contract negotiation.', 'Active', 'Must', ARRAY['rollout','vendor','support']::text[], ARRAY['Support hours and response targets are recorded.', 'Escalation contacts are known.', 'Commitments are available to procurement reviewers.']::text[], NULL, NULL, NULL, 'Documented support commitments prevent surprises and let the board assess vendor reliability.'),
    (65, 5, 'The board can postpone the decision if must-have privacy or accessibility evidence is missing.', 'Active', 'Must', ARRAY['rollout','board-decision','risk']::text[], ARRAY['Must-have evidence gaps are visible.', 'The recommendation can explicitly be postpone.', 'A postponed decision has owners for missing evidence.']::text[], NULL, NULL, NULL, 'Postponement is a valid decision; ownership for closing gaps prevents indefinite delay.');

WITH users AS (
    SELECT
        (SELECT "Id" FROM "Users" WHERE "Email" = 'alex@grundet.test') AS alex_id,
        (SELECT "Id" FROM "Users" WHERE "Email" = 'sam@grundet.test') AS sam_id
)
INSERT INTO "Requirements" (
    "Id", "RequirementId", "ProjectId", "FeatureId", "SequenceNumber", "Text", "Status", "Priority",
    "Labels", "AcceptanceCriteria", "Rationale", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    pg_temp.demo_uuid('51000000-0000-0000-0000-', r."SequenceNumber"),
    pg_temp.demo_uuid('50000000-0000-0000-0000-', r."SequenceNumber"),
    '30000000-0000-0000-0000-000000000001',
    f."Id",
    r."SequenceNumber",
    CASE WHEN r."UpdatedText" IS NULL THEN r."Text" ELSE 'Initial draft: ' || r."Text" END,
    CASE WHEN r."UpdatedText" IS NULL THEN r."Status" ELSE 'Draft' END,
    r."Priority",
    r."Labels",
    r."AcceptanceCriteria",
    r."Rationale",
    NULL,
    :now_utc - INTERVAL '28 days' + (r."SequenceNumber" || ' minutes')::interval,
    users.alex_id,
    'Created'
FROM demo_requirement_seed r
JOIN demo_features f ON f."FeatureNo" = r."FeatureNo"
CROSS JOIN users;

WITH users AS (
    SELECT (SELECT "Id" FROM "Users" WHERE "Email" = 'sam@grundet.test') AS sam_id
)
INSERT INTO "Requirements" (
    "Id", "RequirementId", "ProjectId", "FeatureId", "SequenceNumber", "Text", "Status", "Priority",
    "Labels", "AcceptanceCriteria", "Rationale", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    pg_temp.demo_uuid('52000000-0000-0000-0000-', r."SequenceNumber"),
    pg_temp.demo_uuid('50000000-0000-0000-0000-', r."SequenceNumber"),
    '30000000-0000-0000-0000-000000000001',
    f."Id",
    r."SequenceNumber",
    r."UpdatedText",
    r."Status",
    r."Priority",
    r."Labels",
    COALESCE(r."UpdatedAcceptanceCriteria", r."AcceptanceCriteria"),
    r."Rationale",
    r."Reason",
    :now_utc - INTERVAL '12 days' + (r."SequenceNumber" || ' minutes')::interval,
    users.sam_id,
    'Updated'
FROM demo_requirement_seed r
JOIN demo_features f ON f."FeatureNo" = r."FeatureNo"
CROSS JOIN users
WHERE r."UpdatedText" IS NOT NULL;

CREATE TEMP TABLE demo_work_seed AS
SELECT
    r."SequenceNumber" AS "WorkNo",
    r."SequenceNumber" + 65 AS "SequenceNumber",
    CASE (r."SequenceNumber" - 1) % 6
        WHEN 0 THEN 'Interview pilot users for IN-' || r."SequenceNumber"
        WHEN 1 THEN 'Prototype board evidence for IN-' || r."SequenceNumber"
        WHEN 2 THEN 'Review policy and privacy impact for IN-' || r."SequenceNumber"
        WHEN 3 THEN 'Prepare classroom trial checklist for IN-' || r."SequenceNumber"
        WHEN 4 THEN 'Validate accessibility and equity evidence for IN-' || r."SequenceNumber"
        ELSE 'Write rollout decision note for IN-' || r."SequenceNumber"
    END AS "Title",
    'Collect decision evidence for intent IN-' || r."SequenceNumber" || ': ' || r."Text" AS "Description",
    CASE
        WHEN r."SequenceNumber" IN (1,5,8,12,17,22,29,34,41,48,55,63) THEN 'Done'
        WHEN r."SequenceNumber" IN (2,6,9,13,18,24,31,37,44,51,59) THEN 'InProgress'
        WHEN r."SequenceNumber" IN (20,46) THEN 'WontDo'
        ELSE 'Todo'
    END AS "Status",
    CASE r."Priority"
        WHEN 'Must' THEN 'High'
        WHEN 'Should' THEN 'Medium'
        ELSE 'Low'
    END AS "Priority",
    CASE (r."SequenceNumber" - 1) % 3
        WHEN 0 THEN 'Task'
        WHEN 1 THEN 'Feature'
        ELSE 'Bug'
    END AS "Type",
    r."Labels" || ARRAY['work']::text[] AS "Labels",
    CASE
        WHEN r."SequenceNumber" IN (9,31,52) THEN 'Waiting for vendor evidence before the board packet can be finalized.'
        ELSE NULL
    END AS "BlockedReason",
    CASE
        WHEN r."SequenceNumber" IN (1,5,8,12,17,22,29,34,41,48,55,63) THEN 'Evidence accepted for the current pilot review cycle.'
        WHEN r."SequenceNumber" IN (20,46) THEN 'Dropped from active work after scope review; the intent remains visible for decision history.'
        ELSE NULL
    END AS "Reason"
FROM demo_requirement_seed r;

WITH users AS (
    SELECT
        (SELECT "Id" FROM "Users" WHERE "Email" = 'alex@grundet.test') AS alex_id,
        (SELECT "Id" FROM "Users" WHERE "Email" = 'sam@grundet.test') AS sam_id
)
INSERT INTO "WorkItems" (
    "Id", "WorkItemId", "ProjectId", "SequenceNumber", "Title", "Description", "Status", "Priority", "Type",
    "AssignedToUserId", "BlockedReason", "Labels", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    pg_temp.demo_uuid('61000000-0000-0000-0000-', w."WorkNo"),
    pg_temp.demo_uuid('60000000-0000-0000-0000-', w."WorkNo"),
    '30000000-0000-0000-0000-000000000001',
    w."SequenceNumber",
    'Draft work: ' || w."Title",
    w."Description",
    'Todo',
    w."Priority",
    w."Type",
    CASE WHEN w."WorkNo" % 2 = 0 THEN users.sam_id ELSE users.alex_id END,
    NULL,
    w."Labels",
    NULL,
    :now_utc - INTERVAL '24 days' + (w."WorkNo" || ' minutes')::interval,
    users.alex_id,
    'Created'
FROM demo_work_seed w
CROSS JOIN users;

WITH users AS (
    SELECT
        (SELECT "Id" FROM "Users" WHERE "Email" = 'alex@grundet.test') AS alex_id,
        (SELECT "Id" FROM "Users" WHERE "Email" = 'sam@grundet.test') AS sam_id
)
INSERT INTO "WorkItems" (
    "Id", "WorkItemId", "ProjectId", "SequenceNumber", "Title", "Description", "Status", "Priority", "Type",
    "AssignedToUserId", "BlockedReason", "Labels", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    pg_temp.demo_uuid('62000000-0000-0000-0000-', w."WorkNo"),
    pg_temp.demo_uuid('60000000-0000-0000-0000-', w."WorkNo"),
    '30000000-0000-0000-0000-000000000001',
    w."SequenceNumber",
    w."Title",
    w."Description" || ' Current owner should update this work item when the linked intent changes.',
    w."Status",
    w."Priority",
    w."Type",
    CASE WHEN w."WorkNo" % 2 = 0 THEN users.sam_id ELSE users.alex_id END,
    w."BlockedReason",
    w."Labels",
    w."Reason",
    :now_utc - INTERVAL '8 days' + (w."WorkNo" || ' minutes')::interval,
    CASE WHEN w."WorkNo" % 2 = 0 THEN users.sam_id ELSE users.alex_id END,
    'Updated'
FROM demo_work_seed w
CROSS JOIN users
WHERE w."Status" <> 'Todo' OR w."WorkNo" <= 25;

WITH users AS (
    SELECT (SELECT "Id" FROM "Users" WHERE "Email" = 'alex@grundet.test') AS alex_id
), links AS (
    SELECT generate_series(1, 45) AS n
)
INSERT INTO "WorkItemLinks" (
    "Id", "WorkItemLinkId", "WorkItemId", "RequirementId", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    pg_temp.demo_uuid('70000000-0000-0000-0000-', links.n),
    pg_temp.demo_uuid('71000000-0000-0000-0000-', links.n),
    pg_temp.demo_uuid('60000000-0000-0000-0000-', links.n),
    pg_temp.demo_uuid('50000000-0000-0000-0000-', links.n),
    :now_utc - INTERVAL '16 days' + (links.n || ' minutes')::interval,
    users.alex_id,
    'Linked'
FROM links
CROSS JOIN users;

INSERT INTO "TeamUsageSnapshots" (
    "TeamId", "Plan", "ProjectCount", "MemberCount", "RequirementCount", "WorkItemCount",
    "RequirementWorkItemStatus", "OverLimitSince", "EnforcementStartsAt", "CalculatedAt")
VALUES (
    '20000000-0000-0000-0000-000000000001',
    'TeamS',
    1,
    2,
    65,
    65,
    'WithinLimit',
    NULL,
    NULL,
    :now_utc)
ON CONFLICT ("TeamId") DO UPDATE SET
    "Plan" = EXCLUDED."Plan",
    "ProjectCount" = EXCLUDED."ProjectCount",
    "MemberCount" = EXCLUDED."MemberCount",
    "RequirementCount" = EXCLUDED."RequirementCount",
    "WorkItemCount" = EXCLUDED."WorkItemCount",
    "RequirementWorkItemStatus" = EXCLUDED."RequirementWorkItemStatus",
    "OverLimitSince" = EXCLUDED."OverLimitSince",
    "EnforcementStartsAt" = EXCLUDED."EnforcementStartsAt",
    "CalculatedAt" = EXCLUDED."CalculatedAt";

COMMIT;

\echo 'Seeded Northbridge School Board scenario: 65 current intent rows, 65 current work rows, 45 current links.'
