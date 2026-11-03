import { getProjectById } from "@/lib/api/generated/sdk.gen";
import type { ProjectResponse } from "@/lib/api/generated/types.gen";
import type { createServerClient } from "@/lib/api/server";

type ServerClient = Awaited<ReturnType<typeof createServerClient>>;

export async function findProject(
  serverClient: ServerClient,
  projectId: string
): Promise<ProjectResponse | null> {
  const { data, error } = await getProjectById({ client: serverClient, path: { projectId } });
  if (error || !data) {
    return null;
  }
  return data;
}
