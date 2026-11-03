import { describe, expect, it } from "vitest";

import { buildLabelFilterHref } from "@/components/app/browse-links";

describe("buildLabelFilterHref", () => {
  it("builds a fresh single-label filter href", () => {
    expect(buildLabelFilterHref("/projects/p1/requirements", "backend")).toBe(
      "/projects/p1/requirements?labels=backend"
    );
  });

  it("encodes spaces", () => {
    expect(buildLabelFilterHref("/projects/p1/work-items", "needs review")).toBe(
      "/projects/p1/work-items?labels=needs+review"
    );
  });

  it("encodes reserved characters (#, &, /)", () => {
    expect(buildLabelFilterHref("/projects/p1/requirements", "a#b&c/d")).toBe(
      "/projects/p1/requirements?labels=a%23b%26c%2Fd"
    );
  });
});
