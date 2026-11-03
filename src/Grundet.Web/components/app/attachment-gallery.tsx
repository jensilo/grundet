"use client";

import { useState } from "react";

import { AttachmentImage } from "@/components/app/attachment-image";
import { LightboxImage } from "@/components/app/lightbox-image";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import type { AttachmentResponse } from "@/lib/api/generated/types.gen";

type AttachmentGalleryProps = {
  projectId: string;
  attachments: AttachmentResponse[];
};

/**
 * Owner-agnostic image gallery for project attachments (requirements and work items). The grid loads
 * server-side thumbnails; clicking opens the full image in a lightbox dialog.
 */
export function AttachmentGallery({
  projectId,
  attachments,
}: AttachmentGalleryProps): React.JSX.Element | null {
  const [active, setActive] = useState<AttachmentResponse | null>(null);

  if (attachments.length === 0) {
    return null;
  }

  return (
    <section className="bg-card ring-foreground/10 rounded-xl p-5 ring-1">
      <div className="mb-4 flex items-baseline justify-between gap-3">
        <h2 className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
          Images
        </h2>
        <span className="text-muted-foreground text-xs">{attachments.length}</span>
      </div>

      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {attachments.map((attachment) => (
          <li key={attachment.id}>
            <button
              type="button"
              onClick={() => setActive(attachment)}
              className="border-border focus-visible:ring-ring/50 block w-full overflow-hidden rounded-md border transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:outline-none"
            >
              <AttachmentImage
                projectId={projectId}
                attachmentId={attachment.id}
                alt={attachment.originalFileName ?? "Image"}
                variant="thumb"
                className="aspect-[4/3] w-full object-cover"
              />
            </button>
          </li>
        ))}
      </ul>

      <Dialog open={active !== null} onOpenChange={(open) => !open && setActive(null)}>
        {/* sm:max-w-6xl, not max-w-6xl: DialogContent's default sm:max-w-sm would otherwise win at
            every desktop breakpoint and pin the lightbox to ~384px. */}
        <DialogContent className="max-h-[94vh] overflow-hidden p-2 sm:max-w-6xl">
          <DialogTitle className="sr-only">{active?.originalFileName ?? "Image"}</DialogTitle>
          {active && (
            <LightboxImage
              projectId={projectId}
              attachmentId={active.id}
              alt={active.originalFileName ?? "Image"}
            />
          )}
          <p className="text-muted-foreground mt-1 text-center text-xs">
            Scroll to zoom · drag to pan · double-click to reset
          </p>
        </DialogContent>
      </Dialog>
    </section>
  );
}
