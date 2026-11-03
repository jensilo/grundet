"use client";

import type { Dialog as DialogPrimitive } from "@base-ui/react/dialog";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { EmptyState } from "@/components/app/empty-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { createTeamApiKey, deleteTeamApiKey, revokeTeamApiKey } from "@/lib/api/generated/sdk.gen";
import type {
  ProblemDetails,
  TeamApiKeySummary,
  TeamMemberResponse,
} from "@/lib/api/generated/types.gen";
import { mutationErrorMessage } from "@/lib/api/mutation-error";
import { cn } from "@/lib/utils";

type CreateState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(value));
}

function tomorrowIsoDate(): string {
  const date = new Date();
  date.setDate(date.getDate() + 1);
  // Format from local date parts so the `min` guard matches the user's local
  // "tomorrow" rather than UTC's.
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

// Convert a date-only input value (YYYY-MM-DD) to an ISO string anchored at the
// END of that calendar day in the user's local timezone. A naive
// `new Date("YYYY-MM-DD").toISOString()` parses as UTC midnight, which in
// western timezones lands on the previous calendar day — expiring the key a day
// early. End-of-local-day keeps the key valid through the whole selected date
// and satisfies the backend's `expiresAt > now` check.
function expiryDateToIso(value: string): string {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day, 23, 59, 59, 999).toISOString();
}

function memberDisplayName(members: TeamMemberResponse[], userId: string): string {
  const member = members.find((m) => m.userId === userId);
  if (!member) return "Unknown";
  return member.displayName ?? member.email ?? "Unknown";
}

function getCreateErrorMessage(
  error: ProblemDetails | undefined,
  status: number | undefined
): string {
  // The rate limiter rejects the request before the API layer runs, so a 429
  // carries no ProblemDetails body — it must be special-cased on status code.
  if (status === 429) {
    return "Too many keys created recently. Wait a moment before trying again.";
  }

  return mutationErrorMessage(error, "The API key could not be created. Please try again.");
}

function StatusBadge({ apiKey }: { apiKey: TeamApiKeySummary }): React.JSX.Element {
  if (apiKey.isActive) {
    return <Badge variant="active">Active</Badge>;
  }
  if (apiKey.revokedAt) {
    return <Badge variant="destructive">Revoked</Badge>;
  }
  return <Badge variant="archived">Expired</Badge>;
}

