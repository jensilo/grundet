import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";

import { getMe } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";
import { buildMarketingUrl } from "@/lib/domains";

import { LoginForm } from "./login-form";

export default async function LoginPage(): Promise<React.JSX.Element> {
  // Already authenticated? Skip the form and send them to the app. `redirect` throws to perform the
  // control-flow jump, so it must stay at the top level — never inside a try/catch. A getMe error or
  // null means "not signed in": fall through and render the login form. Onboarding-incomplete users
  // are forwarded to /onboarding by the authenticated layout's own guard, so /dashboard is safe.
  const { data, error } = await getMe({ client: await createServerClient() });
  if (!error && data) {
    redirect("/dashboard");
  }

  const headerStore = await headers();
  const host = headerStore.get("host") ?? "my.grundet.app";
  const joinAlphaUrl = buildMarketingUrl(host, "/join-alpha");

  // Invitees land here from an invite link (the token is parked in an httpOnly cookie).
  // Their accounts are auto-approved server-side, so the alpha-access pitch would only
  // confuse them — show the invitation context instead.
  const cookieStore = await cookies();
  const hasPendingInvite = Boolean(cookieStore.get("grundet_pending_invite_token")?.value);

  return <LoginForm joinAlphaUrl={joinAlphaUrl} hasPendingInvite={hasPendingInvite} />;
}
