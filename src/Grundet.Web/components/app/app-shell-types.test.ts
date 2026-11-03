import { describe, expect, it } from "vitest";

import { resolveRouteProject, type ShellProject } from "@/components/app/app-shell-types";

const projects: ShellProject[] = [
  { id: "project-1", name: "Core", teamId: "team-1", teamName: "Platform" },
  { id: "project-2", name: "Edge", teamId: "team-1", teamName: "Platform" },
];

describe("resolveRouteProject", () => {
  it("returns the project named by a project-scoped route", () => {
    expect(resolveRouteProject("/projects/project-2/requirements", projects)?.id).toBe("project-2");
  });

  it.each(["/dashboard", "/profile", "/projects", "/teams/team-1"])(
    "returns null off a project route (%s) — no first-project fallback",
    (pathname) => {
      expect(resolveRouteProject(pathname, projects)).toBeNull();
    }
  );

  it("returns null when the route names a project the user cannot see", () => {
    expect(resolveRouteProject("/projects/unknown/requirements", projects)).toBeNull();
  });
});
