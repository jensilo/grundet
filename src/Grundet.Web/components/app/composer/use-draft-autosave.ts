"use client";

import { useEffect, useRef, useState } from "react";

import { buildDraftEnvelope, parseDraftEnvelope } from "./draft-envelope";

const DEBOUNCE_MS = 1000;

export type DraftIo = {
  // Returns the opaque draft content, or null when no draft exists.
  load: () => Promise<unknown | null>;
  save: (content: unknown) => Promise<void>;
  remove: () => Promise<void>;
};

export type UseDraftAutosaveOptions<T> = {
  schemaVersion: number;
  baseChangedAt: string | null;
  io: DraftIo;
  // The serialized form, recomputed each render by the composer.
  value: T;
  // Whether the current value is worth persisting (essential field present / dirty).
  enabled: boolean;
  // Applies a restored draft to the form state.
  onRestore: (form: T) => void;
};

export type DraftAutosave = {
  // A draft was loaded and applied on open.
  restored: boolean;
  // The underlying entity changed since the draft was started (edit mode).
  stale: boolean;
  saving: boolean;
  // Remove the server draft; caller resets the form so no new draft is recreated.
  discard: () => Promise<void>;
  // Seal autosave on successful submit so a late PUT can't recreate a deleted draft.
  markSubmitted: () => void;
};

/**
 * Server-side draft autosave/restore/discard for the composer. The server draft is the
 * single source of truth (no localStorage mirror). Pending autosaves are race-guarded on
 * submit and discard via an epoch counter so a late PUT can never resurrect a removed draft.
 */
export function useDraftAutosave<T>({
  schemaVersion,
  baseChangedAt,
  io,
  value,
  enabled,
  onRestore,
}: UseDraftAutosaveOptions<T>): DraftAutosave {
  const [restored, setRestored] = useState(false);
  const [stale, setStale] = useState(false);
  const [saving, setSaving] = useState(false);
  const [ready, setReady] = useState(false);

  // Keep mutable handles to per-render values so the autosave effect can stay stable. The
  // assignments live in an effect (refs must not be written during render); the timeouts and
  // queued operations that read them only run after commit, so they always see the latest values.
  const ioRef = useRef(io);
  const onRestoreRef = useRef(onRestore);
  const valueRef = useRef(value);
  const baseChangedAtRef = useRef(baseChangedAt);
  useEffect(() => {
    ioRef.current = io;
    onRestoreRef.current = onRestore;
    valueRef.current = value;
    baseChangedAtRef.current = baseChangedAt;
  });

  const chainRef = useRef<Promise<void>>(Promise.resolve());
  const epochRef = useRef(0);
  const sealedRef = useRef(false);
  const mightHaveDraftRef = useRef(false);

  function enqueue(op: () => Promise<void>): Promise<void> {
    const next = chainRef.current.then(op).catch(() => {
      // Draft failures are non-fatal — never surface as an error toast.
    });
    chainRef.current = next;
    return next;
  }

  // Load + restore once on open.
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const content = await ioRef.current.load();
        if (cancelled) return;
        const parsed = parseDraftEnvelope<T>(content, schemaVersion, baseChangedAtRef.current);
        if (parsed.kind === "restored") {
          onRestoreRef.current(parsed.form);
          // A server draft exists, so discard/submit must clean it up even if this session
          // never autosaves.
          mightHaveDraftRef.current = true;
          setRestored(true);
          setStale(parsed.stale);
        }
        // Only enable autosave once we have successfully read the draft state (present or
        // absent). parseDraftEnvelope never throws, so reaching here means the load resolved.
        if (!cancelled) setReady(true);
      } catch {
        // The load itself failed (network/HTTP error). Leave autosave disabled (ready stays
        // false) so a later edit can't overwrite a server draft we merely failed to read.
        // Submit still works; the backend clears any existing draft inside the save transaction.
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Debounced autosave whenever the serialized value changes.
  const serialized = JSON.stringify(value);
  useEffect(() => {
    if (!ready || !enabled || sealedRef.current) return;
    const epoch = epochRef.current;
    const handle = setTimeout(() => {
      if (sealedRef.current || epoch !== epochRef.current) return;
      mightHaveDraftRef.current = true;
      setSaving(true);
      void enqueue(() =>
        ioRef.current.save(
          buildDraftEnvelope(valueRef.current, schemaVersion, baseChangedAtRef.current)
        )
      ).finally(() => setSaving(false));
    }, DEBOUNCE_MS);
    return () => clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [serialized, enabled, ready]);

  async function discard(): Promise<void> {
    epochRef.current += 1;
    setRestored(false);
    setStale(false);
    if (!mightHaveDraftRef.current) return;
    mightHaveDraftRef.current = false;
    await enqueue(() => ioRef.current.remove());
  }

  function markSubmitted(): void {
    sealedRef.current = true;
    epochRef.current += 1;
    // The backend deletes the draft inside the create/update transaction. If a save was
    // pending or in flight, chain a cleanup delete after it so it can't outlive the submit.
    if (mightHaveDraftRef.current) {
      mightHaveDraftRef.current = false;
      void enqueue(() => ioRef.current.remove());
    }
  }

  return { restored, stale, saving, discard, markSubmitted };
}
