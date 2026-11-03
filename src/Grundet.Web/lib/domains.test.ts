import { describe, expect, it } from "vitest";

import { buildAppUrl, buildMarketingUrl } from "./domains";

describe("buildAppUrl", () => {
  it("maps grundet.test with port to http app subdomain", () => {
    expect(buildAppUrl("grundet.test:3000", "/dashboard")).toBe(
      "http://my.grundet.test:3000/dashboard"
    );
  });

  it("maps grundet.app to https app subdomain", () => {
    expect(buildAppUrl("grundet.app", "/dashboard")).toBe("https://my.grundet.app/dashboard");
  });

  it("strips www from grundet.app", () => {
    expect(buildAppUrl("www.grundet.app", "/dashboard")).toBe("https://my.grundet.app/dashboard");
  });

  it("preserves arbitrary paths", () => {
    expect(buildAppUrl("grundet.app", "/teams/abc-123")).toBe(
      "https://my.grundet.app/teams/abc-123"
    );
  });
});

describe("buildMarketingUrl", () => {
  it("maps my.grundet.test to local marketing domain", () => {
    expect(buildMarketingUrl("my.grundet.test:3000", "/join-alpha")).toBe(
      "http://grundet.test:3000/join-alpha"
    );
  });

  it("maps my.grundet.app to production marketing domain", () => {
    expect(buildMarketingUrl("my.grundet.app", "/join-alpha")).toBe(
      "https://grundet.app/join-alpha"
    );
  });
});
