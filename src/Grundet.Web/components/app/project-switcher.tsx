"use client";

import { Check, ChevronsUpDown } from "lucide-react";
import { useRouter } from "next/navigation";

import type { ShellProject, ShellTeam } from "@/components/app/app-shell-types";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

export function ProjectSwitcher({
  currentProject,
  teams,
}: {
  currentProject: ShellProject | null;
  teams: ShellTeam[];
}): React.JSX.Element {
  const router = useRouter();

  if (!currentProject) {
    return (
      <DropdownMenu>
        <DropdownMenuTrigger className="group/trigger border-border bg-background focus-visible:ring-ring/30 hover:bg-muted w-full rounded-md border px-2.5 py-2 text-left transition-colors focus-visible:ring-2 focus-visible:outline-none">
          <div className="flex items-center justify-between gap-2">
            <div className="min-w-0">
              <div className="text-foreground truncate text-sm font-medium">
                No project selected
              </div>
              <div className="text-muted-foreground truncate text-xs">Open a project</div>
            </div>
            <ChevronsUpDown className="text-muted-foreground size-3.5 shrink-0" />
          </div>
        </DropdownMenuTrigger>
        <DropdownMenuContent className="w-[240px]" sideOffset={6}>
          <DropdownMenuItem onClick={() => router.push("/projects")}>
            Browse projects
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="group/trigger border-border bg-background focus-visible:ring-ring/30 hover:bg-muted w-full rounded-md border px-2.5 py-2 text-left transition-colors focus-visible:ring-2 focus-visible:outline-none">
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0">
            <div className="text-foreground truncate text-sm font-medium">
              {currentProject.name}
            </div>
            <div className="text-muted-foreground truncate text-xs">{currentProject.teamName}</div>
          </div>
          <ChevronsUpDown className="text-muted-foreground size-3.5 shrink-0" />
        </div>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-[240px]" sideOffset={6}>
        {teams.map((team, teamIndex) => (
          <DropdownMenuGroup key={team.id}>
            <DropdownMenuLabel>{team.name}</DropdownMenuLabel>
            {team.projects.map((project) => (
              <DropdownMenuItem
                key={project.id}
                className="flex items-center gap-2"
                onClick={() => router.push(`/projects/${project.id}/requirements`)}
              >
                <span className="flex-1 truncate">{project.name}</span>
                {project.id === currentProject.id && <Check className="text-primary size-3.5" />}
              </DropdownMenuItem>
            ))}
            {teamIndex < teams.length - 1 && <DropdownMenuSeparator />}
          </DropdownMenuGroup>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => router.push("/projects")}>
          Browse projects
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
