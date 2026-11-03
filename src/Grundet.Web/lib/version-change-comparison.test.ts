import { describe, expect, it } from "vitest";

import {
  acceptanceCriteriaEqual,
  idsEqualIgnoringOrder,
  labelsEqualIgnoringOrder,
  trimmedEqual,
} from "./version-change-comparison";

describe("trimmedEqual", () => {
  it("ignores surrounding whitespace", () => {
    expect(trimmedEqual("  hello ", "hello")).toBe(true);
    expect(trimmedEqual("hello", "world")).toBe(false);
  });
});

describe("labelsEqualIgnoringOrder", () => {
  it("compares case-insensitively and order-independently", () => {
    expect(labelsEqualIgnoringOrder(["Bug", "ux"], ["ux", "bug"])).toBe(true);
  });

  it("detects added or removed labels", () => {
    expect(labelsEqualIgnoringOrder(["bug"], ["bug", "ux"])).toBe(false);
  });
});

describe("idsEqualIgnoringOrder", () => {
  it("ignores order", () => {
    expect(idsEqualIgnoringOrder(["a", "b"], ["b", "a"])).toBe(true);
  });

  it("detects a differing set", () => {
    expect(idsEqualIgnoringOrder(["a"], ["a", "b"])).toBe(false);
  });
});

describe("acceptanceCriteriaEqual", () => {
  it("is position-sensitive so a reorder counts as a change", () => {
    expect(acceptanceCriteriaEqual(["one", "two"], ["two", "one"])).toBe(false);
  });

  it("ignores per-row whitespace", () => {
    expect(acceptanceCriteriaEqual([" one ", "two"], ["one", "two"])).toBe(true);
  });

  it("collapses internal whitespace like the backend normalizer", () => {
    expect(acceptanceCriteriaEqual(["Given  data"], ["Given data"])).toBe(true);
  });
});
