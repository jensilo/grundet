"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";

import { cn } from "@/lib/utils";

import { MARKETING_CONTAINER } from "./layout";

const COLS = 15;
const ROWS = 8;
const JITTER = 0.18;
const VB_W = 200;
const VB_H = 100;
const THRESHOLD = 32;
const MIN_POINT_R = 0.46;
const POINT_R_RANGE = 0.36;
const MIN_POINT_MAX_R = 1.15;
const POINT_MAX_R_RANGE = 0.55;

type Point = {
  id: number;
  x: number;
  y: number;
  baseR: number;
  maxR: number;
  opacity: number;
  response: number;
  transitionMs: number;
};
type Triangle = { id: number; d: string; cx: number; cy: number };

function round2(n: number): number {
  return Math.round(n * 100) / 100;
}

function buildMesh(): { points: Point[]; triangles: Triangle[] } {
  const points: Point[] = [];
  const cellW = VB_W / (COLS - 1);
  const cellH = VB_H / (ROWS - 1);
  const seedRand = (a: number, b: number): number => {
    const v = Math.sin(a * 137.61 + b * 53.83) * 4831.71;
    return v - Math.floor(v);
  };
  let pid = 0;
  for (let r = 0; r < ROWS; r++) {
    for (let c = 0; c < COLS; c++) {
      const isEdge = r === 0 || r === ROWS - 1 || c === 0 || c === COLS - 1;
      const jx = isEdge ? 0 : (seedRand(c, r) - 0.5) * 2 * JITTER * cellW;
      const jy = isEdge ? 0 : (seedRand(c, r + 100) - 0.5) * 2 * JITTER * cellH;
      const body = Math.pow(seedRand(c + 40, r + 73), 1.35);
      points.push({
        id: pid++,
        x: round2(c * cellW + jx),
        y: round2(r * cellH + jy),
        baseR: round2(MIN_POINT_R + body * POINT_R_RANGE),
        maxR: round2(MIN_POINT_MAX_R + body * POINT_MAX_R_RANGE),
        opacity: round2(0.54 + body * 0.34),
        response: round2(0.82 + body * 0.32),
        transitionMs: Math.round(170 + body * 120),
      });
    }
  }
  const triangles: Triangle[] = [];
  let tid = 0;
  const idx = (c: number, r: number): number => r * COLS + c;
  for (let r = 0; r < ROWS - 1; r++) {
    for (let c = 0; c < COLS - 1; c++) {
      const a = points[idx(c, r)];
      const b = points[idx(c + 1, r)];
      const cP = points[idx(c, r + 1)];
      const d = points[idx(c + 1, r + 1)];
      const altDiagonal = (r + c) % 2 === 0;
      const tris: [Point, Point, Point][] = altDiagonal
        ? [
            [a, b, d],
            [a, d, cP],
          ]
        : [
            [a, b, cP],
            [b, d, cP],
          ];
      for (const [p1, p2, p3] of tris) {
        triangles.push({
          id: tid++,
          d: `M${round2(p1.x)},${round2(p1.y)}L${round2(p2.x)},${round2(p2.y)}L${round2(p3.x)},${round2(p3.y)}Z`,
          cx: round2((p1.x + p2.x + p3.x) / 3),
          cy: round2((p1.y + p2.y + p3.y) / 3),
        });
      }
    }
  }
  triangles.sort((tA, tB) => {
    const da = Math.hypot(tA.cx - VB_W / 2, tA.cy - VB_H / 2);
    const db = Math.hypot(tB.cx - VB_W / 2, tB.cy - VB_H / 2);
    return da - db;
  });
  return { points, triangles };
}

const MESH = buildMesh();

const ANIMATION_STORAGE_KEY = "grundet-marketing-move-fast-animation";

function shouldPlayEntranceAnimation(): boolean {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return false;

  try {
    const documentMarker = String(performance.timeOrigin);
    if (sessionStorage.getItem(ANIMATION_STORAGE_KEY) === documentMarker) return false;
    sessionStorage.setItem(ANIMATION_STORAGE_KEY, documentMarker);
  } catch {
    // If sessionStorage is unavailable, prefer the first-load animation.
  }

  return true;
}

