import type { TeamRole } from "@/lib/api/generated/types.gen";

export function TeamRoleBadge({ role }: { role: TeamRole }): React.JSX.Element {
  if (role === "Lead") {
    return (
      <span className="bg-accent text-accent-foreground inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium">
        Lead
      </span>
    );
  }

  return (
    <span className="bg-muted text-muted-foreground inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium">
      Contributor
    </span>
  );
}
