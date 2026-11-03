import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { DraftIo } from "./use-draft-autosave";
import { useDraftAutosave } from "./use-draft-autosave";

type Form = { text: string };

function makeIo(loadContent: unknown | null): DraftIo & {
  load: ReturnType<typeof vi.fn>;
  save: ReturnType<typeof vi.fn>;
  remove: ReturnType<typeof vi.fn>;
} {
  return {
    load: vi.fn(async () => loadContent),
    save: vi.fn(async () => undefined),
    remove: vi.fn(async () => undefined),
  };
}

describe("useDraftAutosave", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("restores a compatible draft on open", async () => {
    const io = makeIo({ schemaVersion: 1, baseChangedAt: null, form: { text: "restored" } });
    const onRestore = vi.fn();

    const { result } = renderHook(() =>
      useDraftAutosave<Form>({
        schemaVersion: 1,
        baseChangedAt: null,
        io,
        value: { text: "" },
        enabled: false,
        onRestore,
      })
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(onRestore).toHaveBeenCalledWith({ text: "restored" });
    expect(result.current.restored).toBe(true);
  });

  it("ignores a draft with an incompatible schema version", async () => {
    const io = makeIo({ schemaVersion: 99, baseChangedAt: null, form: { text: "old" } });
    const onRestore = vi.fn();

    const { result } = renderHook(() =>
      useDraftAutosave<Form>({
        schemaVersion: 1,
        baseChangedAt: null,
        io,
        value: { text: "" },
        enabled: false,
        onRestore,
      })
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(onRestore).not.toHaveBeenCalled();
    expect(result.current.restored).toBe(false);
  });

  it("debounce-saves when enabled and the value changes", async () => {
    const io = makeIo(null);

    const { rerender } = renderHook(
      ({ value, enabled }) =>
        useDraftAutosave<Form>({
          schemaVersion: 1,
          baseChangedAt: null,
          io,
          value,
          enabled,
          onRestore: () => {},
        }),
      { initialProps: { value: { text: "" }, enabled: false } }
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    rerender({ value: { text: "draft" }, enabled: true });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    expect(io.save).toHaveBeenCalledTimes(1);
    expect(io.save).toHaveBeenCalledWith({
      schemaVersion: 1,
      baseChangedAt: null,
      form: { text: "draft" },
    });
  });

  it("removes the server draft on discard when one exists", async () => {
    const io = makeIo({ schemaVersion: 1, baseChangedAt: null, form: { text: "restored" } });

    const { result } = renderHook(() =>
      useDraftAutosave<Form>({
        schemaVersion: 1,
        baseChangedAt: null,
        io,
        value: { text: "restored" },
        enabled: false,
        onRestore: () => {},
      })
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await act(async () => {
      await result.current.discard();
    });

    expect(io.remove).toHaveBeenCalledTimes(1);
    expect(result.current.restored).toBe(false);
  });

  it("race-guards a pending autosave on submit so it cannot recreate a deleted draft", async () => {
    const io = makeIo(null);

    const { result, rerender } = renderHook(
      ({ value, enabled }) =>
        useDraftAutosave<Form>({
          schemaVersion: 1,
          baseChangedAt: null,
          io,
          value,
          enabled,
          onRestore: () => {},
        }),
      { initialProps: { value: { text: "" }, enabled: false } }
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // User types — a debounced save is now pending but has not fired yet.
    rerender({ value: { text: "typed" }, enabled: true });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(500);
    });
    expect(io.save).not.toHaveBeenCalled();

    // Submit succeeds: the backend deletes the draft, so the pending save must be cancelled.
    act(() => {
      result.current.markSubmitted();
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(io.save).not.toHaveBeenCalled();
  });

  it("race-guards a pending autosave on discard so it cannot recreate the draft", async () => {
    const io = makeIo(null);

    const { result, rerender } = renderHook(
      ({ value, enabled }) =>
        useDraftAutosave<Form>({
          schemaVersion: 1,
          baseChangedAt: null,
          io,
          value,
          enabled,
          onRestore: () => {},
        }),
      { initialProps: { value: { text: "" }, enabled: false } }
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // User types — a debounced save is now pending but has not fired yet.
    rerender({ value: { text: "typed" }, enabled: true });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(500);
    });
    expect(io.save).not.toHaveBeenCalled();

    // Discard before the debounce fires: the pending save must be cancelled so it cannot
    // resurrect a draft the user just discarded.
    await act(async () => {
      await result.current.discard();
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(io.save).not.toHaveBeenCalled();
    expect(result.current.restored).toBe(false);
  });

  it("keeps autosave disabled when the initial draft load fails", async () => {
    // A failed load (network/HTTP error) is distinct from "no draft": the adapter throws so we
    // must not start autosaving, or a subsequent edit would overwrite a server draft we simply
    // failed to read.
    const io = makeIo(null);
    io.load = vi.fn(async () => {
      throw new Error("load failed");
    });

    const { rerender } = renderHook(
      ({ value, enabled }) =>
        useDraftAutosave<Form>({
          schemaVersion: 1,
          baseChangedAt: null,
          io,
          value,
          enabled,
          onRestore: () => {},
        }),
      { initialProps: { value: { text: "" }, enabled: false } }
    );

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // Even once the value is dirty and enabled, no save fires because the hook never became ready.
    rerender({ value: { text: "draft" }, enabled: true });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    expect(io.save).not.toHaveBeenCalled();
  });
});
