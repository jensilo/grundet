"use client";

import { FileText, FolderOpen, Layers, ListChecks } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { completeOnboarding, listTeams } from "@/lib/api/generated/sdk.gen";

const OVERVIEW = [
  {
    label: "Project",
    description: "The product space – where features, intent, and work live.",
    icon: FolderOpen,
  },
  {
    label: "Feature",
    description: "Groups related intent into one capability.",
    icon: Layers,
  },
  {
    label: "Intent",
    description: "What should be true, why it matters, and how you know it is done.",
    icon: FileText,
  },
  {
    label: "Work",
    description: "Who is making a slice of intent true.",
    icon: ListChecks,
  },
] as const;

// The solo primary CTA links into the team's project surface. Resolved at click time —
// only then has Step 1 set the display name, which the API's onboarding guard requires
// for the teams fetch. On 0 / failed / multiple teams fall back to the dashboard, whose
// empty state is itself a creation entry. Never block the flow on this fetch.
async function resolveCreateProjectHref(): Promise<string> {
  try {
    const { data: teams, error } = await listTeams();

    if (error || !teams || teams.length !== 1) {
      return "/dashboard";
    }

    return `/teams/${teams[0].id}/projects?new=1`;
  } catch {
    return "/dashboard";
  }
}

// Step 2 — orientation. Guided, never gated: both actions mark onboarding complete on
// the server (a durable, cross-device fact), then route. Completion is best-effort —
// a failed call must never trap the user here, so navigation always proceeds.
//
// Two variants, keyed on `inviteHref`:
//  - Solo (no invite): primary "Create your first project", both actions land in-app.
//  - Invited: the user is joining an existing team, so "create a project" is wrong.
//    Both actions route through the invite-accept href, which accepts the invite and
//    lands them on their new team – the invite context is never lost.
export function OnboardingOrientation({
  displayName,
  inviteHref,
}: {
  displayName: string;
  inviteHref: string | null;
}): React.JSX.Element {
  const router = useRouter();
  const [pending, setPending] = useState<"primary" | "skip" | null>(null);
  const isInvite = inviteHref !== null;

  async function completeAndGo(action: "primary" | "skip"): Promise<void> {
    if (pending) {
      return;
    }
    setPending(action);
    try {
      await completeOnboarding();
    } catch {
      // Ignored — the flag stays null and /onboarding simply remains resumable.
    }

    if (isInvite) {
      // Both actions must reach the invite-accept route so the user actually joins the
      // team; there is nothing to "skip past" for an invitee.
      router.push(inviteHref);
      return;
    }

    router.push(action === "primary" ? await resolveCreateProjectHref() : "/dashboard");
  }

  const name = displayName.trim();

  return (
    <div className="motion-safe:animate-in motion-safe:fade-in-0 motion-safe:slide-in-from-bottom-3 mx-auto w-full max-w-[480px] space-y-6 motion-safe:duration-[400ms] motion-safe:ease-out">
      <div className="space-y-2 text-center">
        <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          {name ? `Welcome, ${name}` : "Welcome"}
        </h1>
        <p className="text-muted-foreground mx-auto max-w-[400px] text-[0.9375rem] leading-relaxed">
          {isInvite
            ? "You have been invited to a team. Grundet keeps your product truth versioned – here is the shape of it before you jump in."
            : "Grundet keeps your product truth versioned – every change, every reason. Here is the shape of it."}
        </p>
      </div>

      <Card>
        <CardContent>
          <ol className="space-y-4">
            {OVERVIEW.map(({ label, description, icon: Icon }) => (
              <li key={label} className="flex items-start gap-3">
                <span
                  className="bg-muted text-muted-foreground flex size-8 shrink-0 items-center justify-center rounded-md"
                  aria-hidden={true}
                >
                  <Icon className="size-4" />
                </span>
                <div className="min-w-0 pt-0.5">
                  <p className="text-foreground text-sm font-medium">{label}</p>
                  <p className="text-muted-foreground text-sm">{description}</p>
                </div>
              </li>
            ))}
          </ol>
        </CardContent>
      </Card>

      <div className="space-y-2">
        <Button
          type="button"
          className="w-full"
          disabled={pending !== null}
          onClick={() => completeAndGo("primary")}
        >
          {pending === "primary"
            ? "Continuing…"
            : isInvite
              ? "Go to your team"
              : "Create your first project"}
        </Button>
        <Button
          type="button"
          variant="ghost"
          className="text-muted-foreground w-full"
          disabled={pending !== null}
          onClick={() => completeAndGo("skip")}
        >
          Skip for now
        </Button>
      </div>

      <p className="text-muted-foreground text-center text-xs">
        {isInvite
          ? "You can explore at your own pace – every empty page shows you the next step."
          : "Skipping is fine – every empty page shows you the next step."}
      </p>
    </div>
  );
}
