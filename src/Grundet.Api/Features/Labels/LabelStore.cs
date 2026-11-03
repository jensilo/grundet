using Grundet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Grundet.Api.Features.Labels;

public sealed class LabelStore(AppDbContext dbContext) : ILabelStore
{
    // Aggregates distinct labels across the project's current requirements and work items in a
    // single round-trip. Labels are trimmed and case-folded for grouping; counted once per entity
    // (a label repeated on one entity counts once); the displayed value is the most common original
    // casing. Filters by the authorized projectId — never relies on RLS alone.
    private const string LabelsSql = """
        WITH raw AS (
            SELECT r."RequirementId" AS entity_id, btrim(label) AS original
            FROM "CurrentRequirements" r
            CROSS JOIN LATERAL unnest(r."Labels") AS label
            WHERE r."ProjectId" = @projectId AND btrim(label) <> ''
            UNION ALL
            SELECT w."WorkItemId" AS entity_id, btrim(label) AS original
            FROM "CurrentWorkItems" w
            CROSS JOIN LATERAL unnest(w."Labels") AS label
            WHERE w."ProjectId" = @projectId AND btrim(label) <> ''
        ),
        per_entity AS (
            SELECT DISTINCT entity_id, lower(original) AS norm, original
            FROM raw
        ),
        counts AS (
            SELECT norm, count(DISTINCT entity_id)::int AS cnt
            FROM per_entity
            GROUP BY norm
        ),
        casing AS (
            SELECT
                norm,
                original,
                ROW_NUMBER() OVER (PARTITION BY norm ORDER BY count(*) DESC, original ASC) AS rn
            FROM per_entity
            GROUP BY norm, original
        )
        SELECT casing.original AS "Value", counts.cnt AS "Count"
        FROM counts
        JOIN casing ON casing.norm = counts.norm AND casing.rn = 1
        ORDER BY counts.cnt DESC, casing.original ASC
        """;

    public async Task<IReadOnlyList<LabelSuggestion>> GetProjectLabelsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Database.SqlQueryRaw<LabelSuggestion>(
                LabelsSql,
                new NpgsqlParameter<Guid>("projectId", projectId))
            .ToListAsync(cancellationToken);

        return rows;
    }
}
