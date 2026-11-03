"use client";

import { useEffect, useLayoutEffect, useRef, useState } from "react";

import { MARKETING_CONTAINER } from "./layout";

const items = [
  {
    prefix: "Not",
    name: "Jira",
    because: "Coordination without process tax.",
    accent: "var(--color-espresso-500)",
    wave: "M 0 10 C 5 2, 14 2, 20 10 C 26 18, 34 17, 42 10 C 50 3, 56 4, 64 10 C 72 16, 80 18, 88 10 C 94 3, 97 5, 100 10",
    chaos: { x: -16, y: 22, r: -3, s: 1.04 },
  },
  {
    prefix: "Not",
    name: "Notion",
    because: "Product truth, not a blank canvas.",
    accent: "var(--color-cool-sky-500)",
    wave: "M 0 10 C 8 4, 13 3, 22 10 C 31 17, 37 16, 45 10 C 53 4, 60 2, 68 10 C 76 18, 82 16, 90 10 C 95 5, 98 4, 100 10",
    chaos: { x: 14, y: 16, r: 2.5, s: 1.02 },
  },
  {
    prefix: "Not a",
    name: "wiki",
    because: "Versioned, structured, searchable.",
    accent: "var(--color-emerald-500)",
    wave: "M 0 10 C 4 5, 11 2, 18 10 C 25 18, 33 17, 40 10 C 47 3, 55 4, 62 10 C 69 16, 77 18, 84 10 C 91 4, 96 3, 100 10",
    chaos: { x: -8, y: 26, r: -1.5, s: 1.03 },
  },
];

const ANIMATION_STORAGE_KEY = "grundet-marketing-not-list-animation";

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

export function NotList() {
  const sectionRef = useRef<HTMLElement>(null);
  const [triggered, setTriggered] = useState(true);

  useLayoutEffect(() => {
    if (!shouldPlayEntranceAnimation()) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setTriggered(false);
  }, []);

  useEffect(() => {
    if (triggered) return;
    const el = sectionRef.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setTriggered(true);
          observer.disconnect();
        }
      },
      { threshold: 0.4 }
    );

    observer.observe(el);
    return () => {
      observer.disconnect();
      setTriggered(true);
    };
  }, [triggered]);

  // bfcache restore preserves component state but may discard pending timers.
  // Force the completed state so items aren't frozen mid-animation.
  useEffect(() => {
    const complete = () => setTriggered(true);
    const handlePageShow = (e: PageTransitionEvent) => {
      if (e.persisted) complete();
    };
    window.addEventListener("pageshow", handlePageShow);
    return () => {
      window.removeEventListener("pageshow", handlePageShow);
    };
  }, []);

  return (
    <section ref={sectionRef} className="border-b border-slate-200">
      <div className={MARKETING_CONTAINER}>
        <div className="divide-y divide-slate-200 border-y border-slate-200">
          {items.map((item, i) => {
            // Each item: heading at t=stagger, wave at t=stagger+150, reason at t=stagger+280.
            // Next item's heading fires at stagger+500 — after this item's reason starts.
            const stagger = i * 500;
            const reasonDelay = stagger + 280;

            return (
              <div
                key={item.name}
                className="grid grid-cols-1 items-baseline gap-4 py-8 md:grid-cols-[minmax(0,1fr)_2fr] md:gap-12 md:py-10 2xl:gap-16 2xl:py-14"
              >
                <div
                  className="font-serif text-3xl tracking-tight text-slate-950 md:text-5xl 2xl:text-6xl"
                  style={{
                    transitionProperty: "transform, opacity",
                    transitionDuration: "650ms, 300ms",
                    transitionTimingFunction: "var(--ease-out)",
                    transitionDelay: `${stagger}ms`,
                    transform: triggered
                      ? "translate(0, 0) rotate(0deg) scale(1)"
                      : `translate(${item.chaos.x}px, ${item.chaos.y}px) rotate(${item.chaos.r}deg) scale(${item.chaos.s})`,
                    opacity: triggered ? 1 : 0,
                  }}
                >
                  {item.prefix}{" "}
                  <span className="relative inline-block">
                    {item.name}
                    <svg
                      className="pointer-events-none absolute inset-x-[-4%] top-[45%] h-[20%] w-[108%]"
                      viewBox="0 0 100 20"
                      preserveAspectRatio="none"
                      aria-hidden="true"
                    >
                      <path
                        d={item.wave}
                        fill="none"
                        stroke={item.accent}
                        strokeWidth="5.5"
                        strokeLinecap="round"
                        strokeDasharray="200"
                        style={{
                          strokeDashoffset: triggered ? 0 : 200,
                          transitionProperty: "stroke-dashoffset",
                          transitionDuration: "500ms",
                          transitionTimingFunction: "var(--ease-out)",
                          transitionDelay: `${stagger + 150}ms`,
                        }}
                      />
                    </svg>
                  </span>
                  .
                </div>

                <span
                  className="inline-block rounded-sm px-1.5 py-0.5 text-lg text-slate-600 md:text-2xl 2xl:text-3xl"
                  style={{
                    transitionProperty: "transform, opacity",
                    transitionDuration: "500ms, 400ms",
                    transitionTimingFunction: "var(--ease-out)",
                    transitionDelay: `${reasonDelay}ms`,
                    transform: triggered ? "translateY(0)" : "translateY(12px)",
                    opacity: triggered ? 1 : 0,
                    animation: triggered
                      ? `diff-highlight-flash 2.5s var(--ease-out) ${reasonDelay}ms both`
                      : "none",
                  }}
                >
                  {item.because}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
