import { History } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";
import type * as React from "react";

import { EmptyState } from "@/components/app/empty-state";
import { PageError } from "@/components/app/page-error";
import { ReferenceText } from "@/components/app/reference-text";
import { VersionHistoryList } from "@/components/app/version-history-list";
import { getProjectRequirement, getRequirementVersionHistory } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

export default async function RequirementHistoryPage({
  params,
}: {
  params: Promise<{ projectId: string; reqId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId, reqId } = await params;

  const serverClient = await createServerClient();

  const [requirementResult, versionsResult] = await Promise.all([
    getProjectRequirement({ client: serverClient, path: { projectId, requirementId: reqId } }),
    getRequirementVersionHistory({ client: serverClient, path: { requirementId: reqId } }),
  ]);

  if (requirementResult.error || !requirementResult.data) {
    notFound();
  }

  const requirement = requirementResult.data;
  const versions = versionsResult.data ?? [];

  return (
    <div className="mx-auto max-w-[720px] space-y-6 py-8">
      <div className="flex flex-col gap-4">
        <Link
          href={`/projects/${projectId}/requirements/${reqId}`}
          className="text-muted-foreground hover:text-foreground w-fit text-xs transition-colors"
        >
          ← {requirement.ref}
        </Link>
        <div className="space-y-1">
          <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
            Full history
          </p>
          <h1 className="text-foreground text-2xl leading-[1.3] font-semibold tracking-[-0.01em]">
            <ReferenceText projectId={projectId} text={requirement.text} />
          </h1>
        </div>
      </div>

      {versionsResult.error ? (
        <PageError message="Could not load the version history." />
      ) : versions.length > 0 ? (
        <VersionHistoryList versions={versions} projectId={projectId} />
      ) : (
        <div className="border-border rounded-xl border border-dashed">
          <EmptyState icon={History} title="No history yet" />
        </div>
      )}
    </div>
  );
}
