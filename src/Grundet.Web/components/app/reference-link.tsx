"use client";

import Link from "next/link";
import { useState } from "react";

import { StatusBadge } from "@/components/app/status-badge";
import { PreviewCard, PreviewCardContent, PreviewCardTrigger } from "@/components/ui/preview-card";
import { resolveProjectReference } from "@/lib/api/generated/sdk.gen";
import type { ResolveProjectReferenceResponse } from "@/lib/api/generated/types.gen";
import {
  type ReferenceKind,
  referenceKindLabel,
  referenceResolverHref,
  referenceTargetHref,
} from "@/lib/references";
import { cn } from "@/lib/utils";

type ResolvedState =
  { status: "found"; data: ResolveProjectReferenceResponse } | { status: "missing" };

// Resolution is shared across every reference on the page: one network call per
// distinct reference, cached for the lifetime of the document.
const resultCache = new Map<string, ResolvedState>();
const inflight = new Map<string, Promise<ResolvedState>>();

async function resolveReference(projectId: string, query: string): Promise<ResolvedState> {
  const key = `${projectId}:${query}`;
  const cached = resultCache.get(key);
  if (cached) {
    return cached;
  }

  const existing = inflight.get(key);
  if (existing) {
    return existing;
  }

  const request = (async (): Promise<ResolvedState> => {
    try {
      const { data } = await resolveProjectReference({
        path: { projectId },
        query: { ref: query },
      });
      const state: ResolvedState = data ? { status: "found", data } : { status: "missing" };
      resultCache.set(key, state);
      return state;
    } finally {
      inflight.delete(key);
    }
  })();

  inflight.set(key, request);
  return request;
}

export function ReferenceLink({
  projectId,
  label,
  query,
  className,
}: {
  projectId: string;
  label: string;
  query: string;
  className?: string;
}): React.JSX.Element {
  const [state, setState] = useState<ResolvedState | "loading" | null>(null);

  function handleOpenChange(open: boolean): void {
    if (open && state === null) {
      setState("loading");
      resolveReference(projectId, query)
        .then(setState)
        .catch(() => setState({ status: "missing" }));
    }
  }

  const resolved =
    state !== null && state !== "loading" && state.status === "found" ? state.data : null;
  const isMissing = state !== null && state !== "loading" && state.status === "missing";

  // Before resolution (and without JavaScript) the link points at the resolver
  // route, which redirects server-side. Once resolved it points straight at the
  // target for instant client navigation.
  const href = resolved
    ? referenceTargetHref(projectId, resolved.type as ReferenceKind, resolved.id)
    : referenceResolverHref(projectId, query);

  return (
    <PreviewCard onOpenChange={handleOpenChange}>
      <PreviewCardTrigger
        render={<Link href={href} prefetch={false} />}
        className={cn(
          "text-primary hover:text-primary-hover focus-visible:ring-ring/40 rounded-sm font-mono text-[0.95em] font-medium underline decoration-dotted underline-offset-2 focus-visible:ring-2 focus-visible:outline-none",
          className
        )}
      >
        {label}
      </PreviewCardTrigger>
      <PreviewCardContent>
        {resolved ? (
          <Link href={href} prefetch={false} className="block space-y-2">
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
                {referenceKindLabel(resolved.type as ReferenceKind)}
              </span>
              <span className="text-primary font-mono text-xs font-medium">{resolved.ref}</span>
              <StatusBadge status={resolved.status} className="ml-auto" />
            </div>
            <p className="text-foreground line-clamp-3 text-sm leading-snug">{resolved.title}</p>
          </Link>
        ) : isMissing ? (
          <div className="space-y-1">
            <p className="text-foreground text-sm font-medium">{label}</p>
            <p className="text-muted-foreground text-xs leading-relaxed">
              Nothing resolves to this reference – it may have been archived or deleted.
            </p>
          </div>
        ) : (
          <p className="text-muted-foreground text-sm">Resolving {label} …</p>
        )}
      </PreviewCardContent>
    </PreviewCard>
  );
}
