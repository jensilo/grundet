"use client";

import { MessageSquare, Trash2 } from "lucide-react";
import { usePathname, useSearchParams } from "next/navigation";
import { useMemo, useRef, useState, useTransition } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { createFeedback } from "@/lib/api/generated/sdk.gen";
import type { FeedbackKind } from "@/lib/api/generated/types.gen";
import { TEXT_LIMITS } from "@/lib/text-limits";

const MaxScreenshots = 5;
const MaxScreenshotBytes = 2 * 1024 * 1024;

function formatBytes(bytes: number): string {
  if (bytes >= 1024 * 1024) {
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  return `${Math.ceil(bytes / 1024)} KB`;
}

function getErrorMessage(error: unknown): string {
  if (typeof error === "object" && error !== null && "detail" in error) {
    const detail = (error as { detail?: unknown }).detail;
    if (typeof detail === "string" && detail.trim().length > 0) {
      return detail;
    }
  }

  return "Feedback could not be sent. Check the fields and try again.";
}

export function FeedbackDialog(): React.JSX.Element {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [open, setOpen] = useState(false);
  const [kind, setKind] = useState<FeedbackKind>("Feedback");
  const [message, setMessage] = useState("");
  const [screenshots, setScreenshots] = useState<File[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const pastedScreenshotCount = useRef(0);
  const [isPending, startTransition] = useTransition();

  const pagePath = useMemo(() => {
    const query = searchParams.toString();
    return query.length > 0 ? `${pathname}?${query}` : pathname;
  }, [pathname, searchParams]);

  function resetForm(): void {
    setKind("Feedback");
    setMessage("");
    setScreenshots([]);
    pastedScreenshotCount.current = 0;
    setError(null);
    setSent(false);
  }

  function handleOpenChange(nextOpen: boolean): void {
    setOpen(nextOpen);
    if (!nextOpen) {
      resetForm();
    }
  }

  function addScreenshots(files: File[]): void {
    if (files.length === 0) {
      return;
    }

    const oversized = files.find((file) => file.size > MaxScreenshotBytes);
    if (oversized) {
      setError(`${oversized.name} is larger than 2 MB.`);
      return;
    }

    if (screenshots.length + files.length > MaxScreenshots) {
      setError("You can attach up to 5 screenshots.");
      return;
    }

    setError(null);
    setScreenshots((current) => [...current, ...files]);
  }

  function handleFilesChange(event: React.ChangeEvent<HTMLInputElement>): void {
    const selectedFiles = Array.from(event.target.files ?? []);
    event.target.value = "";
    addScreenshots(selectedFiles);
  }

  function handlePaste(event: React.ClipboardEvent<HTMLFormElement>): void {
    if (isPending) {
      return;
    }

    const pastedImages = Array.from(event.clipboardData.items)
      .filter((item) => item.kind === "file" && item.type.startsWith("image/"))
      .map((item) => item.getAsFile())
      .filter((file): file is File => file !== null)
      .map((file) => {
        pastedScreenshotCount.current += 1;
        return new File([file], `pasted-screenshot-${pastedScreenshotCount.current}.png`, {
          type: file.type || "image/png",
        });
      });

    if (pastedImages.length === 0) {
      return;
    }

    event.preventDefault();
    addScreenshots(pastedImages);
  }

  function removeScreenshot(file: File): void {
    setScreenshots((current) => current.filter((item) => item !== file));
    setError(null);
  }

  function handleSubmit(event: React.FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setError(null);
    setSent(false);

    const trimmedMessage = message.trim();
    if (trimmedMessage.length < 10) {
      setError("Write at least 10 characters so the feedback is useful.");
      return;
    }

    startTransition(async () => {
      const result = await createFeedback({
        body: {
          kind,
          message: trimmedMessage,
          pagePath,
          userAgent: navigator.userAgent,
          viewportWidth: window.innerWidth,
          viewportHeight: window.innerHeight,
          locale: navigator.language,
          timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
          screenshots,
        },
      });

      if (result.error) {
        setError(getErrorMessage(result.error));
        return;
      }

      setSent(true);
      setMessage("");
      setScreenshots([]);
      pastedScreenshotCount.current = 0;
    });
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger
        render={<Button variant="outline" size="sm" className="w-full justify-start gap-2" />}
      >
        <MessageSquare className="size-4" />
        Give feedback
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <form onSubmit={handleSubmit} onPaste={handlePaste} className="grid gap-4">
          <DialogHeader>
            <DialogTitle>Give feedback</DialogTitle>
            <DialogDescription>
              Tell me what happened or what would make Grundet better.
            </DialogDescription>
          </DialogHeader>

          {sent && (
            <p className="bg-success-50 text-success-700 rounded-md px-3 py-2 text-sm">
              Thanks – your feedback was sent.
            </p>
          )}

          <div className="grid gap-1.5">
            <Label>Type</Label>
            <Select value={kind} onValueChange={(value) => setKind(value as FeedbackKind)}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Feedback">Feedback</SelectItem>
                <SelectItem value="Bug">Report bug</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="feedback-message">Message</Label>
            <Textarea
              id="feedback-message"
              value={message}
              onChange={(event) => setMessage(event.target.value)}
              placeholder="What happened, or what should be better?"
              rows={6}
              required={true}
              maxLength={TEXT_LIMITS.feedback}
            />
            <p className="text-muted-foreground text-xs">Minimum 10 characters.</p>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="feedback-screenshots">Screenshots</Label>
            <input
              id="feedback-screenshots"
              type="file"
              accept="image/*"
              multiple={true}
              onChange={handleFilesChange}
              disabled={screenshots.length >= MaxScreenshots || isPending}
              className="border-input file:bg-muted file:text-foreground hover:file:bg-muted/80 focus-visible:border-ring focus-visible:ring-ring/50 rounded-lg border bg-transparent text-sm file:mr-3 file:border-0 file:px-3 file:py-2 file:text-sm file:font-medium focus-visible:ring-3 focus-visible:outline-none"
            />
            <p className="text-muted-foreground text-xs">
              Optional. Up to 5 images, 2 MB each. Paste screenshots with ⌘V / Ctrl+V.
            </p>
            {screenshots.length > 0 && (
              <ul className="border-border divide-border rounded-md border">
                {screenshots.map((file) => (
                  <li
                    key={`${file.name}:${file.size}:${file.lastModified}`}
                    className="flex items-center gap-2 px-3 py-2 text-sm"
                  >
                    <span className="min-w-0 flex-1 truncate">{file.name}</span>
                    <span className="text-muted-foreground shrink-0 text-xs">
                      {formatBytes(file.size)}
                    </span>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-xs"
                      onClick={() => removeScreenshot(file)}
                      aria-label={`Remove ${file.name}`}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {error && <p className="text-destructive text-sm">{error}</p>}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setOpen(false)}
              disabled={isPending}
            >
              Close
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? "Sending…" : "Send feedback"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
