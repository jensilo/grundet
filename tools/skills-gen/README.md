# skills-gen

Builds the downloadable Grundet skill bundle from [`src/Grundet.Skills`](../../src/Grundet.Skills).
It is repo tooling — **not** shipped to users; the artifact it produces (`grundet-<version>.zip`)
is.

## What it does

1. Reads the single-skill manifest `src/Grundet.Skills/skills.json`.
2. Reads every file under the skill directory (`grundet/`).
3. Resolves `<!-- include: path.md -->` directives (inlining the target, relative to the skill
   dir). Files under `grundet/partials/` are include-sources only and are **not** shipped.
4. Stamps the manifest `version` into the `SKILL.md` frontmatter.
5. Validates the frontmatter against Anthropic's Agent Skills rules: `name` present, ≤64 chars,
   lowercase/hyphen only, no reserved words, matches the manifest; `description` present and
   ≤1024 chars; no angle brackets; `SKILL.md` body under 500 lines; no unresolved includes.
6. Emits a **deterministic** `dist/<name>-<version>.zip` (store mode, fixed entry timestamps,
   sorted entries) so the same input always hashes identically.

## Usage

```bash
# from repo root
make skills            # install deps + build the zip
make skills-validate   # validate only, write nothing

# or directly
cd tools/skills-gen
pnpm install
pnpm run build         # -> dist/grundet-<version>.zip  (prints sha256)
pnpm run validate      # node generate.mjs --validate-only
```

## Dependency

One dependency, [`archiver`](https://www.npmjs.com/package/archiver), for correct deterministic
ZIP output (fixed per-entry timestamps + store mode). Hand-rolling the ZIP central directory / CRC
or shelling out to system `zip` was rejected as more error-prone and less portable for reproducible
bytes.
