"use client";

import { useState } from "react";

import { Logo } from "@/components/brand/logo";

import { OnboardingForm } from "./onboarding-form";
import { OnboardingOrientation } from "./onboarding-orientation";

// Two-step first-run flow. Step 1 (display name) is the only hard gate; Step 2
// (orientation) is guided-not-gated — skip is always one click. Brand-new invited users
// see both steps too; the invite context (inviteHref) is carried into Step 2, whose
// actions route through the invite-accept path so the team is never lost.
export function OnboardingFlow({
  initialStep,
  initialDisplayName,
  inviteHref,
}: {
  initialStep: "name" | "orientation";
  initialDisplayName: string | null;
  inviteHref: string | null;
}): React.JSX.Element {
  const [step, setStep] = useState(initialStep);
  const [displayName, setDisplayName] = useState(initialDisplayName ?? "");

  return (
    <div className="bg-background flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full">
        <div className="mb-8 flex justify-center">
          <Logo size="lg" />
        </div>

        {step === "name" ? (
          <OnboardingForm
            onContinue={(name) => {
              setDisplayName(name);
              setStep("orientation");
            }}
          />
        ) : (
          <OnboardingOrientation displayName={displayName} inviteHref={inviteHref} />
        )}
      </div>
    </div>
  );
}
