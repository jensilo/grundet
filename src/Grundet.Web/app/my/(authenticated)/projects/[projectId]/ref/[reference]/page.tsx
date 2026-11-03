import { redirect } from "next/navigation";

import { resolveProjectReference } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";
import { type ReferenceKind, referenceTargetHref } from "@/lib/references";

// Server-side resolver for a soft reference. Keeps reference links real anchors
// (right-click, open-in-new-tab, keyboard, no-JS all work): it resolves the
// reference and redirects to the target, or to project search when it can't.
export default async function ReferenceResolverPage({
  params,
}: {
  params: Promise<{ projectId: string; reference: string }>;
}): Promise<never> {
  const { projectId, reference } = await params;
  const ref = decodeURIComponent(reference);

  const serverClient = await createServerClient();
  const { data } = await resolveProjectReference({
    client: serverClient,
    path: { projectId },
    query: { ref },
  });

  if (data) {
    redirect(referenceTargetHref(projectId, data.type as ReferenceKind, data.id));
  }

  redirect(`/projects/${projectId}/search?q=${encodeURIComponent(ref)}`);
}
