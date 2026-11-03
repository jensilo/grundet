import { History } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";
import type * as React from "react";

import { EmptyState } from "@/components/app/empty-state";
import { PageError } from "@/components/app/page-error";
import { ReferenceText } from "@/components/app/reference-text";
import { VersionHistoryList } from "@/components/app/version-history-list";
import { getProjectWorkItem, getWorkItemVersionHistory } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

export default async function WorkItemHistoryPage({
  params,
}: {
  params: Promise<{ projectId: string; workItemId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId, workItemId } = await params;

  const serverClient = await createServerClient();

  const [workItemResult, versionsResult] = await Promise.all([
    getProjectWorkItem({ client: serverClient, path: { projectId, workItemId } }),
    getWorkItemVersionHistory({ client: serverClient, path: { workItemId } }),
  ]);

  if (workItemResult.error || !workItemResult.data) {
    notFound();
  }

  const { workItem } = workItemResult.data;
  const versions = versionsResult.data ?? [];

  return (
    <div className="mx-auto max-w-[720px] space-y-6 py-8">
      <div className="flex flex-col gap-4">
        <Link
          href={`/projects/${projectId}/work-items/${workItemId}`}
          className="text-muted-foreground hover:text-foreground w-fit text-xs transition-colors"
        >
          ← {workItem.ref}
        </Link>
        <div className="space-y-1">
          <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
            Full history
          </p>
          <h1 className="text-foreground text-2xl leading-[1.3] font-semibold tracking-[-0.01em]">
            <ReferenceText projectId={projectId} text={workItem.title} />
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
