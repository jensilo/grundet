"use client";

import { ArrowUpRight } from "lucide-react";
import Link from "next/link";
import type * as React from "react";

import { ReferenceText } from "@/components/app/reference-text";
import { VersionHistoryList } from "@/components/app/version-history-list";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import type { RequirementVersionEntry, WorkItemVersionEntry } from "@/lib/api/generated/types.gen";

type AnyVersionEntry = RequirementVersionEntry | WorkItemVersionEntry;

/**
 * In-context full history: the detail page already loads every version, so the
 * dialog renders from props with no extra fetch. `historyHref` keeps a
 * deep-linkable page (per-version `#v{n}` anchors) reachable for sharing.
 */
export function VersionHistoryDialog({
  versions,
  projectId,
  reference,
  title,
  historyHref,
}: {
  versions: AnyVersionEntry[];
  projectId: string;
  reference: string;
  title: string;
  historyHref: string;
}): React.JSX.Element {
  return (
    <Dialog>
      <DialogTrigger className="text-primary mt-4 inline-block text-xs font-medium hover:underline">
        View full history →
      </DialogTrigger>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle className="pr-8 leading-snug">
            <span className="text-primary mr-2 font-mono text-sm">{reference}</span>
            <ReferenceText projectId={projectId} text={title} />
          </DialogTitle>
          <DialogDescription>Full version history</DialogDescription>
        </DialogHeader>

        <div className="max-h-[70vh] overflow-y-auto px-1 py-1">
          <VersionHistoryList versions={versions} projectId={projectId} />
        </div>

        <Link
          href={historyHref}
          className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 text-xs transition-colors"
        >
          Open as page
          <ArrowUpRight className="size-3.5" />
        </Link>
      </DialogContent>
    </Dialog>
  );
}
