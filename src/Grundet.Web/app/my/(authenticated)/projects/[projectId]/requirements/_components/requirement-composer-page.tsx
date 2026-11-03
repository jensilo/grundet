"use client";

import { useRouter } from "next/navigation";

import type { AttachmentResponse, RequirementResponse } from "@/lib/api/generated/types.gen";

import { RequirementComposer } from "./requirement-composer";

type RequirementComposerPageProps = {
  projectId: string;
  projectName: string;
  currentFeature: { id: string; name: string };
  featureCount: number;
} & (
  | { mode: "create"; featureId: string }
  | {
      mode: "edit";
      initial: RequirementResponse;
      initialAttachments: AttachmentResponse[];
      attachmentsLoaded: boolean;
    }
);

/**
 * Full-page host for the Intent composer — the deep-linkable `/new` and `/edit` fallback. Renders
 * the same `RequirementComposer` as the dialog host; cancel returns to the originating surface and
 * success navigates to the intent detail page.
 */
export function RequirementComposerPage(props: RequirementComposerPageProps): React.JSX.Element {
  const router = useRouter();
  const onCancel = (): void => router.back();
  const onSuccess = (requirementId: string): void =>
    router.push(`/projects/${props.projectId}/requirements/${requirementId}`);

  return props.mode === "create" ? (
    <RequirementComposer
      mode="create"
      projectId={props.projectId}
      projectName={props.projectName}
      featureId={props.featureId}
      currentFeature={props.currentFeature}
      featureCount={props.featureCount}
      surface="page"
      onCancel={onCancel}
      onSuccess={onSuccess}
    />
  ) : (
    <RequirementComposer
      mode="edit"
      projectId={props.projectId}
      projectName={props.projectName}
      initial={props.initial}
      currentFeature={props.currentFeature}
      featureCount={props.featureCount}
      initialAttachments={props.initialAttachments}
      attachmentsLoaded={props.attachmentsLoaded}
      surface="page"
      onCancel={onCancel}
      onSuccess={onSuccess}
    />
  );
}
