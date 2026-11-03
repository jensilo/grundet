"use client";

import { X } from "lucide-react";
import { useRef, useState } from "react";

import { AttachmentImage } from "@/components/app/attachment-image";
import { Button } from "@/components/ui/button";
import { createAttachments } from "@/lib/api/generated/sdk.gen";
import type { AttachmentResponse } from "@/lib/api/generated/types.gen";
import { mutationErrorMessage } from "@/lib/api/mutation-error";

type AttachmentUploaderProps = {
  projectId: string;
  attachments: AttachmentResponse[];
  disabled?: boolean;
  onChange: (next: AttachmentResponse[]) => void;
};

/**
 * Owner-agnostic image uploader for project attachments (requirements and work items). Files are
 * uploaded immediately to the project and tracked by id; the caller references the returned ids when
 * it saves the owning entity version.
 */
export function AttachmentUploader({
  projectId,
  attachments,
  disabled = false,
  onChange,
}: AttachmentUploaderProps): React.JSX.Element {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleFiles(fileList: FileList | null): Promise<void> {
    const files = fileList ? Array.from(fileList) : [];
    if (files.length === 0) return;

    setUploading(true);
    setError(null);

    const { data, error: uploadError } = await createAttachments({
      path: { projectId },
      body: { files },
    });

    setUploading(false);
    if (inputRef.current) inputRef.current.value = "";

    if (uploadError || !data) {
      setError(mutationErrorMessage(uploadError, "Could not upload the image. Please try again."));
      return;
    }

    onChange([...attachments, ...data]);
  }

  function removeAt(attachmentId: string): void {
    onChange(attachments.filter((attachment) => attachment.id !== attachmentId));
  }

  return (
    <div className="space-y-3">
      {attachments.length > 0 && (
        <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
          {attachments.map((attachment) => (
            <li key={attachment.id} className="group relative">
              <AttachmentImage
                projectId={projectId}
                attachmentId={attachment.id}
                alt={attachment.originalFileName ?? "Image"}
                variant="thumb"
                className="border-border aspect-square w-full rounded-md border object-cover"
              />
              <button
                type="button"
                aria-label="Remove image"
                disabled={disabled || uploading}
                onClick={() => removeAt(attachment.id)}
                className="bg-background/90 text-muted-foreground hover:text-foreground absolute top-1 right-1 inline-flex size-6 items-center justify-center rounded-full border shadow-sm transition-colors disabled:opacity-50"
              >
                <X className="size-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        multiple
        className="hidden"
        onChange={(event) => void handleFiles(event.target.files)}
      />
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled || uploading}
        onClick={() => inputRef.current?.click()}
      >
        {uploading ? "Uploading…" : "Add images"}
      </Button>

      {error && <p className="text-destructive text-xs">{error}</p>}
    </div>
  );
}
