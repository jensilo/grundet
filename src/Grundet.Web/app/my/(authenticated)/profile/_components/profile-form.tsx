"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { updateMe } from "@/lib/api/generated/sdk.gen";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type MutationState =
  | { type: "idle" }
  | { type: "submitting" }
  | { type: "success" }
  | { type: "error"; message: string };

type Props = {
  displayName: string;
  email: string;
};

// ---------------------------------------------------------------------------
// Field helpers
// ---------------------------------------------------------------------------

function FieldGroup({ children }: { children: React.ReactNode }): React.JSX.Element {
  return <div className="space-y-1.5">{children}</div>;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function ProfileForm({ displayName: initialDisplayName, email }: Props): React.JSX.Element {
  const router = useRouter();

  const [value, setValue] = useState(initialDisplayName);
  const [mutationState, setMutationState] = useState<MutationState>({ type: "idle" });

  const isSubmitting = mutationState.type === "submitting";

  // Auto-clear success message after 3 seconds
  useEffect(() => {
    if (mutationState.type === "success") {
      const id = setTimeout(() => setMutationState({ type: "idle" }), 3000);
      return () => clearTimeout(id);
    }
  }, [mutationState.type]);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();

    if (!value.trim()) return;

    setMutationState({ type: "submitting" });

    const { error } = await updateMe({
      body: { displayName: value.trim() },
    });

    if (error) {
      setMutationState({
        type: "error",
        message: "Failed to update display name. Please try again.",
      });
      return;
    }

    setMutationState({ type: "success" });
    router.refresh();
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-[480px] space-y-5">
      {/* Display name */}
      <FieldGroup>
        <Label htmlFor="profile-displayName" className="text-foreground block text-sm font-medium">
          Display name
          <span className="text-primary ml-0.5">*</span>
        </Label>
        <Input
          id="profile-displayName"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder="Your name"
          required
          disabled={isSubmitting}
        />
      </FieldGroup>

      {/* Email */}
      <FieldGroup>
        <Label htmlFor="profile-email" className="text-foreground block text-sm font-medium">
          Email
        </Label>
        <Input id="profile-email" type="email" value={email} readOnly disabled />
        <p className="text-muted-foreground text-xs">Email cannot be changed.</p>
      </FieldGroup>

      {/* Error */}
      {mutationState.type === "error" && (
        <p role="alert" className="text-destructive text-xs">
          {mutationState.message}
        </p>
      )}

      {/* Success */}
      {mutationState.type === "success" && (
        <p role="status" className="text-success-700 text-xs">
          Display name updated.
        </p>
      )}

      {/* Submit button */}
      <Button type="submit" disabled={!value.trim() || isSubmitting}>
        {isSubmitting ? "Saving…" : "Save"}
      </Button>
    </form>
  );
}
