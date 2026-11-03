"use client";

import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { updateMe } from "@/lib/api/generated/sdk.gen";

type State = { type: "idle" } | { type: "loading" } | { type: "error"; message: string };

// The backend rejects invalid display names with ProblemDetails whose `detail` is
// user-facing copy (e.g. length limit). Show it verbatim; fall back to generic retry
// copy only when no detail is present.
function getErrorMessage(error: unknown): string {
  if (typeof error === "object" && error !== null && "detail" in error) {
    const detail = (error as { detail?: unknown }).detail;
    if (typeof detail === "string" && detail.trim().length > 0) {
      return detail;
    }
  }

  return "Something went wrong. Please try again.";
}

// Step 1 — the display-name gate. Everyone (solo or invited) advances in place to the
// orientation step; the invite context is carried into Step 2, not short-circuited here.
export function OnboardingForm({
  onContinue,
}: {
  onContinue: (name: string) => void;
}): React.JSX.Element {
  const [name, setName] = useState("");
  const [state, setState] = useState<State>({ type: "idle" });

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setState({ type: "loading" });
    const { error } = await updateMe({ body: { displayName: name } });
    if (error) {
      setState({ type: "error", message: getErrorMessage(error) });
      return;
    }

    onContinue(name);
  }

  return (
    <div className="mx-auto w-full max-w-[360px]">
      <form onSubmit={handleSubmit} className="space-y-5">
        <h1 className="text-foreground mb-6 text-center text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          What should we call you?
        </h1>
        <div>
          <Label htmlFor="name" className="text-foreground mb-1.5 block text-sm font-medium">
            Display name
          </Label>
          <Input
            id="name"
            type="text"
            placeholder="Your name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            maxLength={120}
            autoFocus
          />
        </div>
        {state.type === "error" && <p className="text-destructive text-xs">{state.message}</p>}
        <Button
          type="submit"
          className="w-full"
          disabled={state.type === "loading" || name.trim().length === 0}
        >
          {state.type === "loading" ? "Saving…" : "Continue"}
        </Button>
      </form>
    </div>
  );
}
