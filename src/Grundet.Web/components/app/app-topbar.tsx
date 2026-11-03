"use client";

import { Plus, Search } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import type { ShellProject, ShellTeam } from "@/components/app/app-shell-types";
import { useCommandPalette } from "@/components/app/command-palette";
import { MobileNavDrawer } from "@/components/app/mobile-nav-drawer";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export type Crumb = { id: string; label: string; href?: string };
type TopbarAction = { href: string; label: string; showPlus: boolean };

function getSegments(pathname: string): string[] {
  return pathname.split("/").filter(Boolean);
}

export function getProjectIdFromPath(pathname: string): string | null {
  const segments = getSegments(pathname);
  return segments[0] === "projects" && segments[1] ? segments[1] : null;
}

export function buildCrumbs(
  pathname: string,
  teams: ShellTeam[],
  projects: ShellProject[]
): Crumb[] {
  const segments = getSegments(pathname);
  const [root, id, section] = segments;

  if (root === "dashboard" || segments.length === 0) return [{ id: "home", label: "Home" }];
  if (root === "profile") return [{ id: "profile", label: "Profile" }];

  if (root === "teams") {
    if (!id) return [{ id: "teams", label: "Teams" }];

    const team = teams.find((candidate) => candidate.id === id);
    const crumbs: Crumb[] = [
      { id: "teams", label: "Teams", href: "/teams" },
      { id: `team:${id}`, label: team?.name ?? "Team", href: `/teams/${id}` },
    ];

    if (section === "projects") {
      crumbs.push({ id: "team-projects", label: "Projects" });
    }

    return crumbs;
  }

  if (root === "projects") {
    if (!id) return [{ id: "projects", label: "Projects" }];

    const project = projects.find((candidate) => candidate.id === id);
    if (!project) return [{ id: "projects", label: "Projects", href: "/projects" }];

    const crumbs: Crumb[] = [
      { id: `team:${project.teamId}`, label: project.teamName, href: `/teams/${project.teamId}` },
      {
        id: `project:${project.id}`,
        label: project.name,
        href: `/teams/${project.teamId}/projects`,
      },
    ];

    if (section === "features") {
      crumbs.push({ id: "features", label: "Features", href: `/projects/${project.id}/features` });
    } else if (section === "requirements") {
      crumbs.push({
        id: "requirements",
        label: "Intent",
        href: `/projects/${project.id}/requirements`,
      });
    } else if (section === "work-items") {
      crumbs.push({ id: "work-items", label: "Work", href: `/projects/${project.id}/work-items` });
    } else if (section === "search") {
      crumbs.push({ id: "search", label: "Search", href: `/projects/${project.id}/search` });
    } else if (section === "changelog") {
      crumbs.push({
        id: "changelog",
        label: "Changelog",
        href: `/projects/${project.id}/changelog`,
      });
    }

    return crumbs;
  }

  return [{ id: "app", label: "App" }];
}

export function buildNewAction(
  pathname: string,
  projectId: string | null,
  teamId: string | null = null
): TopbarAction {
  if (!projectId) {
    return { href: "/projects", label: "Browse projects", showPlus: false };
  }

  const segments = getSegments(pathname);
  const section = segments[2];
  // List views (`/projects/{id}/requirements`) have no fourth segment — a detail page (`/{id}`) or
  // the `/new` page do. On a list view the create action deep-links to the dialog via `?compose=`;
  // elsewhere there is no list to host the dialog, so it navigates to the `/new` page.
  const isListView = segments[3] === undefined;

  if (section === "work-items") {
    return {
      href: isListView
        ? `/projects/${projectId}/work-items?compose=work`
        : `/projects/${projectId}/work-items/new`,
      label: "New work item",
      showPlus: true,
    };
  }

  if (section === "requirements") {
    if (isListView) {
      return {
        href: `/projects/${projectId}/requirements?compose=intent`,
        label: "New intent",
        showPlus: true,
      };
    }

    // On an intent detail route (`/{reqId}`, possibly with deeper segments like `/history`),
    // carry the viewed intent so the `/new` page can pre-select its feature. Segment 3 is the
    // reqId; guard against the `/new` page itself ("new") and the list view (undefined).
    const detailReqId = segments[3] && segments[3] !== "new" ? segments[3] : null;
    return {
      href: detailReqId
        ? `/projects/${projectId}/requirements/new?fromRequirement=${detailReqId}`
        : `/projects/${projectId}/requirements/new`,
      label: "New intent",
      showPlus: true,
    };
  }

  if (section === "features") {
    // The Features list has no top-level "new" action of its own, so offer a step back up
    // to the team's projects rather than a button that links to the page you are already on.
    return teamId
      ? { href: `/teams/${teamId}/projects`, label: "Manage projects", showPlus: false }
      : { href: "/projects", label: "Browse projects", showPlus: false };
  }

  return { href: `/projects/${projectId}/requirements`, label: "Open intent", showPlus: false };
}

export function AppTopbar({
  teams,
  projects,
}: {
  teams: ShellTeam[];
  projects: ShellProject[];
}): React.JSX.Element {
  const pathname = usePathname();
  const { open: openCommandPalette } = useCommandPalette();

  const projectId = getProjectIdFromPath(pathname);
  const currentProject = projectId
    ? (projects.find((candidate) => candidate.id === projectId) ?? null)
    : null;
  const crumbs = buildCrumbs(pathname, teams, projects);
  const newAction = buildNewAction(pathname, projectId, currentProject?.teamId ?? null);

  return (
    <header className="border-border sticky top-0 z-30 flex h-[52px] items-center justify-between gap-4 border-b bg-white px-6">
      <div className="flex min-w-0 flex-1 items-center gap-2">
        <MobileNavDrawer projects={projects} teams={teams} />
        <nav aria-label="Breadcrumb" className="min-w-0 flex-1">
          <ol className="text-muted-foreground flex items-center gap-1.5 text-sm">
            {crumbs.map((crumb, i) => {
              const isCurrent = i === crumbs.length - 1;

              return (
                <li key={crumb.id} className="flex items-center gap-1.5">
                  {i > 0 && <span className="text-muted-foreground/40">/</span>}
                  {crumb.href && !isCurrent ? (
                    <Link
                      href={crumb.href}
                      className="hover:text-foreground text-muted-foreground transition-colors"
                    >
                      {crumb.label}
                    </Link>
                  ) : (
                    <span
                      aria-current={isCurrent ? "page" : undefined}
                      className={isCurrent ? "text-foreground font-medium" : undefined}
                    >
                      {crumb.label}
                    </span>
                  )}
                </li>
              );
            })}
          </ol>
        </nav>
      </div>

      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={openCommandPalette}
          aria-label="Search workspace"
          className={cn(
            "border-input text-muted-foreground hover:bg-muted/60 relative hidden h-8 w-64 items-center rounded-lg border bg-transparent pr-12 pl-8 text-left text-sm transition-colors md:flex"
          )}
        >
          <Search
            aria-hidden={true}
            className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2"
          />
          <span className="truncate">Search</span>
          <kbd className="border-border bg-muted text-muted-foreground pointer-events-none absolute top-1/2 right-2 -translate-y-1/2 rounded border px-1.5 py-0.5 font-mono text-[10px]">
            ⌘K
          </kbd>
        </button>
        <Button
          size="sm"
          className="gap-1"
          nativeButton={false}
          render={
            <Link href={newAction.href}>
              {newAction.showPlus && <Plus className="size-3.5" aria-hidden={true} />}
              {newAction.label}
            </Link>
          }
        />
      </div>
    </header>
  );
}
