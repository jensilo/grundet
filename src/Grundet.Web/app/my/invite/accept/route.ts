import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { NextRequest, NextResponse } from "next/server";

import { acceptInvite, completeOnboarding, getMe } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

const InviteTokenCookieName = "grundet_pending_invite_token";
const InvalidTokenProblemType = "https://grundet.app/errors/invalid-token";

function storePendingInviteToken(
  cookieStore: Awaited<ReturnType<typeof cookies>>,
  token: string
): void {
  cookieStore.set(InviteTokenCookieName, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: 60 * 60 * 24 * 7,
  });
}

export async function GET(request: NextRequest): Promise<NextResponse> {
  const token = request.nextUrl.searchParams.get("token")?.trim();

  if (!token) {
    redirect("/login");
  }

  const cookieStore = await cookies();
  const serverClient = await createServerClient();
  const { data: me } = await getMe({ client: serverClient });

  if (!me) {
    storePendingInviteToken(cookieStore, token);

    redirect("/login");
  }

  const { data, error } = await acceptInvite({
    client: serverClient,
    body: { token },
  });

  if (error || !data) {
    if (error?.type === InvalidTokenProblemType) {
      cookieStore.delete(InviteTokenCookieName);
      redirect("/auth/error?reason=invalid_invite");
    }

    storePendingInviteToken(cookieStore, token);
    redirect("/auth/error?reason=invite_accept_failed");
  }

  // Existing members reach this route directly (the authenticated layout forwards them
  // here once their display name is set), so defensively mark onboarding complete
  // (idempotent set-if-null) so the flow can't nag them on another device. Brand-new
  // invitees have already completed it in the orientation step; the call is a no-op for
  // them. Best effort: a failure must not break invite acceptance.
  try {
    await completeOnboarding({ client: serverClient });
  } catch {
    // Ignored — see above.
  }

  cookieStore.delete(InviteTokenCookieName);
  // Land on the joined team's projects page — it matches the "Go to your team" CTA and is
  // a working surface for every role (the team workspace page is read-only for Contributors).
  redirect(`/teams/${data.teamId}/projects`);
}
