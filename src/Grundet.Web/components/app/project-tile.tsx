import { ArrowRight, FolderOpen } from "lucide-react";
import Link from "next/link";

import type { ProjectResponse } from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

// The single project-card representation. Every surface that lists projects (the Projects
// overview and the team projects page) renders this tile; surfaces differ only in the wrapper
// layout and the optional management `actions` slot.
export function ProjectTile({
  project,
  actions,
  className,
}: {
  project: ProjectResponse;
  actions?: React.ReactNode;
  className?: string;
}): React.JSX.Element {
  const requirementsHref = `/projects/${project.id}/requirements`;
  const description = project.description?.trim();

  return (
    <div
      className={cn(
        "border-border bg-background hover:bg-muted/50 rounded-lg border p-4 motion-safe:transition-colors",
        className
      )}
    >
      <div className="flex min-w-0 items-start gap-2">
        <FolderOpen className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden={true} />
        <div className="min-w-0 flex-1 space-y-1">
          <h3 className="min-w-0 text-sm font-medium">
            <Link
              href={requirementsHref}
              className="hover:text-primary block truncate motion-safe:transition-colors"
            >
              {project.name}
            </Link>
          </h3>
          <p className="text-muted-foreground line-clamp-2 text-sm">
            {description && description.length > 0 ? description : "No description yet."}
          </p>
        </div>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-x-4 gap-y-2">
        <Link
          href={requirementsHref}
          className="text-primary hover:text-primary-hover inline-flex items-center gap-1 text-sm font-medium motion-safe:transition-colors"
        >
          Open intent
          <ArrowRight className="size-3.5" aria-hidden={true} />
        </Link>
        <Link
          href={`/projects/${project.id}/features`}
          className="text-muted-foreground hover:text-foreground text-sm motion-safe:transition-colors"
        >
          Features
        </Link>
        <Link
          href={`/projects/${project.id}/work-items`}
          className="text-muted-foreground hover:text-foreground text-sm motion-safe:transition-colors"
        >
          Work
        </Link>
        {actions ? <div className="ml-auto flex items-center gap-2">{actions}</div> : null}
      </div>
    </div>
  );
}
