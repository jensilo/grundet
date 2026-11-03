import { describe, expect, it } from "vitest";

import {
  hasReference,
  parseReferenceTokens,
  referenceKindLabel,
  referenceResolverHref,
  referenceTargetHref,
} from "./references";

// Project tokens to a compact, position-independent shape for assertions.
const summarize = (text: string): string[] =>
  parseReferenceTokens(text).map((token) =>
    token.kind === "text" ? `text:${token.value}` : `ref:${token.label}:${token.query}`
  );

describe("parseReferenceTokens", () => {
  it("returns a single text token when there are no references", () => {
    expect(summarize("just some plain text")).toEqual(["text:just some plain text"]);
  });

  it("splits IN-, WO-, and # references out of surrounding text", () => {
    expect(summarize("See IN-12 and WO-3, plus #5.")).toEqual([
      "text:See ",
      "ref:IN-12:IN-12",
      "text: and ",
      "ref:WO-3:WO-3",
      "text:, plus ",
      "ref:#5:5",
      "text:.",
    ]);
  });

  it("requires the uppercase prefix and ignores lowercase look-alikes", () => {
    expect(summarize("blocks in-7 and wo-9")).toEqual(["text:blocks in-7 and wo-9"]);
  });

  it("does not match references glued to letters or digits", () => {
    expect(summarize("xWO-3")).toEqual(["text:xWO-3"]);
    expect(summarize("IN-12abc")).toEqual(["text:IN-12abc"]);
  });

  it("does not turn hyphenated words into references", () => {
    expect(summarize("sign-in-2")).toEqual(["text:sign-in-2"]);
    expect(summarize("check-in-12")).toEqual(["text:check-in-12"]);
  });

  it("still matches refs after non-hyphen punctuation", () => {
    expect(summarize("(IN-12)")).toEqual(["text:(", "ref:IN-12:IN-12", "text:)"]);
    expect(summarize("ref: WO-4")).toEqual(["text:ref: ", "ref:WO-4:WO-4"]);
  });

  it("carries the source offset as a stable key", () => {
    const tokens = parseReferenceTokens("See IN-12 now");
    expect(tokens.map((token) => token.start)).toEqual([0, 4, 9]);
  });

  it("returns an empty array for empty text", () => {
    expect(parseReferenceTokens("")).toEqual([]);
  });
});

describe("hasReference", () => {
  it("detects references and ignores plain text", () => {
    expect(hasReference("fixes WO-4")).toBe(true);
    expect(hasReference("nothing here")).toBe(false);
    expect(hasReference(null)).toBe(false);
    expect(hasReference(undefined)).toBe(false);
  });

  it("is stable across repeated calls (no lastIndex leakage)", () => {
    expect(hasReference("IN-1")).toBe(true);
    expect(hasReference("IN-1")).toBe(true);
  });
});

describe("href helpers", () => {
  it("builds the resolver href with an encoded reference", () => {
    expect(referenceResolverHref("p1", "IN-12")).toBe("/projects/p1/ref/IN-12");
    expect(referenceResolverHref("p1", "5")).toBe("/projects/p1/ref/5");
  });

  it("builds target hrefs per kind", () => {
    expect(referenceTargetHref("p1", "requirement", "r-1")).toBe("/projects/p1/requirements/r-1");
    expect(referenceTargetHref("p1", "workItem", "w-1")).toBe("/projects/p1/work-items/w-1");
  });

  it("labels kinds with product framing", () => {
    expect(referenceKindLabel("requirement")).toBe("Intent");
    expect(referenceKindLabel("workItem")).toBe("Work");
  });
});
