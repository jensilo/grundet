import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import { getMe } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

import { OnboardingFlow } from "./onboarding-flow";

export default async function OnboardingPage(): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();
  const { data, error } = await getMe({ client: serverClient });

  if (error || !data) {
    redirect("/login");
  }

  const cookieStore = await cookies();
  const pendingInviteToken = cookieStore.get("grundet_pending_invite_token")?.value;
  const inviteHref = pendingInviteToken
    ? `/invite/accept?token=${encodeURIComponent(pendingInviteToken)}`
    : null;

  // Precedence: the display-name gate (Step 1) comes first. Once past it, a user who has
  // already completed onboarding skips the flow — honouring a pending invite if present,
  // else the dashboard (this is the path an existing member takes when accepting an
  // invite). Brand-new users (completion flag null) resume Step 2; the orientation carries
  // the invite context so an invitee still lands on their new team afterwards.
  if (data.displayName && data.onboardingCompletedAt) {
    redirect(inviteHref ?? "/dashboard");
  }

  return (
    <OnboardingFlow
      initialStep={data.displayName ? "orientation" : "name"}
      initialDisplayName={data.displayName ?? null}
      inviteHref={inviteHref}
    />
  );
}
