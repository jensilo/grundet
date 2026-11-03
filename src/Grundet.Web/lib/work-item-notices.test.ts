import { describe, expect, it } from "vitest";

import type { WorkItemNoticeResponse } from "@/lib/api/generated/types.gen";
import { affectedWorkMessage } from "@/lib/work-item-notices";

function notice(title: string): WorkItemNoticeResponse {
  return { workItemId: crypto.randomUUID(), title, status: "Todo" };
}

describe("affectedWorkMessage", () => {
  it("returns null when no work is linked", () => {
    expect(affectedWorkMessage(undefined)).toBeNull();
    expect(affectedWorkMessage([])).toBeNull();
  });

  it("names the single affected work item", () => {
    const message = affectedWorkMessage([notice("Add OAuth flow")]);
    expect(message).toEqual({
      title: "Intent saved",
      description: "1 linked work item may be affected – Add OAuth flow.",
    });
  });

  it("summarises by count when several are affected", () => {
    const message = affectedWorkMessage([notice("A"), notice("B"), notice("C")]);
    expect(message?.description).toBe("3 linked work items may be affected.");
  });
});
