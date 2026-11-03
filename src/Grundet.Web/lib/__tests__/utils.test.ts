import { describe, expect, it } from "vitest";

import { cn } from "../utils";

describe("cn", () => {
  it("returns empty string for no args", () => {
    expect(cn()).toBe("");
  });

  it("merges simple class strings", () => {
    expect(cn("foo", "bar")).toBe("foo bar");
  });

  it("handles conditional classes (falsy values)", () => {
    expect(cn("foo", false && "bar", "baz")).toBe("foo baz");
  });

  it("resolves tailwind conflicts (last wins)", () => {
    // Both px-2 and px-4 set padding-x; tailwind-merge should keep px-4
    expect(cn("px-2", "px-4")).toBe("px-4");
  });

  it("handles undefined, null, and empty string values", () => {
    expect(cn(undefined, null, "", "foo")).toBe("foo");
  });

  it("merges Tailwind classes with conflict resolution across categories", () => {
    expect(cn("text-lg", "text-sm")).toBe("text-sm");
    expect(cn("bg-red-500", "bg-blue-500")).toBe("bg-blue-500");
    expect(cn("p-4", "p-2")).toBe("p-2");
  });
});
