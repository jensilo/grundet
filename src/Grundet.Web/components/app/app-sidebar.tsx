"use client";

import Link from "next/link";

import { AppNavList } from "@/components/app/app-nav-list";
import { type ShellProject, type ShellTeam } from "@/components/app/app-shell-types";
import { AppUserMenu } from "@/components/app/app-user-menu";
import { FeedbackDialog } from "@/components/app/feedback-dialog";
import { ProjectSwitcher } from "@/components/app/project-switcher";
import { useCurrentProject } from "@/components/app/use-current-project";
import { Logo } from "@/components/brand/logo";

export function AppSidebar({
  projects,
  teams,
  user,
}: {
  projects: ShellProject[];
  teams: ShellTeam[];
  user: { id: string; name: string; email: string };
}): React.JSX.Element {
  const currentProject = useCurrentProject(projects);

  return (
    <aside className="bg-background border-border sticky top-0 hidden h-screen w-60 shrink-0 flex-col border-r md:flex">
      <div className="border-border flex h-[52px] items-center border-b px-4">
        <Link href="/dashboard" className="flex items-center" aria-label="Grundet home">
          <Logo size="sm" />
        </Link>
      </div>

      <div className="px-3 pt-3">
        <ProjectSwitcher currentProject={currentProject} teams={teams} />
      </div>

      <AppNavList currentProjectId={currentProject?.id} className="flex-1 px-2 py-3" />

      <div className="border-border space-y-3 border-t p-3">
        <FeedbackDialog />
        <AppUserMenu user={user} />
      </div>
    </aside>
  );
}
