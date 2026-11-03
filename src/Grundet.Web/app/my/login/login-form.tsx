"use client";

import { ArrowRight } from "lucide-react";
import { useState } from "react";

import { Logo } from "@/components/brand/logo";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { sendMagicLink } from "@/lib/api/generated/sdk.gen";

type State =
  | { type: "idle" }
  | { type: "loading" }
  | { type: "sent"; email: string }
  | { type: "error"; message: string };

export function LoginForm({
  joinAlphaUrl,
  hasPendingInvite = false,
}: {
  joinAlphaUrl: string;
  hasPendingInvite?: boolean;
}): React.JSX.Element {
  const [email, setEmail] = useState("");
  const [state, setState] = useState<State>({ type: "idle" });

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setState({ type: "loading" });

    const submittedEmail = email.trim();

    try {
      const { error } = await sendMagicLink({ body: { email: submittedEmail } });
      if (error) {
        setState({ type: "error", message: "Something went wrong. Please try again." });
      } else {
        setState({ type: "sent", email: submittedEmail });
      }
    } catch {
      setState({ type: "error", message: "Something went wrong. Please try again." });
    }
  }

  return (
    <div className="bg-background flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-[360px]">
        <div className="mb-8 flex justify-center">
          <Logo size="lg" />
        </div>

        {state.type === "sent" ? (
          <div className="space-y-3 text-center">
            <p className="text-foreground text-base font-semibold">Check your email</p>
            <p className="text-muted-foreground text-[0.9375rem] leading-relaxed">
              We sent a sign-in link to <span className="text-foreground">{state.email}</span>.
            </p>
            <button
              type="button"
              className="text-muted-foreground hover:text-foreground text-sm underline-offset-4 hover:underline"
              onClick={() => setState({ type: "idle" })}
            >
              Use a different email
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <p className="text-muted-foreground mb-4 text-center text-[0.9375rem] leading-relaxed">
                Enter your email and we&apos;ll send you a sign-in link.
              </p>
              {hasPendingInvite ? (
                <div className="border-border bg-card rounded-md border p-3 text-sm">
                  <p className="text-foreground text-base leading-[1.4] font-semibold">
                    You have a team invitation
                  </p>
                  <p className="text-muted-foreground mt-1">
                    Sign in with your email to accept it – new accounts are approved automatically
                    for invites.
                  </p>
                </div>
              ) : (
                <div className="border-border bg-card rounded-md border p-3 text-sm">
                  <p className="text-foreground font-medium">Need alpha access first?</p>
                  <p className="text-muted-foreground mt-1">
                    New accounts require approval during the private alpha.
                  </p>
                  <a
                    href={joinAlphaUrl}
                    className="text-foreground mt-2 inline-flex items-center gap-1.5 underline-offset-4 hover:underline"
                  >
                    Join the alpha
                    <ArrowRight className="size-3.5" aria-hidden="true" />
                  </a>
                </div>
              )}
            </div>
            <div>
              <Label htmlFor="email" className="text-foreground mb-1.5 block text-sm font-medium">
                Email
              </Label>
              <Input
                id="email"
                type="email"
                placeholder="you@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoFocus
              />
            </div>
            {state.type === "error" && <p className="text-destructive text-xs">{state.message}</p>}
            <Button type="submit" className="w-full" disabled={state.type === "loading"}>
              {state.type === "loading" ? "Sending…" : "Send sign-in link"}
            </Button>
          </form>
        )}
      </div>
    </div>
  );
}
