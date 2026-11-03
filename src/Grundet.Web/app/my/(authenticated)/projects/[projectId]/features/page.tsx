import { Layers3 } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";

import { PageError } from "@/components/app/page-error";
import { Button } from "@/components/ui/button";
import { listFeatures, listTeamMembers } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

import { FeatureList } from "./_components/feature-form";

export default async function FeaturesPage({
  params,
}: {
  params: Promise<{ projectId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId } = await params;

  const serverClient = await createServerClient();
  const project = await findProject(serverClient, projectId);

  if (!project) {
    notFound();
  }

  const [featuresResult, teamMembersResult] = await Promise.all([
    listFeatures({ client: serverClient, path: { projectId }, query: { pageSize: 200 } }),
    listTeamMembers({ client: serverClient, path: { teamId: project.teamId } }),
  ]);
  const { data: features, error: featuresError } = featuresResult;
  const teamMembers: TeamMemberResponse[] = teamMembersResult.data ?? [];

  return (
    <div className="mx-auto max-w-[1080px] space-y-6">
      <header className="space-y-4">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div className="max-w-[720px] space-y-3">
            <div className="text-muted-foreground flex items-center gap-2 text-sm">
              <Layers3 className="size-4" aria-hidden={true} />
              {project.name}
            </div>
            <div className="space-y-2">
              <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
                Features
              </h1>
              <p className="text-muted-foreground text-sm">
                Organise product intent into natural capability areas. Open a feature to view and
                evolve the intent that defines what should be true.
              </p>
            </div>
          </div>
          <Button
            variant="outline"
            size="sm"
            nativeButton={false}
            render={<Link href={`/projects/${projectId}/requirements`}>View all intent</Link>}
          />
        </div>
      </header>

      {featuresError ? (
        <PageError message="Could not load features. Please try again." />
      ) : (
        <FeatureList
          features={features?.items ?? []}
          projectId={projectId}
          teamMembers={teamMembers}
        />
      )}
    </div>
  );
}
