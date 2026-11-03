# Marketing Page Animations — Design Notes

## The Problem

Next.js App Router does **not** fully unmount pages on client-side navigation. When a user clicks a link (e.g., `/docs`) and then presses the browser back button, the landing page is restored from the router's internal cache — not re-mounted from scratch.

This means:

- `useEffect` cleanup fires when the page is hidden (timers cleared, observers disconnected, DOM listeners removed)
- On back navigation, the cached React tree is shown again
- Effects with `[]` deps **do not re-run** because the component was never unmounted
- The component's React state is whatever it was when the page was hidden — often the initial "blank/hidden" animation start state
- Result: **permanently blank content**

This is not a bfcache issue (`pageshow` with `persisted=true` does not fire). It is an App Router client-side cache/Activity behaviour.

## What Does NOT Work

| Approach                                             | Why It Fails                                                                                                   |
| ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `window.scrollY > 0` check in `useState` initializer | Hydration mismatch: server renders `false`, client may read `true`                                             |
| `popstate` event listener                            | Fires before remounted components can register handlers; also fires on forward nav                             |
| Module-level `let hasPlayed = false`                 | Does not solve the core issue — the component instance may be preserved, or the cached tree is in hidden state |
| `document.visibilitychange` alone                    | Only handles tab switching, not SPA back navigation                                                            |
| Making initial state = final state                   | Works for back nav but removes first-load animations entirely                                                  |

## The Working Solution

### Core Principle

**Make the completed/final state the safe baseline.** The component always renders visible content. On a _fresh document load only_, a `useLayoutEffect` briefly rewinds to the animation start state before the first paint. On any return visit (back nav, forward nav, tab switch), the rewind is skipped — content stays visible.

### Pattern

```tsx
const ANIMATION_STORAGE_KEY = "my-component-animation";

function shouldPlayEntranceAnimation(): boolean {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches)
    return false;

  try {
    const documentMarker = String(performance.timeOrigin);
    if (sessionStorage.getItem(ANIMATION_STORAGE_KEY) === documentMarker)
      return false;
    sessionStorage.setItem(ANIMATION_STORAGE_KEY, documentMarker);
  } catch {
    // sessionStorage unavailable — prefer animation
  }

  return true;
}

export function MyAnimatedComponent() {
  // Baseline = final visible state. Safe for SSR, safe for cache restore.
  const [visible, setVisible] = useState(true);

  // On fresh document load only, rewind to hidden state before first paint.
  useLayoutEffect(() => {
    if (!shouldPlayEntranceAnimation()) return;
    setVisible(false);
  }, []);

  // Entrance animation effect — only runs when starting from hidden.
  useEffect(() => {
    if (visible) return;
    const id = setTimeout(() => setVisible(true), 500);
    return () => {
      clearTimeout(id);
      // Cleanup forces final state so cached tree is never blank.
      setVisible(true);
    };
  }, [visible]);
}
```

### Key Points

1. **`useState(true)`** — SSR-safe baseline. Server and client agree. No hydration mismatch.
2. **`useLayoutEffect` resets to `false`** — runs synchronously after DOM mutation but before paint. Only on first document load because `sessionStorage` keyed by `performance.timeOrigin` remembers this document.
3. **`performance.timeOrigin`** changes on full page reload, so animations replay after refresh.
4. **Effect cleanup forces final state** — when Next.js hides the page, cleanup runs. The `setVisible(true)` in cleanup ensures the cached tree is always in visible state.
5. **Back nav → `useLayoutEffect` skipped** (marker exists) → content stays visible.

### Pointer Interactivity

Pointer interactivity is best-effort only after App Router cached-page restore.

Event listeners attached via `addEventListener` inside `useEffect([], [])` suffer the same problem: cleanup removes them on hide, and they may not re-attach on cache restore because the component tree is shown again without a clean remount.

Use React synthetic event handlers (`onPointerMove`, `onPointerLeave`, etc.) on the JSX element for normal interaction. If they stop responding after back/forward navigation, accept that limitation for marketing polish rather than adding global document-level pointer listeners or route-cache lifecycle workarounds.

Reason: the content visibility bug is solved at the root by making final visible state the safe baseline. Pointer tracking would require a reliable “cached App Router tree became interactive again” lifecycle hook, which React/Next does not expose. Global `document.pointermove` + hit-testing can appear to work, but it leaks component concerns outside React, risks stale cached instances, and is not worth it for non-essential polish.

For RAF loops (ambient animations), use `document.visibilitychange` + `pageshow` + `window.focus` to restart the loop when the page becomes visible again.

## Components Using This Pattern

- `components/marketing/hero-diff-mock.tsx`
- `components/marketing/not-list.tsx`
- `components/marketing/move-fast.tsx`
