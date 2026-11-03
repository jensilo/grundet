/**
 * Shared inner-container classes for marketing page sections.
 *
 * Single source of truth for the responsive width / padding tiers so the
 * large-screen scaling lives in one place instead of being copy-pasted across
 * sections. The base (`max-w-6xl px-6 py-24 md:py-32`) is intentionally
 * unchanged from the original so layouts at <=1280px stay pixel-identical; only
 * the 2xl (1536px+) and custom 3xl (1920px+) tiers widen the shell and add
 * vertical/horizontal rhythm for large presentation displays.
 *
 * Sections compose with `cn(MARKETING_CONTAINER, "...section extras")`. Inner
 * text blocks keep their own `max-w-xl`/`max-w-2xl` caps so line length stays
 * within the marketing reading rule even as the outer shell widens.
 */
export const MARKETING_CONTAINER =
  "mx-auto w-full max-w-6xl px-6 py-24 md:py-32 2xl:max-w-[88rem] 2xl:px-12 2xl:py-40 3xl:max-w-[96rem] 3xl:px-16 3xl:py-48";