function RevealDialog({
  rawKey,
  onDismiss,
}: {
  rawKey: string;
  onDismiss: () => void;
}): React.JSX.Element {
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  async function handleCopy(): Promise<void> {
    setCopyFailed(false);

    // Primary path: Clipboard API (requires secure context or localhost).
    if (navigator.clipboard) {
      try {
        await navigator.clipboard.writeText(rawKey);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
        return;
      } catch {
        // Fall through to execCommand fallback.
      }
    }

    // Fallback: select the input and use the deprecated execCommand, which works
    // in non-secure contexts (e.g. http://my.grundet.test).
    const input = inputRef.current;
    if (input) {
      input.select();
      const ok = document.execCommand("copy");
      if (ok) {
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
        return;
      }
    }

    // Both paths failed — surface a hint so the user knows to copy manually.
    setCopyFailed(true);
    setTimeout(() => setCopyFailed(false), 3000);
  }

  function handleOpenChange(
    open: boolean,
    eventDetails: DialogPrimitive.Root.ChangeEventDetails
  ): void {
    if (!open) {
      // Ignore Escape and outside/backdrop clicks — the raw key is shown exactly
      // once and must only be cleared by an explicit "Done" action.
      if (eventDetails.reason === "escape-key" || eventDetails.reason === "outside-press") {
        return;
      }
      onDismiss();
    }
  }

  return (
    <Dialog open={true} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Copy your API key</DialogTitle>
          <DialogDescription>
            This key is shown once. Store it now – you will not be able to see it again.
          </DialogDescription>
        </DialogHeader>

        <div className="flex items-center gap-2">
          <Input
            ref={inputRef}
            readOnly
            value={rawKey}
            aria-label="API key"
            className="font-mono text-xs"
            onFocus={(e) => e.currentTarget.select()}
          />
          <Button type="button" variant="outline" className="shrink-0" onClick={handleCopy}>
            {copied ? "Copied" : "Copy"}
          </Button>
        </div>
        {copyFailed && (
          <p className="text-muted-foreground text-xs">Press ⌘C to copy the key above.</p>
        )}

        <DialogFooter>
          <Button type="button" onClick={onDismiss}>
            Done
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ApiKeyRow({
  apiKey,
  teamId,
  members,
  isLead,
}: {
  apiKey: TeamApiKeySummary;
  teamId: string;
  members: TeamMemberResponse[];
  isLead: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [revokeDialogOpen, setRevokeDialogOpen] = useState(false);
  const [revokeState, setRevokeState] = useState<
    { type: "idle" } | { type: "revoking" } | { type: "error"; message: string }
  >({ type: "idle" });
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteState, setDeleteState] = useState<
    { type: "idle" } | { type: "deleting" } | { type: "error"; message: string }
  >({ type: "idle" });

  const isRevoking = revokeState.type === "revoking";
  const isDeleting = deleteState.type === "deleting";
  const canRevoke = isLead && apiKey.isActive;
  const canDelete = isLead && !apiKey.isActive;

  async function handleRevokeConfirm(): Promise<void> {
    setRevokeState({ type: "revoking" });

    const { error } = await revokeTeamApiKey({
      path: { teamId, keyId: apiKey.id },
    });

    if (error) {
      setRevokeState({
        type: "error",
        message: "Failed to revoke this key. Please try again.",
      });
      return;
    }

    setRevokeDialogOpen(false);
    setRevokeState({ type: "idle" });
    router.refresh();
  }

  function handleRevokeDialogChange(open: boolean): void {
    if (isRevoking) return;
    setRevokeDialogOpen(open);
    if (!open) {
      setRevokeState({ type: "idle" });
    }
  }

  async function handleDeleteConfirm(): Promise<void> {
    setDeleteState({ type: "deleting" });

    const { error, response } = await deleteTeamApiKey({
      path: { teamId, keyId: apiKey.id },
    });

    if (error) {
      const message =
        response?.status === 409
          ? "This key is still active. Revoke it first before deleting."
          : "Failed to delete this key. Please try again.";
      setDeleteState({ type: "error", message });
      return;
    }

    setDeleteDialogOpen(false);
    setDeleteState({ type: "idle" });
    router.refresh();
  }

  function handleDeleteDialogChange(open: boolean): void {
    if (isDeleting) return;
    setDeleteDialogOpen(open);
    if (!open) {
      setDeleteState({ type: "idle" });
    }
  }

  return (
    <li
      className={cn(
        "border-border flex flex-col gap-3 border-b px-4 py-3 last:border-0 sm:flex-row sm:items-start sm:justify-between",
        (isRevoking || isDeleting) && "opacity-50"
      )}
    >
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-foreground text-sm font-medium">{apiKey.name}</p>
          <code className="bg-muted text-muted-foreground rounded-sm px-1.5 py-0.5 font-mono text-xs">
            {apiKey.prefix}…
          </code>
          <StatusBadge apiKey={apiKey} />
        </div>
        <dl className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-0.5 text-xs">
          <div className="flex gap-1">
            <dt>Created by</dt>
            <dd className="text-foreground/80">
              {memberDisplayName(members, apiKey.createdByUserId)}
            </dd>
          </div>
          <div className="flex gap-1">
            <dt>Created</dt>
            <dd className="text-foreground/80">{formatDate(apiKey.createdAt)}</dd>
          </div>
          <div className="flex gap-1">
            <dt>Expires</dt>
            <dd className="text-foreground/80">
              {apiKey.expiresAt ? formatDate(apiKey.expiresAt) : "Never"}
            </dd>
          </div>
          <div className="flex gap-1">
            <dt>Last used</dt>
            <dd className="text-foreground/80">
              {apiKey.lastUsedAt ? formatDate(apiKey.lastUsedAt) : "Never"}
            </dd>
          </div>
        </dl>
        {revokeState.type === "error" && (
          <p role="alert" className="text-destructive text-xs">
            {revokeState.message}
          </p>
        )}
        {deleteState.type === "error" && (
          <p role="alert" className="text-destructive text-xs">
            {deleteState.message}
          </p>
        )}
      </div>

      {canRevoke ? (
        <div className="flex shrink-0 items-center gap-2">
          <Button
            type="button"
            variant="destructive"
            size="sm"
            disabled={isRevoking}
            onClick={() => setRevokeDialogOpen(true)}
          >
            Revoke
          </Button>
        </div>
      ) : canDelete ? (
        <div className="flex shrink-0 items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={isDeleting}
            className="text-destructive hover:text-destructive"
            onClick={() => setDeleteDialogOpen(true)}
          >
            Delete
          </Button>
        </div>
      ) : null}

      <ConfirmDialog
        open={revokeDialogOpen}
        onOpenChange={handleRevokeDialogChange}
        title={`Revoke ${apiKey.name}?`}
        description="Any MCP client using this key will immediately lose access. This cannot be undone."
        confirmLabel="Revoke key"
        pendingLabel="Revoking…"
        pending={isRevoking}
        error={revokeState.type === "error" ? revokeState.message : undefined}
        onConfirm={handleRevokeConfirm}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        onOpenChange={handleDeleteDialogChange}
        title={`Permanently delete ${apiKey.name}?`}
        description="This key will be removed from the system. This cannot be undone."
        confirmLabel="Delete key"
        pendingLabel="Deleting…"
        pending={isDeleting}
        error={deleteState.type === "error" ? deleteState.message : undefined}
        onConfirm={handleDeleteConfirm}
      />
    </li>
  );
}

function CreateApiKeyForm({
  teamId,
  onCreated,
}: {
  teamId: string;
  onCreated: (rawKey: string) => void;
}): React.JSX.Element {
  const [name, setName] = useState("");
  const [expiresAt, setExpiresAt] = useState("");
  const [createState, setCreateState] = useState<CreateState>({ type: "idle" });

  const isSubmitting = createState.type === "submitting";

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!name.trim()) return;

    setCreateState({ type: "submitting" });

    const { data, error, response } = await createTeamApiKey({
      path: { teamId },
      body: {
        name: name.trim(),
        expiresAt: expiresAt ? expiryDateToIso(expiresAt) : null,
      },
    });

    if (error || !data) {
      setCreateState({ type: "error", message: getCreateErrorMessage(error, response?.status) });
      return;
    }

    setName("");
    setExpiresAt("");
    setCreateState({ type: "idle" });
    onCreated(data.rawKey);
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="border-border bg-muted/50 space-y-4 rounded-md border p-4"
    >
      <h3 className="text-foreground text-sm font-semibold">Create an API key</h3>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="min-w-0 flex-1 space-y-1.5">
          <Label htmlFor="api-key-name">
            Name <span className="text-primary ml-0.5">*</span>
          </Label>
          <Input
            id="api-key-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Local MCP client"
            disabled={isSubmitting}
            required
          />
        </div>

        <div className="w-44 shrink-0 space-y-1.5">
          <Label htmlFor="api-key-expiry">Expires</Label>
          <Input
            id="api-key-expiry"
            type="date"
            value={expiresAt}
            min={tomorrowIsoDate()}
            onChange={(e) => setExpiresAt(e.target.value)}
            disabled={isSubmitting}
          />
        </div>

        <Button type="submit" disabled={isSubmitting} className="shrink-0 sm:self-end">
          {isSubmitting ? "Creating…" : "Create key"}
        </Button>
      </div>

      <p className="text-muted-foreground text-xs">
        Leave the expiry empty for a key that never expires. The raw key is shown once on creation.
      </p>

      {createState.type === "error" && (
        <p role="alert" className="text-destructive text-xs">
          {createState.message}
        </p>
      )}
    </form>
  );
}

