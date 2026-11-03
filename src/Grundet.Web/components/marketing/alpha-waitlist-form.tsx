"use client";

import { CheckCircle2 } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { submitAlphaWaitlist } from "@/lib/api/generated/sdk.gen";

const genericSuccess = "Thanks – if alpha access becomes available, we will contact you.";

type AccountType = "Personal" | "Business";
type SubmitState = "idle" | "loading" | "success" | "error";

export function AlphaWaitlistForm({ initialEmail = "" }: { initialEmail?: string }) {
  const [accountType, setAccountType] = useState<AccountType>("Personal");
  const [teamSize, setTeamSize] = useState<string | null>(null);
  const [lineOfWork, setLineOfWork] = useState<string | null>(null);
  const [state, setState] = useState<SubmitState>("idle");

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setState("loading");

    const form = new FormData(event.currentTarget);
    const isBusiness = accountType === "Business";
    const reason = getFormValue(form, "reason");

    try {
      const { error } = await submitAlphaWaitlist({
        body: {
          accountType,
          name: getFormValue(form, "name"),
          email: getFormValue(form, "email"),
          teamSize: isBusiness ? teamSize : null,
          lineOfWork: isBusiness ? lineOfWork : null,
          organizationName: isBusiness ? getFormValue(form, "organizationName") : null,
          organizationCountry: isBusiness ? getFormValue(form, "organizationCountry") : null,
          reason: reason || null,
        },
      });

      setState(error ? "error" : "success");
    } catch {
      setState("error");
    }
  }

  return (
    <Card id="request-access" className="scroll-mt-24">
      <CardHeader>
        <CardTitle>Request alpha access</CardTitle>
        <CardDescription>
          A few details help us understand fit. We review every request manually.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {state === "success" ? (
          <div className="space-y-5 py-8 text-center">
            <CheckCircle2 className="text-primary mx-auto size-10" aria-hidden="true" />
            <div className="space-y-1">
              <p className="text-foreground text-base leading-snug font-semibold">
                Request received
              </p>
              <p className="text-muted-foreground text-sm">{genericSuccess}</p>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-5">
            <fieldset className="space-y-3">
              <legend className="text-foreground text-sm font-medium">
                Account type<span className="text-primary ml-0.5">*</span>
              </legend>
              <div className="grid gap-3 sm:grid-cols-2">
                {accountTypes.map(({ value, description }) => (
                  <label
                    key={value}
                    className="border-border has-checked:border-primary has-checked:bg-accent flex cursor-pointer gap-3 rounded-md border px-4 py-3 text-sm transition-colors"
                  >
                    <input
                      type="radio"
                      name="accountType"
                      value={value}
                      aria-label={value}
                      checked={accountType === value}
                      onChange={() => setAccountType(value)}
                      className="accent-primary mt-1"
                    />
                    <span>
                      <span className="text-foreground block font-medium">{value}</span>
                      <span className="text-muted-foreground block text-xs">{description}</span>
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Name" id="name" required>
                <Input id="name" name="name" required autoComplete="name" placeholder="Your name" />
              </Field>
              <Field label="Email" id="email" required>
                <Input
                  id="email"
                  name="email"
                  type="email"
                  required
                  autoComplete="email"
                  defaultValue={initialEmail}
                  placeholder="you@company.com"
                />
              </Field>
            </div>

            {accountType === "Business" && (
              <div className="grid gap-4 sm:grid-cols-2">
                <SelectField
                  label="Team size"
                  id="teamSize"
                  value={teamSize}
                  onValueChange={setTeamSize}
                  placeholder="Select team size"
                  required
                  options={teamSizeOptions}
                />
                <SelectField
                  label="Line of work"
                  id="lineOfWork"
                  value={lineOfWork}
                  onValueChange={setLineOfWork}
                  placeholder="Select line of work"
                  required
                  options={lineOfWorkOptions}
                />
                <Field label="Organization name" id="organizationName" required>
                  <Input
                    id="organizationName"
                    name="organizationName"
                    required
                    placeholder="Acme Inc."
                  />
                </Field>
                <Field label="Organization country" id="organizationCountry" required>
                  <Input
                    id="organizationCountry"
                    name="organizationCountry"
                    required
                    autoComplete="country-name"
                    placeholder="Sweden"
                  />
                </Field>
              </div>
            )}

            <Field label="Where does product intent get lost?" id="reason">
              <Textarea
                id="reason"
                name="reason"
                maxLength={1000}
                placeholder="Example: decisions live in Slack, specs drift from implementation, or agents lack reliable context."
              />
              <p className="text-muted-foreground mt-1 text-xs">
                Optional – one concrete example is enough.
              </p>
            </Field>

            {state === "error" && (
              <p className="text-destructive text-xs">
                We could not send your request. Please check the fields and try again.
              </p>
            )}

            <Button type="submit" size="lg" className="w-full" disabled={state === "loading"}>
              {state === "loading" ? "Sending…" : "Request alpha access"}
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}

const accountTypes = [
  { value: "Personal", description: "Exploring Grundet for your own work." },
  { value: "Business", description: "Using Grundet with a team or organization." },
] as const;

const teamSizeOptions = [
  ["TwoToFive", "2–5"],
  ["SixToFifteen", "6–15"],
  ["SixteenToFifty", "16–50"],
  ["FiftyPlus", "50+"],
] as const;

const lineOfWorkOptions = [
  ["ProductDevelopment", "Product development"],
  ["AgencyConsultancy", "Agency / consultancy"],
  ["NonDevelopment", "Non-development"],
  ["Other", "Other"],
] as const;

function getFormValue(form: FormData, name: string): string {
  return String(form.get(name) ?? "").trim();
}

function Field({
  label,
  id,
  required = false,
  children,
}: {
  label: string;
  id: string;
  required?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div>
      <Label htmlFor={id} className="text-foreground mb-1.5 block text-sm font-medium">
        {label}
        {required && <span className="text-primary ml-0.5">*</span>}
      </Label>
      {children}
    </div>
  );
}

function SelectField({
  label,
  id,
  value,
  onValueChange,
  placeholder,
  required = false,
  options,
}: {
  label: string;
  id: string;
  value: string | null;
  onValueChange: (value: string | null) => void;
  placeholder: string;
  required?: boolean;
  options: ReadonlyArray<readonly [string, string]>;
}) {
  const selectedLabel = options.find(([optionValue]) => optionValue === value)?.[1] ?? null;

  return (
    <Field label={label} id={id} required={required}>
      <Select value={value} onValueChange={onValueChange}>
        <SelectTrigger id={id} className="w-full">
          <SelectValue placeholder={selectedLabel ?? placeholder}>{selectedLabel}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          {options.map(([optionValue, optionLabel]) => (
            <SelectItem key={optionValue} value={optionValue}>
              {optionLabel}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {required && (
        <input tabIndex={-1} className="sr-only" required value={value ?? ""} readOnly />
      )}
    </Field>
  );
}
