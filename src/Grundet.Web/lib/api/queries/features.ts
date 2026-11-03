import { getFeature, listFeatures } from "@/lib/api/generated/sdk.gen";
import type { FeatureResponse } from "@/lib/api/generated/types.gen";
import type { createServerClient } from "@/lib/api/server";

type ServerClient = Awaited<ReturnType<typeof createServerClient>>;

export type FeatureOption = { id: string; name: string };

// The intent browse page needs every feature up front — it renders the feature filter facet and
// resolves each card's feature name from the full set. Page through with the max server page size
// until exhausted. The composer no longer uses this: it resolves its single current feature and a
// count instead (see getFeatureById / getFeatureCount), then searches the rest on demand.
const PAGE_SIZE = 200;

export async function listAllFeatures(
  serverClient: ServerClient,
  projectId: string
): Promise<{ features: FeatureResponse[]; error: boolean }> {
  const features: FeatureResponse[] = [];

  for (let page = 1; ; page += 1) {
    const { data, error } = await listFeatures({
      client: serverClient,
      path: { projectId },
      query: { page, pageSize: PAGE_SIZE },
    });

    if (error || !data) {
      return { features: [], error: true };
    }

    features.push(...data.items);

    if (!data.hasMore) {
      return { features, error: false };
    }
  }
}

// Resolve a single feature by id (composer current-feature label) without loading the whole set.
// Returns null when the feature is missing or inaccessible.
export async function getFeatureById(
  serverClient: ServerClient,
  projectId: string,
  featureId: string
): Promise<FeatureOption | null> {
  const { data, error } = await getFeature({
    client: serverClient,
    path: { projectId, featureId },
  });

  if (error || !data) {
    return null;
  }

  return { id: data.id, name: data.name };
}

// One cheap round-trip that yields the project's feature count (to decide whether the composer
// shows a picker at all) plus the most-recent feature (the create-mode default target).
export async function getFeatureCountAndFirst(
  serverClient: ServerClient,
  projectId: string
): Promise<{ count: number; first: FeatureOption | null; error: boolean }> {
  const { data, error } = await listFeatures({
    client: serverClient,
    path: { projectId },
    query: { page: 1, pageSize: 1 },
  });

  if (error || !data) {
    return { count: 0, first: null, error: true };
  }

  const item = data.items[0];
  return {
    count: Number(data.total),
    first: item ? { id: item.id, name: item.name } : null,
    error: false,
  };
}
