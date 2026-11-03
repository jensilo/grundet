import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import { LightboxImage } from "@/components/app/lightbox-image";

// AttachmentImage streams via the generated client; stub it so the viewer mounts without a request.
vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getAttachmentContent: vi.fn().mockResolvedValue({ data: undefined, error: { status: 404 } }),
}));

describe("LightboxImage", () => {
  function transformOf(container: HTMLElement): string {
    const inner = container.querySelector('div[style*="transform"]') as HTMLElement | null;
    return inner?.style.transform ?? "";
  }

  it("toggles zoom on double-click and resets on a second double-click", async () => {
    const user = userEvent.setup();
    const { container } = render(<LightboxImage projectId="p1" attachmentId="a1" alt="Chart" />);
    const viewport = container.firstElementChild as HTMLElement;

    expect(transformOf(container)).toContain("scale(1)");

    await user.dblClick(viewport);
    expect(transformOf(container)).toContain("scale(2.5)");

    await user.dblClick(viewport);
    expect(transformOf(container)).toContain("scale(1)");
  });
});
