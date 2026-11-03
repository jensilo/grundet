"use client";

import { useEffect, useState } from "react";

import { getAttachmentContent } from "@/lib/api/generated/sdk.gen";
import { cn } from "@/lib/utils";

type AttachmentImageProps = {
  projectId: string;
  attachmentId: string;
  alt: string;
  className?: string;
  /** Forwarded to the underlying <img>; set false to suppress native drag (e.g. in the lightbox). */
  draggable?: boolean;
  /** "thumb" loads the small server-side thumbnail (gallery grids); "full" the re-encoded original. */
  variant?: "full" | "thumb";
};

/**
 * Renders a project image attachment by streaming its bytes through the generated client
 * (cookie-authenticated, cross-origin safe) into an object URL. Plain <img> is deliberate: the
 * source is a runtime blob URL that Next's image optimizer cannot process.
 */
export function AttachmentImage({
  projectId,
  attachmentId,
  alt,
  className,
  draggable,
  variant = "full",
}: AttachmentImageProps): React.JSX.Element {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    let created: string | null = null;

    void (async () => {
      const { data, error } = await getAttachmentContent({
        path: { projectId, attachmentId },
        query: { variant },
        parseAs: "blob",
      });

      if (!active) return;
      if (error || !data) {
        setFailed(true);
        return;
      }

      created = URL.createObjectURL(data);
      setObjectUrl(created);
    })();

    return () => {
      active = false;
      if (created) URL.revokeObjectURL(created);
    };
  }, [projectId, attachmentId, variant]);

  if (failed) {
    return (
      <div
        className={cn(
          "bg-muted text-muted-foreground flex items-center justify-center text-xs",
          className
        )}
      >
        Unavailable
      </div>
    );
  }

  if (!objectUrl) {
    return <div className={cn("bg-muted animate-pulse", className)} aria-hidden />;
  }

  // eslint-disable-next-line @next/next/no-img-element -- blob object URL, not optimizable by next/image
  return <img src={objectUrl} alt={alt} className={className} draggable={draggable} />;
}
