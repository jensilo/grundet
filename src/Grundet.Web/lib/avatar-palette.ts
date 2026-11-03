/**
 * Identicon palette for seeded user avatars (boring-avatars `beam`).
 *
 * A broader, vibrant generative palette so different users get visually distinct identicons. This
 * is a SANCTIONED EXCEPTION to the authenticated-app hue ban (docs/design/ruleset.md → Color):
 * decorative identicon avatars may draw from this wider set. The exception is scoped to identicons
 * ONLY — it does not relax the marketing-accent ban for any other product UI.
 *
 * Seven jewel-toned hues spread evenly around the wheel at consistent saturation (~55–75%) and
 * mid lightness (~50–62%). That keeps each colour distinct and harmonious (not garish) while
 * staying legible on BOTH the light (`slate-50`) and reserved dark surfaces — none sit close enough
 * to white or black to wash out on either theme.
 *
 * boring-avatars needs concrete colour strings — CSS custom properties do not resolve inside its
 * inline SVG — so these are plain hex. They are intentionally NOT design tokens: this palette is
 * decorative and deliberately outside the semantic/token system.
 */
export const AVATAR_PALETTE: string[] = [
  "#E5604C", // coral red
  "#EE9B3A", // orange
  "#E3C24A", // gold
  "#46C08D", // emerald
  "#36A8D6", // sky / teal
  "#6A5AE0", // indigo-violet
  "#CE5BA6", // magenta
];
