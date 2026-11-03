"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { AttachmentImage } from "@/components/app/attachment-image";
import { cn } from "@/lib/utils";

const MIN_SCALE = 1;
const MAX_SCALE = 5;
// Per-pixel wheel zoom sensitivity. The factor is exp(-deltaY * SENSITIVITY), so a mouse-wheel notch
// (~100px) nudges ~8% while a trackpad's many small deltas accumulate into a smooth, gradual zoom.
const WHEEL_SENSITIVITY = 0.0008;
const DOUBLE_CLICK_SCALE = 2.5;

// Normalize wheel deltas to pixels: Firefox reports lines (deltaMode 1) and some setups report pages
// (deltaMode 2), which would otherwise zoom far too aggressively.
function normalizeWheelDelta(event: WheelEvent): number {
  const unit = event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? 100 : 1;
  return event.deltaY * unit;
}

type Transform = { scale: number; x: number; y: number };

type Point = { x: number; y: number };

const IDENTITY: Transform = { scale: 1, x: 0, y: 0 };

function distance(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function clampScale(scale: number): number {
  return Math.max(MIN_SCALE, Math.min(MAX_SCALE, scale));
}

// Constrain pan so the scaled image can't be dragged past its own edges. At scale 1 the view is
// always centred.
function clampTransform(next: Transform, el: HTMLElement | null): Transform {
  if (!el || next.scale <= MIN_SCALE) {
    return { scale: Math.max(MIN_SCALE, next.scale), x: 0, y: 0 };
  }
  const maxX = ((next.scale - 1) * el.clientWidth) / 2;
  const maxY = ((next.scale - 1) * el.clientHeight) / 2;
  return {
    scale: next.scale,
    x: Math.max(-maxX, Math.min(maxX, next.x)),
    y: Math.max(-maxY, Math.min(maxY, next.y)),
  };
}

/**
 * Hand-rolled zoom/pan image viewer for the attachment lightbox. Wheel zooms toward the cursor,
 * drag pans when zoomed, double-click toggles zoom, and two-finger pinch scales. A lightbox library
 * would fight {@link AttachmentImage}'s blob-URL streaming, so this stays self-contained. State lives
 * in the component, so closing the dialog (which unmounts it) resets the view.
 */
export function LightboxImage({
  projectId,
  attachmentId,
  alt,
}: {
  projectId: string;
  attachmentId: string;
  alt: string;
}): React.JSX.Element {
  const viewportRef = useRef<HTMLDivElement>(null);
  const [transform, setTransform] = useState<Transform>(IDENTITY);
  // Gesture trackers live in refs so they never trigger renders mid-drag.
  const pointers = useRef<Map<number, Point>>(new Map());
  const drag = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    origX: number;
    origY: number;
  } | null>(null);
  const pinch = useRef<{ startDistance: number; startScale: number } | null>(null);
  // Render-visible flag for the active drag/pinch (refs must not be read during render): drives the
  // grab cursor and disables the ease transition so panning tracks the pointer 1:1.
  const [interacting, setInteracting] = useState(false);

  // Zoom by `factor`, keeping the point under (clientX, clientY) fixed on screen. Stable identity so
  // the non-passive wheel listener subscribes once.
  const zoomAt = useCallback((clientX: number, clientY: number, factor: number): void => {
    const el = viewportRef.current;
    if (!el) {
      return;
    }
    const rect = el.getBoundingClientRect();
    const cx = clientX - rect.left - rect.width / 2;
    const cy = clientY - rect.top - rect.height / 2;
    setTransform((prev) => {
      const scale = clampScale(prev.scale * factor);
      const ratio = scale / prev.scale;
      return clampTransform(
        { scale, x: cx - (cx - prev.x) * ratio, y: cy - (cy - prev.y) * ratio },
        el
      );
    });
  }, []);

  // Wheel must be a non-passive native listener so preventDefault stops the dialog from scrolling.
  useEffect(() => {
    const el = viewportRef.current;
    if (!el) {
      return;
    }
    function onWheel(event: WheelEvent): void {
      event.preventDefault();
      zoomAt(
        event.clientX,
        event.clientY,
        Math.exp(-normalizeWheelDelta(event) * WHEEL_SENSITIVITY)
      );
    }
    el.addEventListener("wheel", onWheel, { passive: false });
    return () => el.removeEventListener("wheel", onWheel);
  }, [zoomAt]);

  function handlePointerDown(event: React.PointerEvent): void {
    // Capture on the viewport, not event.target: capturing the <img> loses the pointer the moment the
    // browser hands the press to native image drag, which is what makes panning die after a few pixels.
    event.currentTarget.setPointerCapture?.(event.pointerId);
    pointers.current.set(event.pointerId, { x: event.clientX, y: event.clientY });

    if (pointers.current.size === 2) {
      const [a, b] = [...pointers.current.values()];
      pinch.current = { startDistance: distance(a, b), startScale: transform.scale };
      drag.current = null;
      setInteracting(true);
    } else if (transform.scale > MIN_SCALE) {
      drag.current = {
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        origX: transform.x,
        origY: transform.y,
      };
      setInteracting(true);
    }
  }

  function handlePointerMove(event: React.PointerEvent): void {
    if (pointers.current.has(event.pointerId)) {
      pointers.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
    }

    if (pinch.current && pointers.current.size === 2) {
      const [a, b] = [...pointers.current.values()];
      const scale = clampScale(
        pinch.current.startScale * (distance(a, b) / pinch.current.startDistance)
      );
      setTransform((prev) => clampTransform({ scale, x: prev.x, y: prev.y }, viewportRef.current));
      return;
    }

    const active = drag.current;
    if (active && active.pointerId === event.pointerId) {
      const dx = event.clientX - active.startX;
      const dy = event.clientY - active.startY;
      setTransform((prev) =>
        clampTransform(
          { scale: prev.scale, x: active.origX + dx, y: active.origY + dy },
          viewportRef.current
        )
      );
    }
  }

  function handlePointerUp(event: React.PointerEvent): void {
    pointers.current.delete(event.pointerId);
    if (pointers.current.size < 2) {
      pinch.current = null;
    }
    if (drag.current?.pointerId === event.pointerId) {
      drag.current = null;
    }
    if (pointers.current.size === 0) {
      setInteracting(false);
    }
  }

  function handleDoubleClick(event: React.MouseEvent): void {
    if (transform.scale > MIN_SCALE) {
      setTransform(IDENTITY);
    } else {
      zoomAt(event.clientX, event.clientY, DOUBLE_CLICK_SCALE);
    }
  }

  const isZoomed = transform.scale > MIN_SCALE;

  return (
    <div
      ref={viewportRef}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
      onDoubleClick={handleDoubleClick}
      // Cancel the browser's native image drag-and-drop so a press-and-move pans instead of grabbing
      // a ghost image and aborting the gesture.
      onDragStart={(event) => event.preventDefault()}
      className={cn(
        "relative flex max-h-[88vh] w-full touch-none items-center justify-center overflow-hidden",
        isZoomed ? (interacting ? "cursor-grabbing" : "cursor-grab") : "cursor-zoom-in"
      )}
    >
      <div
        className="flex items-center justify-center"
        style={{
          transform: `translate(${transform.x}px, ${transform.y}px) scale(${transform.scale})`,
          transition: interacting ? "none" : "transform 120ms ease-out",
        }}
      >
        <AttachmentImage
          projectId={projectId}
          attachmentId={attachmentId}
          alt={alt}
          draggable={false}
          className="max-h-[88vh] w-auto max-w-full object-contain select-none"
        />
      </div>
    </div>
  );
}