export function ApiKeysPanel({
  teamId,
  initialKeys,
  members,
  isLead,
}: {
  teamId: string;
  initialKeys: TeamApiKeySummary[];
  members: TeamMemberResponse[];
  isLead: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [rawKey, setRawKey] = useState<string | null>(null);

  function handleCreated(key: string): void {
    setRawKey(key);
  }

  function handleDismissReveal(): void {
    setRawKey(null);
    router.refresh();
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>API keys</CardTitle>
        <CardDescription>
          Per-team keys for MCP access. Keys carry team scope – treat them like passwords.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <CreateApiKeyForm teamId={teamId} onCreated={handleCreated} />

        {initialKeys.length === 0 ? (
          <EmptyState
            title="No API keys yet"
            description="Create a key to connect an MCP client to this team."
          />
        ) : (
          <div className="border-border rounded-md border">
            <ul>
              {initialKeys.map((apiKey) => (
                <ApiKeyRow
                  key={apiKey.id}
                  apiKey={apiKey}
                  teamId={teamId}
                  members={members}
                  isLead={isLead}
                />
              ))}
            </ul>
          </div>
        )}
      </CardContent>

      {rawKey !== null && <RevealDialog rawKey={rawKey} onDismiss={handleDismissReveal} />}
    </Card>
  );
}
