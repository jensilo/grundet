"use client";

import { useRouter } from "next/navigation";

import type {
  AttachmentResponse,
  TeamMemberResponse,
  WorkItemResponse,
} from "@/lib/api/generated/types.gen";

import { WorkItemComposer } from "./work-item-composer";

type WorkItemComposerPageProps = {
  projectId: string;
  projectName: string;
  teamMembers: TeamMemberResponse[];
} & (
  | { mode: "create" }
  | {
      mode: "edit";
      initial: WorkItemResponse;
      initialAttachments: AttachmentResponse[];
      attachmentsLoaded: boolean;
    }
);

/**
 * Full-page host for the Work composer — the deep-linkable `/new` and `/edit` fallback. Renders the
 * same `WorkItemComposer` as the dialog host; cancel returns to the originating surface and success
 * navigates to the work item detail page.
 */
export function WorkItemComposerPage(props: WorkItemComposerPageProps): React.JSX.Element {
  const router = useRouter();
  const onCancel = (): void => router.back();
  const onSuccess = (workItemId: string): void =>
    router.push(`/projects/${props.projectId}/work-items/${workItemId}`);

  return props.mode === "create" ? (
    <WorkItemComposer
      mode="create"
      projectId={props.projectId}
      projectName={props.projectName}
      teamMembers={props.teamMembers}
      surface="page"
      onCancel={onCancel}
      onSuccess={onSuccess}
    />
  ) : (
    <WorkItemComposer
      mode="edit"
      projectId={props.projectId}
      projectName={props.projectName}
      teamMembers={props.teamMembers}
      initial={props.initial}
      initialAttachments={props.initialAttachments}
      attachmentsLoaded={props.attachmentsLoaded}
      surface="page"
      onCancel={onCancel}
      onSuccess={onSuccess}
    />
  );
}
