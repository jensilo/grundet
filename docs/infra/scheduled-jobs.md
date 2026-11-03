# Scheduled jobs

| Job | Default cadence | Purpose | Multi-instance safety |
|---|---:|---|---|
| Team usage recalculation | Daily at 03:30 UTC | Refresh cached team quota usage for Requirement/Work Item enforcement and limits display. | PostgreSQL advisory lock. |

## Team usage recalculation

The API hosts the scheduler. By default it is enabled in production config and runs daily at `03:30 UTC`.

Behavior:
- The scheduled daily run recalculates all teams, including teams without an existing usage snapshot.
- On startup, the service waits `BillingUsageRecalculation:StartupDelaySeconds` seconds (`300` by default).
- Startup catch-up then acquires the PostgreSQL advisory lock and checks existing snapshots for staleness inside the lock.
- Snapshots are stale when `CalculatedAt` is older than `BillingUsageRecalculation:StaleAfterHours` (`20` by default).
- Teams without snapshots are ignored by the startup freshness check. They are covered by the next scheduled full run.
- If at least one existing snapshot is stale, startup recalculates all teams.
- Recalculation is per team; one failed team is logged and does not stop remaining teams.

Operational notes:
- Multiple API instances may run the hosted service. The advisory lock ensures only one active recalculation.
- Repeated deploys should not repeatedly recalculate because startup catch-up uses the staleness threshold.
- If the database connection/session holding the lock is lost, PostgreSQL releases the lock and another instance can run later.
- Automated integration tests disable the hosted service by config.
- Local development disables the scheduler by default in `appsettings.Development.json`; enable it explicitly if needed.
- No external queue or cron is required for the 2–3 instance deployment.
