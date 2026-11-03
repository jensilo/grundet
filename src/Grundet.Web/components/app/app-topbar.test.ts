import { describe, expect, it } from "vitest";

import type { ShellProject, ShellTeam } from "@/components/app/app-shell-types";
import { buildCrumbs, buildNewAction, getProjectIdFromPath } from "@/components/app/app-topbar";

const teams: ShellTeam[] = [
  {
    id: "team-1",
    name: "Platform",
    projects: [{ id: "project-1", name: "Core" }],
  },
];

const projects: ShellProject[] = [
  {
    id: "project-1",
    name: "Core",
    teamId: "team-1",
    teamName: "Platform",
  },
  {
    id: "project-2",
    name: "Fallback must not leak",
    teamId: "team-2",
    teamName: "Other team",
  },
];

describe("getProjectIdFromPath", () => {
  it.each(["/dashboard", "/profile", "/projects", "/teams/team-1", "/teams/team-1/projects"])(
    "returns null for %s",
    (pathname) => {
      expect(getProjectIdFromPath(pathname)).toBeNull();
    }
  );

  it("returns the project id only for project routes", () => {
    expect(getProjectIdFromPath("/projects/project-1/requirements")).toBe("project-1");
  });
});

describe("buildCrumbs", () => {
  it("uses the team name on team routes without leaking the first project", () => {
    expect(buildCrumbs("/teams/team-1", teams, projects)).toEqual([
      { id: "teams", label: "Teams", href: "/teams" },
      { id: "team:team-1", label: "Platform", href: "/teams/team-1" },
    ]);
  });

  it("builds linked team projects crumbs", () => {
    expect(buildCrumbs("/teams/team-1/projects", teams, projects)).toEqual([
      { id: "teams", label: "Teams", href: "/teams" },
      { id: "team:team-1", label: "Platform", href: "/teams/team-1" },
      { id: "team-projects", label: "Projects" },
    ]);
  });

  it("builds project search crumbs with the project crumb pointing at the team projects", () => {
    expect(buildCrumbs("/projects/project-1/search", teams, projects)).toEqual([
      { id: "team:team-1", label: "Platform", href: "/teams/team-1" },
      { id: "project:project-1", label: "Core", href: "/teams/team-1/projects" },
      { id: "search", label: "Search", href: "/projects/project-1/search" },
    ]);
  });

  it("uses generic labels for unknown ids without leaking fallback projects", () => {
    expect(buildCrumbs("/teams/missing", teams, projects)).toEqual([
      { id: "teams", label: "Teams", href: "/teams" },
      { id: "team:missing", label: "Team", href: "/teams/missing" },
    ]);

    expect(buildCrumbs("/projects/missing/requirements", teams, projects)).toEqual([
      { id: "projects", label: "Projects", href: "/projects" },
    ]);
  });
});

describe("buildNewAction", () => {
  it("deep-links the intent list view to the compose dialog", () => {
    expect(buildNewAction("/projects/project-1/requirements", "project-1")).toEqual({
      href: "/projects/project-1/requirements?compose=intent",
      label: "New intent",
      showPlus: true,
    });
  });

  it("deep-links the work list view to the compose dialog", () => {
    expect(buildNewAction("/projects/project-1/work-items", "project-1")).toEqual({
      href: "/projects/project-1/work-items?compose=work",
      label: "New work item",
      showPlus: true,
    });
  });

  it("carries the viewed intent from an intent detail page so /new can pre-select its feature", () => {
    expect(buildNewAction("/projects/project-1/requirements/req-1", "project-1").href).toBe(
      "/projects/project-1/requirements/new?fromRequirement=req-1"
    );
  });

  it("carries the viewed intent from deeper intent routes (e.g. history)", () => {
    expect(buildNewAction("/projects/project-1/requirements/req-1/history", "project-1").href).toBe(
      "/projects/project-1/requirements/new?fromRequirement=req-1"
    );
  });

  it("does not carry a fromRequirement from a work item detail page", () => {
    expect(buildNewAction("/projects/project-1/work-items/wi-1", "project-1").href).toBe(
      "/projects/project-1/work-items/new"
    );
  });

  it("does not deep-link the /new page back to itself or carry a fromRequirement", () => {
    expect(buildNewAction("/projects/project-1/requirements/new", "project-1").href).toBe(
      "/projects/project-1/requirements/new"
    );
  });

  it("falls back to browse projects when there is no project context", () => {
    expect(buildNewAction("/dashboard", null)).toEqual({
      href: "/projects",
      label: "Browse projects",
      showPlus: false,
    });
  });
});
