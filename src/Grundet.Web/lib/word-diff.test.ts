import { describe, expect, it } from "vitest";

import { diffRetention, wordDiff } from "./word-diff";

describe("wordDiff", () => {
  it("returns all-unchanged for identical strings", () => {
    const result = wordDiff("hello world", "hello world");
    expect(result.every((t) => t.type === "unchanged")).toBe(true);
  });

  it("marks added words", () => {
    const result = wordDiff("hello", "hello world");
    expect(result).toContainEqual({ text: "world", type: "added" });
  });

  it("marks removed words", () => {
    const result = wordDiff("hello world", "hello");
    expect(result).toContainEqual({ text: "world", type: "removed" });
  });

  it("returns empty array for two empty strings", () => {
    expect(wordDiff("", "")).toEqual([]);
  });

  it("treats entire string as removed when after is empty", () => {
    expect(wordDiff("foo", "")).toEqual([{ text: "foo", type: "removed" }]);
  });

  it("treats entire string as added when before is empty", () => {
    expect(wordDiff("", "foo")).toEqual([{ text: "foo", type: "added" }]);
  });

  it("reconstructing after-string from non-removed tokens yields after", () => {
    const before = "the quick brown fox";
    const after = "the slow brown fox";
    const result = wordDiff(before, after);
    const reconstructed = result
      .filter((t) => t.type !== "removed")
      .map((t) => t.text)
      .join("");
    expect(reconstructed).toBe(after);
  });

  it("reconstructing before-string from non-added tokens yields before", () => {
    const before = "foo bar baz";
    const after = "foo qux baz";
    const result = wordDiff(before, after);
    const reconstructed = result
      .filter((t) => t.type !== "added")
      .map((t) => t.text)
      .join("");
    expect(reconstructed).toBe(before);
  });
});

describe("diffRetention", () => {
  it("returns 1 for identical strings", () => {
    expect(diffRetention(wordDiff("hello world", "hello world"))).toBe(1);
  });

  it("returns 1 for two empty strings", () => {
    expect(diffRetention(wordDiff("", ""))).toBe(1);
  });

  it("ignores whitespace when measuring retention", () => {
    expect(diffRetention(wordDiff("hello   world", "hello world"))).toBe(1);
  });

  it("stays high for a small edit", () => {
    expect(diffRetention(wordDiff("the quick brown fox", "the slow brown fox"))).toBeGreaterThan(
      0.5
    );
  });

  it("drops low for a near-total rewrite", () => {
    const before = "This is absolutely not necessary, always!";
    const after = "We do not need it in pilot.";
    expect(diffRetention(wordDiff(before, after))).toBeLessThan(0.5);
  });
});
