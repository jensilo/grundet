import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import type {
  RequirementWorkItemLimitStatus,
  TeamPlan,
  TeamUsageSummary,
} from "@/lib/api/generated";

function formatLimit(value: null | number | string): string {
  if (value === null) {
    return "Unlimited";
  }

  return Number(value).toLocaleString("en-US");
}

function formatUsage(value: number | string): string {
  return Number(value).toLocaleString("en-US");
}

function formatDate(value: null | string): string {
  if (!value) {
    return "Not calculated yet";
  }

  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC",
  }).format(new Date(value));
}

function getStatusLabel(status: RequirementWorkItemLimitStatus): string {
  switch (status) {
    case "WithinLimit":
      return "Within limit";
    case "ApproachingLimit":
      return "Approaching limit";
    case "OverLimitGrace":
      return "Over-limit grace";
    case "EnforcementActive":
      return "Enforcement active";
  }
}

function getPlanLabel(plan: TeamPlan): string {
  switch (plan) {
    case "Solo":
      return "Solo";
    case "Power":
      return "Power";
    case "Expert":
      return "Expert";
    case "TeamS":
      return "Team S";
    case "TeamM":
      return "Team M";
    case "TeamL":
      return "Team L";
    case "TeamAlpha":
    case "TeamUnlimited":
      return "Team";
  }
}

function getStatusVariant(
  status: RequirementWorkItemLimitStatus
): React.ComponentProps<typeof Badge>["variant"] {
  switch (status) {
    case "WithinLimit":
      return "active";
    case "ApproachingLimit":
    case "OverLimitGrace":
      return "wontDo";
    case "EnforcementActive":
      return "destructive";
  }
}

function LimitRow({ label, usage, limit }: { label: string; usage: string; limit: string }) {
  return (
    <div className="border-border flex items-center justify-between gap-4 border-b py-3 last:border-b-0">
      <dt className="text-foreground text-sm font-medium">{label}</dt>
      <dd className="text-muted-foreground text-sm">
        <span className="text-foreground font-mono">{usage}</span> / {limit}
      </dd>
    </div>
  );
}

export function UsageLimitsPanel({ limits }: { limits: TeamUsageSummary }) {
  const usage = limits.usage;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Usage and limits</CardTitle>
        <CardDescription>
          Plan limits are informational here. {limits.calculationCadence}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="flex flex-wrap items-center gap-3">
          <Badge variant="outline">Plan: {getPlanLabel(limits.plan)}</Badge>
          <Badge variant={getStatusVariant(limits.requirementWorkItemStatus)}>
            {getStatusLabel(limits.requirementWorkItemStatus)}
          </Badge>
        </div>

        <dl>
          <LimitRow
            label="Projects"
            usage={usage ? formatUsage(usage.projects) : "Not calculated"}
            limit={formatLimit(limits.limits.maxProjects)}
          />
          <LimitRow
            label="Members"
            usage={usage ? formatUsage(usage.members) : "Not calculated"}
            limit={formatLimit(limits.limits.maxMembers)}
          />
          <LimitRow
            label="Intent and work"
            usage={usage ? formatUsage(usage.totalRequirementsAndWorkItems ?? 0) : "Not calculated"}
            limit={formatLimit(limits.limits.maxTotalRequirementsAndWorkItems)}
          />
          <LimitRow label="AI assistance" usage={limits.limits.aiUsageLabel} limit="Included" />
        </dl>

        <div className="text-muted-foreground space-y-2 text-sm">
          {limits.calculatedAt ? <p>Calculated at: {formatDate(limits.calculatedAt)}.</p> : null}
          {limits.overLimitSince ? (
            <p>Over limit since: {formatDate(limits.overLimitSince)}.</p>
          ) : null}
          {limits.enforcementStartsAt ? (
            <p>Enforcement starts at: {formatDate(limits.enforcementStartsAt)}.</p>
          ) : null}
          <p>{limits.recommendedAction}</p>
        </div>
      </CardContent>
    </Card>
  );
}
