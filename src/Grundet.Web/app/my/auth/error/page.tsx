import { headers } from "next/headers";
import Link from "next/link";

import { Logo } from "@/components/brand/logo";
import { Button } from "@/components/ui/button";
import { buildMarketingUrl } from "@/lib/domains";

const ERROR_MESSAGES: Partial<Record<string, { heading: string; body: string }>> = {
  expired_link: {
    heading: "Link expired",
    body: "This sign-in link has expired. Links are valid for 30 minutes — request a new one to continue.",
  },
  private_alpha_access_required: {
    heading: "Alpha access required",
    body: "Grundet is currently in private alpha. Join the alpha waitlist to request access.",
  },
  invalid_invite: {
    heading: "Invite unavailable",
    body: "This invitation is invalid or has expired. Ask the team lead to send a new invitation.",
  },
  invite_accept_failed: {
    heading: "Invite not accepted",
    body: "The invitation is still saved. Log out, then sign in with the invited email account.",
  },
};

const DEFAULT_ERROR = {
  heading: "Sign-in failed",
  body: "Something went wrong with your sign-in link. Request a new one to continue.",
};

export default async function AuthErrorPage({
  searchParams,
}: {
  searchParams: Promise<{ reason?: string }>;
}): Promise<React.JSX.Element> {
  const { reason } = await searchParams;
  const headerStore = await headers();
  const host = headerStore.get("host") ?? "my.grundet.app";
  const joinAlphaUrl = buildMarketingUrl(host, "/join-alpha");
  const isPrivateAlphaError = reason === "private_alpha_access_required";
  const isInvalidInviteError = reason === "invalid_invite";
  const isInviteAcceptFailed = reason === "invite_accept_failed";
  const { heading, body } = (reason ? ERROR_MESSAGES[reason] : undefined) ?? DEFAULT_ERROR;

  return (
    <div className="bg-background flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-[360px]">
        <div className="mb-8 flex justify-center">
          <Logo size="lg" />
        </div>
        <div className="space-y-4 text-center">
          <p className="text-foreground text-base font-semibold">{heading}</p>
          <p className="text-muted-foreground text-[0.9375rem] leading-relaxed">{body}</p>
          {isPrivateAlphaError && (
            <Button className="mt-2 w-full" nativeButton={false} render={<a href={joinAlphaUrl} />}>
              Join alpha
            </Button>
          )}
          {isInvalidInviteError && (
            <Button
              className="mt-2 w-full"
              nativeButton={false}
              render={<Link href="/dashboard" />}
            >
              Go to dashboard
            </Button>
          )}
          {isInviteAcceptFailed && (
            <Button className="mt-2 w-full" nativeButton={false} render={<Link href="/login" />}>
              Sign in with another account
            </Button>
          )}
          {!isPrivateAlphaError && !isInvalidInviteError && !isInviteAcceptFailed && (
            <Button className="mt-2 w-full" nativeButton={false} render={<Link href="/login" />}>
              Request a new link
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
