"use client";

import * as React from "react";

import { Logo } from "@/components/brand/logo";
import { StrataMark } from "@/components/brand/strata-mark";
import { Wordmark } from "@/components/brand/wordmark";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";

// ─── Section wrapper ──────────────────────────────────────────────────────────

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-4">
      <div>
        <h2 className="text-xl font-semibold tracking-[-0.01em] text-slate-900">{title}</h2>
        {description && <p className="mt-0.5 text-sm text-slate-500">{description}</p>}
        <Separator className="mt-3" />
      </div>
      <div>{children}</div>
    </section>
  );
}

// ─── Color swatch ─────────────────────────────────────────────────────────────

function Swatch({ label, hex, bg }: { label: string; hex: string; bg: string }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="ring-foreground/8 h-12 rounded-md ring-1" style={{ backgroundColor: bg }} />
      <div className="space-y-0.5">
        <p className="text-xs font-medium text-slate-700">{label}</p>
        <p className="font-mono text-[0.8125rem] text-slate-400">{hex}</p>
      </div>
    </div>
  );
}

function SwatchGroup({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-3">
      <p className="text-sm font-medium text-slate-500">{title}</p>
      <div className="grid grid-cols-5 gap-3 sm:grid-cols-8 lg:grid-cols-11">{children}</div>
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function DesignPage() {
  const [dialogOpen, setDialogOpen] = React.useState(false);

  return (
    <TooltipProvider>
      <div className="max-w-5xl space-y-16 px-6 py-10">
        {/* ── Page header ──────────────────────────────────────────────────── */}
        <div>
          <h1 className="text-4xl font-semibold tracking-[-0.025em] text-slate-900">
            Grundet Foundation
          </h1>
          <p className="mt-1.5 text-base text-slate-500">Design system gallery — dev/test only</p>
        </div>

        {/* ── Brand ────────────────────────────────────────────────────────── */}
        <Section title="Brand">
          <div className="ring-foreground/8 space-y-8 rounded-xl bg-slate-50 p-8 ring-1">
            <div>
              <p className="mb-4 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                StrataMark
              </p>
              <div className="flex items-end gap-6">
                <div className="flex flex-col items-center gap-2">
                  <StrataMark size={16} />
                  <span className="text-xs text-slate-400">16</span>
                </div>
                <div className="flex flex-col items-center gap-2">
                  <StrataMark size={20} />
                  <span className="text-xs text-slate-400">20</span>
                </div>
                <div className="flex flex-col items-center gap-2">
                  <StrataMark size={28} />
                  <span className="text-xs text-slate-400">28</span>
                </div>
                <div className="flex flex-col items-center gap-2">
                  <StrataMark size={40} />
                  <span className="text-xs text-slate-400">40</span>
                </div>
              </div>
            </div>
            <div>
              <p className="mb-4 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Wordmark
              </p>
              <div className="flex items-end gap-8">
                <div className="flex flex-col gap-2">
                  <Wordmark size="sm" />
                  <span className="text-xs text-slate-400">sm</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Wordmark size="md" />
                  <span className="text-xs text-slate-400">md</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Wordmark size="lg" />
                  <span className="text-xs text-slate-400">lg</span>
                </div>
              </div>
            </div>
            <div>
              <p className="mb-4 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Logo
              </p>
              <div className="flex flex-wrap items-end gap-8">
                <div className="flex flex-col gap-2">
                  <Logo size="sm" />
                  <span className="text-xs text-slate-400">sm</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Logo size="md" />
                  <span className="text-xs text-slate-400">md</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Logo size="lg" />
                  <span className="text-xs text-slate-400">lg</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Logo size="sm" markOnly />
                  <span className="text-xs text-slate-400">markOnly sm</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Logo size="md" markOnly />
                  <span className="text-xs text-slate-400">markOnly md</span>
                </div>
                <div className="flex flex-col gap-2">
                  <Logo size="lg" markOnly />
                  <span className="text-xs text-slate-400">markOnly lg</span>
                </div>
              </div>
            </div>
          </div>
        </Section>

        {/* ── Color Palette ────────────────────────────────────────────────── */}
        <Section
          title="Color Palette"
          description="Foundation color tokens. Use inline styles for swatch backgrounds — arbitrary values are not generated."
        >
          <div className="space-y-8">
            <SwatchGroup title="Slate — cool-tinted neutral spine (~95% of surfaces)">
              <Swatch label="slate-50" hex="#fafbfc" bg="rgb(250 251 252)" />
              <Swatch label="slate-100" hex="#f2f4f7" bg="rgb(242 244 247)" />
              <Swatch label="slate-200" hex="#e5e8ed" bg="rgb(229 232 237)" />
              <Swatch label="slate-300" hex="#cfd4dc" bg="rgb(207 212 220)" />
              <Swatch label="slate-400" hex="#9aa1ad" bg="rgb(154 161 173)" />
              <Swatch label="slate-500" hex="#6b7280" bg="rgb(107 114 128)" />
              <Swatch label="slate-600" hex="#4b5563" bg="rgb(75 85 99)" />
              <Swatch label="slate-700" hex="#303744" bg="rgb(48 55 68)" />
              <Swatch label="slate-800" hex="#1a1f2b" bg="rgb(26 31 43)" />
              <Swatch label="slate-900" hex="#0e1117" bg="rgb(14 17 23)" />
              <Swatch label="slate-950" hex="#06080c" bg="rgb(6 8 12)" />
            </SwatchGroup>

            <div className="space-y-3">
              <p className="text-sm font-medium text-slate-500">
                Indigo — primary brand color (#610F7F)
              </p>
              <p className="-mt-1.5 text-xs text-slate-400">
                Confident purple, deliberate, used decisively — not as decorative gradients (except
                the brand Indigo × Bubblegum moment).
              </p>
              <div className="grid grid-cols-5 gap-3 sm:grid-cols-8 lg:grid-cols-11">
                <Swatch label="indigo-50" hex="#fbf5fc" bg="rgb(251 245 252)" />
                <Swatch label="indigo-100" hex="#f4e5f7" bg="rgb(244 229 247)" />
                <Swatch label="indigo-200" hex="#e5c6ec" bg="rgb(229 198 236)" />
                <Swatch label="indigo-300" hex="#cb94d6" bg="rgb(203 148 214)" />
                <Swatch label="indigo-400" hex="#a75bb7" bg="rgb(167 91 183)" />
                <Swatch label="indigo-500" hex="#610f7f" bg="rgb(97 15 127)" />
                <Swatch label="indigo-600" hex="#530b6e" bg="rgb(83 11 110)" />
                <Swatch label="indigo-700" hex="#43085a" bg="rgb(67 8 90)" />
                <Swatch label="indigo-800" hex="#320744" bg="rgb(50 7 68)" />
                <Swatch label="indigo-900" hex="#21052d" bg="rgb(33 5 45)" />
                <Swatch label="indigo-950" hex="#14031c" bg="rgb(20 3 28)" />
              </div>
            </div>

            <SwatchGroup title="Functional">
              <Swatch label="success-50" hex="#e6f8ef" bg="rgb(230 248 239)" />
              <Swatch label="success-500" hex="#16a34a" bg="rgb(22 163 74)" />
              <Swatch label="success-700" hex="#0f7a37" bg="rgb(15 122 55)" />
              <Swatch label="warning-50" hex="#fef6e1" bg="rgb(254 246 225)" />
              <Swatch label="warning-500" hex="#d97706" bg="rgb(217 119 6)" />
              <Swatch label="warning-700" hex="#92520a" bg="rgb(146 82 10)" />
              <Swatch label="destr-50" hex="#fde9e9" bg="rgb(253 233 233)" />
              <Swatch label="destr-500" hex="#dc2626" bg="rgb(220 38 38)" />
              <Swatch label="destr-700" hex="#a41c1c" bg="rgb(164 28 28)" />
            </SwatchGroup>

            <SwatchGroup title="Diff tokens">
              <Swatch label="added-bg" hex="#e6f8ef" bg="rgb(230 248 239)" />
              <Swatch label="added-text" hex="#0f4f26" bg="rgb(15 79 38)" />
              <Swatch label="removed-bg" hex="#fde9e9" bg="rgb(253 233 233)" />
              <Swatch label="removed-text" hex="#5f1414" bg="rgb(95 20 20)" />
              <Swatch label="context-bg" hex="#fafbfc" bg="rgb(250 251 252)" />
              <Swatch label="gutter-bg" hex="#f2f4f7" bg="rgb(242 244 247)" />
            </SwatchGroup>

            <div className="space-y-3">
              <p className="text-sm font-medium text-slate-500">
                Marketing accents{" "}
                <span className="text-warning-700 font-normal">
                  — marketing surfaces only, never in product UI
                </span>
              </p>
              <div className="grid grid-cols-2 gap-6 sm:grid-cols-4">
                <div className="space-y-2">
                  <p className="text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                    Cool Sky
                  </p>
                  <div className="grid grid-cols-4 gap-2">
                    <Swatch label="50" hex="#e8f3fc" bg="rgb(232 243 252)" />
                    <Swatch label="100" hex="#c8e2f7" bg="rgb(200 226 247)" />
                    <Swatch label="500" hex="#5da9e9" bg="rgb(93 169 233)" />
                    <Swatch label="700" hex="#1f6fb0" bg="rgb(31 111 176)" />
                  </div>
                </div>
                <div className="space-y-2">
                  <p className="text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                    Bubblegum
                  </p>
                  <div className="grid grid-cols-4 gap-2">
                    <Swatch label="50" hex="#ffe9ec" bg="rgb(255 233 236)" />
                    <Swatch label="100" hex="#ffcad0" bg="rgb(255 202 208)" />
                    <Swatch label="500" hex="#ff6978" bg="rgb(255 105 120)" />
                    <Swatch label="700" hex="#c2293a" bg="rgb(194 41 58)" />
                  </div>
                </div>
                <div className="space-y-2">
                  <p className="text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                    Emerald
                  </p>
                  <div className="grid grid-cols-4 gap-2">
                    <Swatch label="50" hex="#e0faf1" bg="rgb(224 250 241)" />
                    <Swatch label="100" hex="#b6f2dd" bg="rgb(182 242 221)" />
                    <Swatch label="500" hex="#06d6a0" bg="rgb(6 214 160)" />
                    <Swatch label="700" hex="#028a66" bg="rgb(2 138 102)" />
                  </div>
                </div>
                <div className="space-y-2">
                  <p className="text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                    Espresso
                  </p>
                  <div className="grid grid-cols-4 gap-2">
                    <Swatch label="50" hex="#f4e8e5" bg="rgb(244 232 229)" />
                    <Swatch label="100" hex="#e2c5bd" bg="rgb(226 197 189)" />
                    <Swatch label="500" hex="#551b14" bg="rgb(85 27 20)" />
                    <Swatch label="700" hex="#3a120c" bg="rgb(58 18 12)" />
                  </div>
                </div>
              </div>
            </div>

            <div className="space-y-3">
              <p className="text-sm font-medium text-slate-500">Brand gradient</p>
              <div
                className="ring-foreground/8 flex h-40 items-end rounded-xl p-5 ring-1"
                style={{
                  backgroundImage:
                    "linear-gradient(135deg, var(--color-indigo-500), var(--color-bubblegum-500))",
                }}
              >
                <span className="text-sm font-medium text-white drop-shadow-sm">
                  Brand gradient — sparingly, marketing only.
                </span>
              </div>
            </div>
          </div>
        </Section>

        {/* ── Typography ───────────────────────────────────────────────────── */}
        <Section
          title="Typography"
          description="Foundation type scale. Inter is the primary sans font. Heading-md and above carry negative tracking."
        >
          <div className="space-y-6">
            {[
              {
                token: "display-2xl",
                cls: "text-[5rem] leading-[1.0] font-semibold tracking-[-0.04em]",
                sample: "Grundet",
              },
              {
                token: "display-xl",
                cls: "text-[4rem] leading-[1.05] font-semibold tracking-[-0.035em]",
                sample: "Grundet",
              },
              {
                token: "display-lg",
                cls: "text-5xl leading-[1.1] font-semibold tracking-[-0.03em]",
                sample: "Grundet",
              },
              {
                token: "display-md",
                cls: "text-4xl leading-[1.15] font-semibold tracking-tight",
                sample: "The quick brown fox",
              },
              {
                token: "heading-xl",
                cls: "text-3xl leading-[1.2] font-semibold tracking-[-0.02em]",
                sample: "The quick brown fox",
              },
              {
                token: "heading-lg",
                cls: "text-2xl leading-[1.3] font-semibold tracking-[-0.015em]",
                sample: "The quick brown fox jumps",
              },
              {
                token: "heading-md",
                cls: "text-xl leading-[1.35] font-semibold tracking-[-0.01em]",
                sample: "The quick brown fox jumps over",
              },
              {
                token: "heading-sm",
                cls: "text-base leading-[1.4] font-semibold",
                sample: "The quick brown fox jumps over the lazy dog",
              },
              {
                token: "body-lg",
                cls: "text-lg leading-relaxed",
                sample: "The quick brown fox jumps over the lazy dog",
              },
              {
                token: "body-md",
                cls: "text-base leading-[1.55]",
                sample: "The quick brown fox jumps over the lazy dog",
              },
              {
                token: "body-sm",
                cls: "text-sm leading-normal",
                sample: "The quick brown fox jumps over the lazy dog",
              },
              {
                token: "body-xs",
                cls: "text-[0.8125rem] leading-[1.45]",
                sample: "The quick brown fox jumps over the lazy dog",
              },
              { token: "label-md", cls: "text-sm font-medium", sample: "Form label" },
              {
                token: "label-sm",
                cls: "text-xs font-medium tracking-[0.04em] uppercase",
                sample: "Section label",
              },
              {
                token: "code-md",
                cls: "font-mono text-sm leading-[1.55]",
                sample: "const req = await fetchRequirement(id);",
              },
              {
                token: "code-sm",
                cls: "font-mono text-[0.8125rem] leading-normal",
                sample: "const req = await fetchRequirement(id);",
              },
            ].map(({ token, cls, sample }) => (
              <div key={token} className="flex items-baseline gap-4">
                <span className="w-28 shrink-0 font-mono text-[0.8125rem] text-slate-400">
                  {token}
                </span>
                <span className={cls}>{sample}</span>
              </div>
            ))}
            <div className="flex items-baseline gap-4 border-t border-slate-100 pt-2">
              <span className="w-28 shrink-0 font-mono text-[0.8125rem] text-slate-400">
                display-serif
              </span>
              <span className="font-serif text-[4rem] leading-[1.05]">
                Requirements your team can trust.
              </span>
              <span className="text-warning-700 ml-3 shrink-0 self-center text-xs font-medium">
                Marketing only — never in product UI
              </span>
            </div>
          </div>
        </Section>

        {/* ── Radius & Shadow ──────────────────────────────────────────────── */}
        <Section title="Radius & Shadow">
          <div className="space-y-8">
            <div>
              <p className="mb-4 text-sm font-medium text-slate-500">Radius tokens</p>
              <div className="flex flex-wrap items-end gap-6">
                {[
                  { label: "rounded-sm", cls: "rounded-sm", px: "4px" },
                  { label: "rounded-md", cls: "rounded-md", px: "6px (default)" },
                  { label: "rounded-lg", cls: "rounded-lg", px: "8px" },
                  { label: "rounded-xl", cls: "rounded-xl", px: "12px" },
                ].map(({ label, cls, px }) => (
                  <div key={label} className="flex flex-col items-center gap-2">
                    <div className={`h-16 w-16 bg-indigo-100 ring-1 ring-indigo-300 ${cls}`} />
                    <p className="font-mono text-xs text-slate-500">{label}</p>
                    <p className="text-xs text-slate-400">{px}</p>
                  </div>
                ))}
                <div className="flex flex-col items-center gap-2">
                  <div className="flex h-16 w-16 items-center justify-center rounded-full bg-slate-100 ring-1 ring-slate-300">
                    <span className="text-xs text-slate-400">avatar</span>
                  </div>
                  <p className="font-mono text-xs text-slate-500">rounded-full</p>
                  <p className="text-xs text-slate-400">avatars only</p>
                </div>
              </div>
            </div>
            <div>
              <p className="mb-4 text-sm font-medium text-slate-500">Shadow tokens</p>
              <div className="flex flex-wrap items-end gap-6 rounded-xl bg-slate-100 p-6">
                {[
                  { label: "shadow-sm", cls: "shadow-sm" },
                  { label: "shadow-md", cls: "shadow-md" },
                  { label: "shadow-lg", cls: "shadow-lg" },
                  { label: "shadow-xl", cls: "shadow-xl" },
                ].map(({ label, cls }) => (
                  <div key={label} className="flex flex-col items-center gap-3">
                    <div className={`h-20 w-20 rounded-lg bg-white ${cls}`} />
                    <p className="font-mono text-xs text-slate-500">{label}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </Section>

        {/* ── Button ───────────────────────────────────────────────────────── */}
        <Section title="Button">
          <div className="space-y-5">
            <div>
              <p className="mb-3 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Variants
              </p>
              <div className="flex flex-wrap items-center gap-3">
                <Button variant="default">Default</Button>
                <Button variant="outline">Outline</Button>
                <Button variant="secondary">Secondary</Button>
                <Button variant="ghost">Ghost</Button>
                <Button variant="destructive">Destructive</Button>
                <Button variant="link">Link</Button>
                <Button variant="default" disabled>
                  Disabled
                </Button>
              </div>
            </div>
            <div>
              <p className="mb-3 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Sizes
              </p>
              <div className="flex flex-wrap items-center gap-3">
                <Button size="xs">Extra Small</Button>
                <Button size="sm">Small</Button>
                <Button size="default">Default</Button>
                <Button size="lg">Large</Button>
              </div>
            </div>
            <div>
              <p className="mb-3 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Icon sizes
              </p>
              <div className="flex flex-wrap items-center gap-3">
                <Button size="icon-xs" variant="outline" aria-label="icon xs">
                  <svg viewBox="0 0 16 16" fill="currentColor">
                    <circle cx="8" cy="8" r="3" />
                  </svg>
                </Button>
                <Button size="icon-sm" variant="outline" aria-label="icon sm">
                  <svg viewBox="0 0 16 16" fill="currentColor">
                    <circle cx="8" cy="8" r="3" />
                  </svg>
                </Button>
                <Button size="icon" variant="outline" aria-label="icon default">
                  <svg viewBox="0 0 16 16" fill="currentColor">
                    <circle cx="8" cy="8" r="3" />
                  </svg>
                </Button>
                <Button size="icon-lg" variant="outline" aria-label="icon lg">
                  <svg viewBox="0 0 16 16" fill="currentColor">
                    <circle cx="8" cy="8" r="3" />
                  </svg>
                </Button>
              </div>
            </div>
          </div>
        </Section>

        {/* ── Badge ────────────────────────────────────────────────────────── */}
        <Section title="Badge">
          <div className="space-y-4">
            <div>
              <p className="mb-2 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Generic
              </p>
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="default">Default</Badge>
                <Badge variant="secondary">Secondary</Badge>
                <Badge variant="destructive">Destructive</Badge>
                <Badge variant="outline">Outline</Badge>
                <Badge variant="ghost">Ghost</Badge>
              </div>
            </div>
            <div>
              <p className="mb-2 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                Requirement status
              </p>
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="draft">Draft</Badge>
                <Badge variant="active">Active</Badge>
                <Badge variant="archived">Archived</Badge>
                <Badge variant="wontDo">Won&apos;t Do</Badge>
              </div>
            </div>
            <div>
              <p className="mb-2 text-xs font-medium tracking-[0.04em] text-slate-400 uppercase">
                MoSCoW priority
              </p>
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="must">Must</Badge>
                <Badge variant="should">Should</Badge>
                <Badge variant="could">Could</Badge>
                <Badge variant="wont">Won&apos;t</Badge>
              </div>
            </div>
          </div>
        </Section>

        {/* ── Input + Label ────────────────────────────────────────────────── */}
        <Section title="Input + Label">
          <div className="grid max-w-2xl grid-cols-1 gap-6 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="input-normal">Requirement text</Label>
              <Input id="input-normal" placeholder="Describe the requirement…" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="input-disabled">Disabled</Label>
              <Input id="input-disabled" placeholder="Read only" disabled />
            </div>
            <div className="space-y-2">
              <Label htmlFor="input-error">With error</Label>
              <Input id="input-error" defaultValue="bad input" aria-invalid />
            </div>
          </div>
        </Section>

        {/* ── Textarea ─────────────────────────────────────────────────────── */}
        <Section title="Textarea">
          <div className="grid max-w-xl grid-cols-1 gap-6 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="ta-normal">Acceptance criterion</Label>
              <Textarea id="ta-normal" placeholder="Given… When… Then…" rows={3} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ta-disabled">Disabled</Label>
              <Textarea id="ta-disabled" placeholder="Read only" rows={3} disabled />
            </div>
          </div>
        </Section>

        {/* ── Select ───────────────────────────────────────────────────────── */}
        <Section title="Select">
          <div className="flex flex-wrap items-start gap-6">
            <div className="space-y-2">
              <Label>Priority</Label>
              <Select defaultValue="must">
                <SelectTrigger className="w-40">
                  <SelectValue placeholder="Select priority" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="must">Must</SelectItem>
                  <SelectItem value="should">Should</SelectItem>
                  <SelectItem value="could">Could</SelectItem>
                  <SelectItem value="wont">Won&apos;t</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              <Select>
                <SelectTrigger className="w-40">
                  <SelectValue placeholder="Filter by status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="active">Active</SelectItem>
                  <SelectItem value="draft">Draft</SelectItem>
                  <SelectItem value="archived">Archived</SelectItem>
                  <SelectItem value="wontDo">Won&apos;t Do</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </Section>

        {/* ── Card ─────────────────────────────────────────────────────────── */}
        <Section
          title="Card"
          description="rounded-xl + ring-1 ring-foreground/10 — not a standard border."
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="font-mono text-sm text-slate-500">IN-001</CardTitle>
                <CardDescription className="text-base font-medium text-slate-900">
                  Users can create a project
                </CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-slate-500">
                  A team lead must be able to create a new project from the dashboard. Projects are
                  scoped to a team.
                </p>
              </CardContent>
              <CardFooter className="gap-2">
                <Badge variant="active">Active</Badge>
                <Badge variant="must">Must</Badge>
              </CardFooter>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>Card with action</CardTitle>
                <CardDescription>Header action slot via CardAction</CardDescription>
                <CardAction>
                  <Button variant="outline" size="sm">
                    Edit
                  </Button>
                </CardAction>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-slate-500">
                  The CardAction renders in the top-right of the header grid.
                </p>
              </CardContent>
              <CardFooter className="justify-end gap-2">
                <Button variant="outline" size="sm">
                  Cancel
                </Button>
                <Button size="sm">Save</Button>
              </CardFooter>
            </Card>
          </div>
        </Section>

        {/* ── Table ────────────────────────────────────────────────────────── */}
        <Section title="Table">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-28">ID</TableHead>
                <TableHead>Requirement</TableHead>
                <TableHead className="w-28">Status</TableHead>
                <TableHead className="w-24">Priority</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell className="font-mono text-xs text-slate-400">IN-001</TableCell>
                <TableCell>Users can create a project</TableCell>
                <TableCell>
                  <Badge variant="active">Active</Badge>
                </TableCell>
                <TableCell>
                  <Badge variant="must">Must</Badge>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell className="font-mono text-xs text-slate-400">IN-002</TableCell>
                <TableCell>Users can invite team members via email</TableCell>
                <TableCell>
                  <Badge variant="draft">Draft</Badge>
                </TableCell>
                <TableCell>
                  <Badge variant="should">Should</Badge>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell className="font-mono text-xs text-slate-400">IN-003</TableCell>
                <TableCell>Requirements have an immutable version history</TableCell>
                <TableCell>
                  <Badge variant="active">Active</Badge>
                </TableCell>
                <TableCell>
                  <Badge variant="must">Must</Badge>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell className="font-mono text-xs text-slate-400">IN-004</TableCell>
                <TableCell>Labels can be applied to requirements</TableCell>
                <TableCell>
                  <Badge variant="archived">Archived</Badge>
                </TableCell>
                <TableCell>
                  <Badge variant="could">Could</Badge>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </Section>

        {/* ── Tabs ─────────────────────────────────────────────────────────── */}
        <Section
          title="Tabs"
          description='variant="line" matches Foundation spec (underline indicator).'
        >
          <Tabs defaultValue="requirements" className="max-w-xl">
            <TabsList variant="line">
              <TabsTrigger value="requirements">Requirements</TabsTrigger>
              <TabsTrigger value="work-items">Work Items</TabsTrigger>
              <TabsTrigger value="history">History</TabsTrigger>
            </TabsList>
            <TabsContent value="requirements" className="pt-4">
              <p className="text-sm text-slate-500">
                Requirements tab — list of atomic requirements for this feature.
              </p>
            </TabsContent>
            <TabsContent value="work-items" className="pt-4">
              <p className="text-sm text-slate-500">
                Work Items tab — linked Jira-style tasks and bugs.
              </p>
            </TabsContent>
            <TabsContent value="history" className="pt-4">
              <p className="text-sm text-slate-500">History tab — immutable version changelog.</p>
            </TabsContent>
          </Tabs>
        </Section>

        {/* ── Dialog ───────────────────────────────────────────────────────── */}
        <Section title="Dialog">
          <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
            <DialogTrigger render={<Button variant="outline">Archive requirement…</Button>} />
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Archive requirement?</DialogTitle>
                <DialogDescription>
                  This sets the status to Archived. A new version row will be inserted — the change
                  is permanent. Provide an optional reason.
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-2 py-2">
                <Label htmlFor="archive-reason">Reason (optional)</Label>
                <Textarea id="archive-reason" placeholder="Why is this being archived?" rows={2} />
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setDialogOpen(false)}>
                  Cancel
                </Button>
                <Button onClick={() => setDialogOpen(false)}>Archive</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </Section>

        {/* ── DropdownMenu ─────────────────────────────────────────────────── */}
        <Section title="DropdownMenu">
          <DropdownMenu>
            <DropdownMenuTrigger
              render={<Button variant="outline">Requirement actions ▾</Button>}
            />
            <DropdownMenuContent>
              <DropdownMenuLabel>IN-001</DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem>Edit</DropdownMenuItem>
              <DropdownMenuItem>Duplicate</DropdownMenuItem>
              <DropdownMenuItem>Archive</DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem className="text-destructive">Delete</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </Section>

        {/* ── Avatar ───────────────────────────────────────────────────────── */}
        <Section
          title="Avatar"
          description="Sizes: sm (24px), default (32px), lg (40px). rounded-full — avatars only."
        >
          <div className="flex flex-wrap items-center gap-6">
            <div className="flex flex-col items-center gap-2">
              <Avatar size="sm">
                <AvatarImage src="https://github.com/shadcn.png" alt="shadcn" />
                <AvatarFallback>SC</AvatarFallback>
              </Avatar>
              <span className="text-xs text-slate-400">sm / image</span>
            </div>
            <div className="flex flex-col items-center gap-2">
              <Avatar size="default">
                <AvatarImage src="https://github.com/shadcn.png" alt="shadcn" />
                <AvatarFallback>SC</AvatarFallback>
              </Avatar>
              <span className="text-xs text-slate-400">default / image</span>
            </div>
            <div className="flex flex-col items-center gap-2">
              <Avatar size="lg">
                <AvatarImage src="https://github.com/shadcn.png" alt="shadcn" />
                <AvatarFallback>SC</AvatarFallback>
              </Avatar>
              <span className="text-xs text-slate-400">lg / image</span>
            </div>
            <div className="flex flex-col items-center gap-2">
              <Avatar size="sm">
                <AvatarFallback>JH</AvatarFallback>
              </Avatar>
              <span className="text-xs text-slate-400">sm / initials</span>
            </div>
            <div className="flex flex-col items-center gap-2">
              <Avatar size="default">
                <AvatarFallback>JH</AvatarFallback>
              </Avatar>
              <span className="text-xs text-slate-400">default / initials</span>
            </div>
            <div className="flex flex-col items-center gap-2">
              <Avatar size="lg">
                <AvatarFallback>JH</AvatarFallback>
              </Avatar>
              <span className="text-xs text-slate-400">lg / initials</span>
            </div>
          </div>
        </Section>

        {/* ── Tooltip ──────────────────────────────────────────────────────── */}
        <Section title="Tooltip">
          <div className="flex flex-wrap items-center gap-6">
            <Tooltip>
              <TooltipTrigger
                render={
                  <Button variant="outline" size="sm">
                    Archive
                  </Button>
                }
              />
              <TooltipContent>
                <p>Set status to Archived and insert a new version row</p>
              </TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger
                render={
                  <span>
                    <Badge variant="active">Active</Badge>
                  </span>
                }
              />
              <TooltipContent>
                <p>Requirement is in scope and actively developed</p>
              </TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger
                render={
                  <span className="cursor-default font-mono text-sm text-slate-500">IN-003</span>
                }
              />
              <TooltipContent>
                <p>Requirements have an immutable version history</p>
              </TooltipContent>
            </Tooltip>
          </div>
        </Section>

        {/* ── Separator ────────────────────────────────────────────────────── */}
        <Section title="Separator">
          <div className="space-y-6">
            <div className="max-w-xs space-y-2">
              <p className="text-sm text-slate-600">Above</p>
              <Separator />
              <p className="text-sm text-slate-600">Below</p>
            </div>
            <div className="flex h-6 items-center gap-4">
              <span className="text-sm text-slate-600">Left</span>
              <Separator orientation="vertical" />
              <span className="text-sm text-slate-600">Right</span>
            </div>
          </div>
        </Section>

        {/* ── Diff View ────────────────────────────────────────────────────── */}
        <Section
          title="Diff View"
          description="The brand component — inline implementation, no external import."
        >
          <div className="ring-foreground/10 overflow-hidden rounded-xl text-sm ring-1">
            {/* Header */}
            <div className="flex items-center gap-3 border-b border-slate-200 bg-slate-50 px-4 py-3">
              <Avatar size="sm">
                <AvatarFallback>JH</AvatarFallback>
              </Avatar>
              <div className="min-w-0 flex-1">
                <span className="font-medium text-slate-800">Alex Morgan</span>
                <span className="ml-2 text-xs text-slate-400">updated IN-007 · 2 hours ago</span>
              </div>
              <Badge variant="active">Active</Badge>
            </div>
            {/* Reason row */}
            <div className="border-b border-slate-200 bg-slate-50 px-4 py-2 text-xs text-slate-500 italic">
              Reason: added SMS as an alternative reset channel
            </div>
            {/* Diff lines */}
            <div className="font-mono text-[0.8125rem] leading-[1.5]">
              <div className="flex">
                <span className="bg-diff-gutter-bg w-12 shrink-0 border-r border-slate-200 px-3 py-1.5 text-right text-slate-400 select-none">
                  1
                </span>
                <span className="bg-diff-removed-bg text-diff-removed-text flex-1 px-4 py-1.5 line-through">
                  User can reset password via email
                </span>
              </div>
              <div className="flex">
                <span className="bg-diff-gutter-bg w-12 shrink-0 border-r border-slate-200 px-3 py-1.5 text-right text-slate-400 select-none">
                  1
                </span>
                <span className="bg-diff-added-bg text-diff-added-text flex-1 px-4 py-1.5">
                  User can reset password via email or SMS
                </span>
              </div>
            </div>
          </div>
        </Section>
      </div>
    </TooltipProvider>
  );
}
