"use client";

import {
  type PointerEvent,
  type ReactNode,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";

import { cn } from "@/lib/utils";
import { wordDiff } from "@/lib/word-diff";

const REQUIREMENT_LINE_BEFORE = "  shared with parents at the end of each term.";
const REQUIREMENT_LINE_AFTER = "  accessible to parents in real time.";
const REASON_TEXT =
  "Elin raised a parent council petition for transparency; Clara's motion for real-time access carried 11–4.";

const TIMING = {
  line1: 200,
  line2Removed: 420,
  line2Added: 640,
  line3: 860,
  line4: 1080,
  reasonStart: 1400,
  charBaseDelay: 28,
  charJitter: 20,
  pauseAfterClauseEnd: 180,
  pauseAfterSentenceEnd: 280,
  preFlashDelay: 300,
  publishPulseDuration: 500,
  liveTickMs: 5_000,
};

const TILT = {
  rotateXMax: 5,
  rotateYMax: 7,
  translateMax: 5,
  idleRotateX: -4,
  idleRotateY: -6,
  idleTranslateY: -3,
  transitionMs: 280,
  leaveTransitionMs: 500,
};

const line2Diff = wordDiff(REQUIREMENT_LINE_BEFORE, REQUIREMENT_LINE_AFTER);
const line2Removed = line2Diff.filter((t) => t.type !== "added");
const line2Added = line2Diff.filter((t) => t.type !== "removed");

type Stage = "diffing" | "typing" | "publishing" | "live";

const ANIMATION_STORAGE_KEY = "grundet-marketing-hero-diff-animation";

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

export function HeroDiffMock({ className }: { className?: string }) {
  const [visibleLines, setVisibleLines] = useState(5);
  const [reasonChars, setReasonChars] = useState(REASON_TEXT.length);
  const [stage, setStage] = useState<Stage>("live");
  const [publishedAt, setPublishedAt] = useState<number | null>(null);
  const [now, setNow] = useState<number>(0);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const cardRef = useRef<HTMLDivElement>(null);
  const shouldAnimateRef = useRef(false);
  const reducedMotionRef = useRef(false);

  useLayoutEffect(() => {
    if (!shouldPlayEntranceAnimation()) return;
    shouldAnimateRef.current = true;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setVisibleLines(0);
    setReasonChars(0);
    setPublishedAt(null);
    setStage("diffing");
    setNow(0);
  }, []);

  useEffect(() => {
    if (!shouldAnimateRef.current) return;
    const timeouts: ReturnType<typeof setTimeout>[] = [];

    timeouts.push(setTimeout(() => setVisibleLines(1), TIMING.line1));
    timeouts.push(setTimeout(() => setVisibleLines(2), TIMING.line2Removed));
    timeouts.push(setTimeout(() => setVisibleLines(3), TIMING.line2Added));
    timeouts.push(setTimeout(() => setVisibleLines(4), TIMING.line3));
    timeouts.push(setTimeout(() => setVisibleLines(5), TIMING.line4));
    timeouts.push(setTimeout(() => setStage("typing"), TIMING.reasonStart));

    return () => {
      for (const t of timeouts) clearTimeout(t);
      setVisibleLines(5);
      setReasonChars(REASON_TEXT.length);
      setPublishedAt(Date.now());
      setStage("live");
      setNow(Date.now());
    };
  }, []);

  // bfcache restore preserves component state but may discard pending timers.
  // Force the completed state so the card isn't frozen mid-animation.
  useEffect(() => {
    const complete = () => {
      setVisibleLines(5);
      setReasonChars(REASON_TEXT.length);
      setStage("live");
      setPublishedAt(Date.now());
      setNow(Date.now());
    };
    const handlePageShow = (e: PageTransitionEvent) => {
      if (e.persisted) complete();
    };
    window.addEventListener("pageshow", handlePageShow);
    return () => {
      window.removeEventListener("pageshow", handlePageShow);
    };
  }, []);

  useEffect(() => {
    if (stage !== "typing") return;
    let i = 0;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | null = null;

    const typeNext = () => {
      if (cancelled) return;
      if (i >= REASON_TEXT.length) {
        timer = setTimeout(() => {
          if (cancelled) return;
          setStage("publishing");
          timer = setTimeout(() => {
            if (cancelled) return;
            setPublishedAt(Date.now());
            setStage("live");
            setNow(Date.now());
          }, TIMING.publishPulseDuration);
        }, TIMING.preFlashDelay);
        return;
      }
      const prevCh = REASON_TEXT[i - 1];
      i++;
      setReasonChars(i);

      let delay = TIMING.charBaseDelay + (Math.random() * 2 - 1) * TIMING.charJitter;
      if (prevCh === "," || prevCh === ";") delay += TIMING.pauseAfterClauseEnd;
      else if (prevCh === ".") delay += TIMING.pauseAfterSentenceEnd;

      timer = setTimeout(typeNext, Math.max(8, delay));
    };

    typeNext();

    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [stage]);

  useEffect(() => {
    if (stage !== "live") return;
    const id = setInterval(() => setNow(Date.now()), TIMING.liveTickMs);
    return () => clearInterval(id);
  }, [stage]);

  const applyIdleTilt = useCallback(() => {
    const card = cardRef.current;
    if (!card) return;

    card.style.transitionDuration = `${TILT.leaveTransitionMs}ms`;
    card.style.transform = `translate(0px, ${TILT.idleTranslateY}px) rotateX(${TILT.idleRotateX}deg) rotateY(${TILT.idleRotateY}deg)`;
  }, []);

  useEffect(() => {
    reducedMotionRef.current = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (!reducedMotionRef.current) applyIdleTilt();
  }, [applyIdleTilt]);

  const handleTiltPointerMove = useCallback((e: PointerEvent<HTMLDivElement>) => {
    if (reducedMotionRef.current) return;

    const card = cardRef.current;
    const wrapper = wrapperRef.current;
    if (!card || !wrapper) return;

    const rect = wrapper.getBoundingClientRect();
    const cx = rect.left + rect.width / 2;
    const cy = rect.top + rect.height / 2;
    const dx = Math.max(-1, Math.min(1, (e.clientX - cx) / (rect.width / 2)));
    const dy = Math.max(-1, Math.min(1, (e.clientY - cy) / (rect.height / 2)));
    const ry = -dx * TILT.rotateYMax;
    const rx = dy * TILT.rotateXMax;
    const tx = dx * TILT.translateMax;
    const ty = dy * TILT.translateMax;
    card.style.transitionDuration = `${TILT.transitionMs}ms`;
    card.style.transform = `translate(${tx}px, ${ty}px) rotateX(${rx}deg) rotateY(${ry}deg)`;
  }, []);

  const handleTiltPointerLeave = useCallback(() => {
    if (reducedMotionRef.current) return;
    applyIdleTilt();
  }, [applyIdleTilt]);

  const published = stage === "publishing" || stage === "live";
  const liveLabel =
    stage === "live" && publishedAt !== null ? formatLive(now - publishedAt) : "just now";

  return (
    <div
      ref={wrapperRef}
      className={cn("[perspective:1400px]", className)}
      style={{ transformStyle: "preserve-3d" }}
      onPointerMove={handleTiltPointerMove}
      onPointerLeave={handleTiltPointerLeave}
    >
      <div
        ref={cardRef}
        className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl will-change-transform"
        style={{
          transitionProperty: "transform",
          transitionTimingFunction: "cubic-bezier(0.2, 0, 0, 1)",
          transitionDuration: `${TILT.leaveTransitionMs}ms`,
        }}
      >
        <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-2.5 2xl:px-5 2xl:py-3">
          <div className="flex items-center gap-2 font-mono text-xs text-slate-500">
            <span className="text-slate-400">INT-142</span>
            <span className="text-slate-300">·</span>
            <span className="inline-flex items-center gap-1">
              <span>v6 →</span>
              <span
                className={cn(
                  "inline-block transition-colors duration-300",
                  published ? "text-emerald-700" : "text-slate-500"
                )}
              >
                {published ? "v7" : "draft"}
              </span>
            </span>
          </div>
          <StatusPill stage={stage} published={published} liveLabel={liveLabel} />
        </div>

        <div className="3xl:text-base min-h-[8rem] font-mono text-[13px] leading-[1.6] 2xl:min-h-[10rem] 2xl:text-[15px]">
          <DiffRow visible={visibleLines >= 1} type="context" no={1}>
            Grade and attendance data must be
          </DiffRow>
          <DiffRow visible={visibleLines >= 2} type="removed" no={2} wordLevel>
            {line2Removed.map((t, i) =>
              t.type === "removed" ? (
                // eslint-disable-next-line react/no-array-index-key
                <span key={i} className="line-through">
                  {t.text}
                </span>
              ) : (
                // eslint-disable-next-line react/no-array-index-key
                <span key={i}>{t.text}</span>
              )
            )}
          </DiffRow>
          <DiffRow
            visible={visibleLines >= 3}
            type="added"
            no={2}
            flashOnPublish={stage === "publishing"}
          >
            {line2Added.map((t, i) => (
              // eslint-disable-next-line react/no-array-index-key
              <span key={i}>{t.text}</span>
            ))}
          </DiffRow>
          <DiffRow visible={visibleLines >= 4} type="context" no={3}>
            {""}
          </DiffRow>
          <DiffRow visible={visibleLines >= 5} type="context" no={4}>
            Acceptance: data latency does not exceed 5 minutes.
          </DiffRow>
        </div>

        <div className="min-h-[3.75rem] border-t border-slate-200 bg-slate-50 px-4 py-3 2xl:px-5 2xl:py-4">
          <div className="flex items-baseline gap-2 text-xs">
            <span className="font-mono tracking-wider text-slate-400 uppercase">Reason</span>
            <span className="text-slate-700">
              {REASON_TEXT.slice(0, reasonChars)}
              {stage === "typing" && <BlinkingCaret />}
            </span>
          </div>
          <div className="mt-1.5 flex items-center gap-2 font-mono text-[11px] text-slate-400">
            <span>elin.sandberg</span>
            <span className="text-slate-300">·</span>
            <span>2026-04-30 14:22</span>
          </div>
        </div>
      </div>
    </div>
  );
}

function formatLive(elapsedMs: number): string {
  if (elapsedMs < 60_000) return "just now";
  const m = Math.floor(elapsedMs / 60_000);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  return `${d}d ago`;
}

function StatusPill({
  stage,
  published,
  liveLabel,
}: {
  stage: Stage;
  published: boolean;
  liveLabel: string;
}) {
  return (
    <div
      className={cn(
        "flex items-center gap-1.5 font-mono text-[11px] transition-colors duration-300",
        published ? "text-emerald-700" : "text-slate-400"
      )}
    >
      <span
        className={cn(
          "size-1.5 rounded-full transition-colors duration-300",
          published ? "bg-emerald-500" : "bg-slate-300"
        )}
      />
      <span
        style={
          stage === "publishing"
            ? { animation: "publish-pulse 450ms cubic-bezier(0.2, 0, 0, 1) 1" }
            : undefined
        }
      >
        {published ? `published ${liveLabel}` : "drafting…"}
      </span>
    </div>
  );
}

function BlinkingCaret() {
  return (
    <span
      aria-hidden="true"
      className="-mb-[2px] ml-[1px] inline-block h-[1em] w-[2px] bg-slate-700 align-middle"
      style={{ animation: "caret-blink 1s steps(2, end) infinite" }}
    />
  );
}

function DiffRow({
  visible,
  type,
  no,
  children,
  wordLevel,
  flashOnPublish,
}: {
  visible: boolean;
  type: "added" | "removed" | "context";
  no: number;
  children: ReactNode;
  wordLevel?: boolean;
  flashOnPublish?: boolean;
}) {
  const sign = type === "added" ? "+" : type === "removed" ? "-" : " ";
  return (
    <div
      className={cn(
        "flex transition-all duration-500",
        visible ? "translate-y-0 opacity-100" : "translate-y-1 opacity-0"
      )}
      style={{ transitionTimingFunction: "cubic-bezier(0.2, 0, 0, 1)" }}
    >
      <span
        className={cn(
          "w-10 shrink-0 px-2 py-1 text-right text-[11px] text-slate-400 select-none",
          type === "added" && "bg-diff-added-bg",
          type === "removed" && "bg-diff-removed-bg",
          type === "context" && "bg-diff-gutter-bg"
        )}
      >
        {no}
      </span>
      <span
        className={cn(
          "w-6 shrink-0 py-1 text-center select-none",
          type === "added" && "bg-diff-added-bg text-diff-added-text",
          type === "removed" && "bg-diff-removed-bg text-diff-removed-text",
          type === "context" && "bg-diff-context-bg text-slate-400"
        )}
      >
        {sign}
      </span>
      <span
        className={cn(
          "flex-1 px-2 py-1",
          type === "added" && "bg-diff-added-bg text-diff-added-text",
          type === "removed" &&
            cn(
              "bg-diff-removed-bg text-diff-removed-text",
              !wordLevel && "line-through decoration-[1.5px]"
            ),
          type === "context" && "bg-diff-context-bg text-slate-700"
        )}
        style={
          flashOnPublish
            ? { animation: "publish-flash 700ms cubic-bezier(0.2, 0, 0, 1) both" }
            : undefined
        }
      >
        {children}
      </span>
    </div>
  );
}