export function MoveFast() {
  const sectionRef = useRef<HTMLElement>(null);
  const [built, setBuilt] = useState(true);
  const svgRef = useRef<SVGSVGElement>(null);
  const circleMapRef = useRef<Map<number, SVGCircleElement>>(new Map());
  const rafRef = useRef<number | null>(null);
  const pendingCursorRef = useRef<{ x: number; y: number } | null>(null);
  const realCursorActiveRef = useRef(false);
  const ambientRafRef = useRef<number | null>(null);
  const ambientTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useLayoutEffect(() => {
    if (!shouldPlayEntranceAnimation()) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setBuilt(false);
  }, []);

  useEffect(() => {
    if (built) return;
    const el = sectionRef.current;
    if (!el) return;
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setBuilt(true);
          observer.disconnect();
        }
      },
      { threshold: 0.3 }
    );
    observer.observe(el);
    return () => {
      observer.disconnect();
      setBuilt(true);
    };
  }, [built]);

  // bfcache restore preserves component state but may discard pending timers.
  // Force the completed state so the section isn't frozen mid-animation.
  useEffect(() => {
    const complete = () => setBuilt(true);
    const handlePageShow = (e: PageTransitionEvent) => {
      if (e.persisted) complete();
    };
    window.addEventListener("pageshow", handlePageShow);
    return () => {
      window.removeEventListener("pageshow", handlePageShow);
    };
  }, []);

  // Use setProperty/removeProperty so the browser sees a CSS style change and
  // applies the `transition` defined in the element's inline style — setAttribute
  // sets a presentation attribute which bypasses CSS transitions entirely.
  const applyCursor = useCallback((svgX: number, svgY: number, intensity = 1) => {
    for (const p of MESH.points) {
      const dx = p.x - svgX;
      const dy = p.y - svgY;
      const dist = Math.hypot(dx, dy);
      const el = circleMapRef.current.get(p.id);
      if (!el) continue;

      if (dist < THRESHOLD) {
        // Ease-in falloff: feels soft near the edge, punchy at the centre.
        // Each point has a seeded body, so the mesh reads as weighted instead of uniform.
        const t = Math.min(1, Math.pow(1 - dist / THRESHOLD, 1.4) * intensity * p.response);
        el.style.setProperty("r", String(round2(p.baseR + (p.maxR - p.baseR) * t)));
        el.style.setProperty("fill", "var(--color-indigo-500)");
      } else {
        el.style.removeProperty("r");
        el.style.removeProperty("fill");
      }
    }
  }, []);

  const stopAmbient = useCallback(() => {
    if (ambientTimeoutRef.current !== null) {
      clearTimeout(ambientTimeoutRef.current);
      ambientTimeoutRef.current = null;
    }
    if (ambientRafRef.current !== null) {
      cancelAnimationFrame(ambientRafRef.current);
      ambientRafRef.current = null;
    }
  }, []);

  const startAmbient = useCallback(
    (delayMs = 2000) => {
      stopAmbient();
      let t = 0;
      let lastTime = 0;

      const tick = (time: number) => {
        if (lastTime !== 0 && !realCursorActiveRef.current) {
          const delta = Math.min(time - lastTime, 50);
          t += delta * 0.00022;
          const svgX = VB_W / 2 + Math.sin(t * 1.3) * VB_W * 0.3;
          const svgY = VB_H / 2 + Math.sin(t * 0.7) * VB_H * 0.28;
          applyCursor(svgX, svgY, 0.42);
        }
        lastTime = time;
        ambientRafRef.current = requestAnimationFrame(tick);
      };

      ambientTimeoutRef.current = setTimeout(() => {
        ambientTimeoutRef.current = null;
        ambientRafRef.current = requestAnimationFrame(tick);
      }, delayMs);
    },
    [applyCursor, stopAmbient]
  );

  // Slow Lissajous ambient cursor — keeps the mesh alive between interactions.
  // Pauses while the real pointer is inside so they don't fight each other.
  useEffect(() => {
    if (!built) {
      stopAmbient();
      return;
    }

    startAmbient();
    return stopAmbient;
  }, [built, startAmbient, stopAmbient]);

  useEffect(() => {
    if (!built) return;

    const restartAmbient = () => {
      if (document.visibilityState === "hidden") {
        stopAmbient();
        return;
      }
      startAmbient(0);
    };

    document.addEventListener("visibilitychange", restartAmbient);
    window.addEventListener("pageshow", restartAmbient);
    window.addEventListener("focus", restartAmbient);
    return () => {
      document.removeEventListener("visibilitychange", restartAmbient);
      window.removeEventListener("pageshow", restartAmbient);
      window.removeEventListener("focus", restartAmbient);
    };
  }, [built, startAmbient, stopAmbient]);

  const handlePointerMove = useCallback(
    (e: React.PointerEvent<HTMLElement>) => {
      const svg = svgRef.current;
      if (!svg) return;

      realCursorActiveRef.current = true;

      const rect = svg.getBoundingClientRect();
      pendingCursorRef.current = {
        x: (e.clientX - rect.left) * (VB_W / rect.width),
        y: (e.clientY - rect.top) * (VB_H / rect.height),
      };

      if (rafRef.current !== null) return;
      rafRef.current = requestAnimationFrame(() => {
        rafRef.current = null;
        const cursor = pendingCursorRef.current;
        if (cursor) applyCursor(cursor.x, cursor.y, 1);
      });
    },
    [applyCursor]
  );

  const handlePointerLeave = useCallback(() => {
    realCursorActiveRef.current = false;
    if (rafRef.current !== null) {
      cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
    }
    pendingCursorRef.current = null;
    // Ambient takes over on its next tick and transitions dots back smoothly.
    if (built) startAmbient(0);
  }, [built, startAmbient]);

  useEffect(() => {
    return () => {
      if (rafRef.current !== null) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
      pendingCursorRef.current = null;
      realCursorActiveRef.current = false;
    };
  }, []);

  return (
    <section
      ref={sectionRef}
      className="relative overflow-hidden border-b border-slate-200"
      onPointerMove={handlePointerMove}
      onPointerLeave={handlePointerLeave}
    >
      <svg
        ref={svgRef}
        viewBox={`0 0 ${VB_W} ${VB_H}`}
        preserveAspectRatio="xMidYMid slice"
        className="absolute inset-0 h-full w-full"
        aria-hidden="true"
      >
        {MESH.triangles.map((t, i) => (
          <path
            key={t.id}
            d={t.d}
            fill="none"
            stroke="var(--color-slate-300)"
            strokeWidth="1"
            vectorEffect="non-scaling-stroke"
            pathLength="1"
            strokeDasharray="1"
            className={cn("mesh-tri", built && "built")}
            style={{ animationDelay: `${i * 20}ms` }}
          />
        ))}
        {MESH.points.map((p, i) => (
          <circle
            key={p.id}
            ref={(el) => {
              if (el) circleMapRef.current.set(p.id, el);
              else circleMapRef.current.delete(p.id);
            }}
            cx={p.x}
            cy={p.y}
            r={p.baseR}
            fill="var(--color-slate-400)"
            vectorEffect="non-scaling-stroke"
            style={{
              opacity: built ? p.opacity : 0,
              // Delay only applies to opacity (entry stagger). r and fill get 0ms
              // delay so hover/ambient transitions are immediate.
              transition: `opacity 320ms var(--ease-out) ${320 + i * 6}ms, r ${p.transitionMs}ms ease-out 0ms, fill 300ms ease-out 0ms`,
            }}
          />
        ))}
      </svg>

      <div className={cn(MARKETING_CONTAINER, "relative")}>
        <div
          className="relative z-10 mx-auto max-w-2xl text-center"
          style={{
            opacity: built ? 1 : 0,
            transform: built ? "translateY(0)" : "translateY(10px)",
            transition: "opacity 500ms var(--ease-out), transform 500ms var(--ease-out)",
            transitionDelay: built ? "700ms" : "0ms",
          }}
        >
          <div className="bg-background/50 px-8 py-10">
            <h2 className="font-serif text-4xl leading-[1.05] tracking-tight text-slate-950 md:text-6xl 2xl:text-7xl">
              AI makes shipping cheaper. Intent gets more valuable.
            </h2>
            <p className="mx-auto mt-8 max-w-xl text-lg leading-relaxed text-slate-700 2xl:mt-10 2xl:text-xl">
              When implementation accelerates, outdated decisions become expensive. Grundet gives
              your team a durable place for product intent so people and agents work from the same
              current truth.
            </p>
            <p className="mx-auto mt-5 max-w-xl text-base leading-relaxed text-slate-500">
              The point is trusted context – for planning, building, reviewing, and handing work to
              agents.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
