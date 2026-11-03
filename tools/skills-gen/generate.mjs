#!/usr/bin/env node
// Builds the Grundet skill bundle from src/Grundet.Skills.
//
// It reads the single-skill manifest (skills.json), resolves any
// `<!-- include: file.md -->` directives, stamps the manifest version into the
// SKILL.md frontmatter, validates the frontmatter against Anthropic's Agent
// Skills rules, and emits a DETERMINISTIC dist/<name>-<version>.zip containing
// the skill folder. Determinism (store mode, fixed entry timestamps, sorted
// entries) keeps CI hashes stable across runs.
//
// Usage:
//   node generate.mjs                 build + validate, write the zip
//   node generate.mjs --validate-only validate only, write nothing

import { ZipArchive } from "archiver";
import { createHash } from "node:crypto";
import { createWriteStream } from "node:fs";
import { mkdir, readFile, readdir, rm, stat } from "node:fs/promises";
import { dirname, join, posix, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SKILLS_ROOT = join(HERE, "..", "..", "src", "Grundet.Skills");
const DIST_DIR = join(HERE, "dist");

// A fixed timestamp for every zip entry so identical inputs hash identically.
// (Unix epoch 2000-01-01; DOS/zip cannot represent dates before 1980.)
const FIXED_MTIME = new Date("2000-01-01T00:00:00Z");

// Anthropic Agent Skills frontmatter constraints.
const NAME_MAX = 64;
const DESCRIPTION_MAX = 1024;
const NAME_PATTERN = /^[a-z0-9-]+$/;
const RESERVED_NAME_WORDS = ["anthropic", "claude"];
const SKILL_BODY_MAX_LINES = 500;

// Files under this subdirectory of a skill are include-sources only: inlined by
// the include directive, never shipped in the bundle.
const PARTIALS_DIR = "partials";
const INCLUDE_DIRECTIVE = /<!--\s*include:\s*([^\s]+?)\s*-->/g;

class BuildError extends Error {}

async function main() {
  const validateOnly = process.argv.includes("--validate-only");

  const manifest = JSON.parse(await readFile(join(SKILLS_ROOT, "skills.json"), "utf8"));
  requireManifestFields(manifest);

  const skillDir = join(SKILLS_ROOT, manifest.dir);
  await assertDirectory(skillDir, `skills.json "dir" (${manifest.dir})`);

  // Collect every file in the skill directory, then resolve includes and stamp
  // the version so validation runs against the real shipped bytes.
  const files = await collectFiles(skillDir);
  const entryRel = manifest.entry ?? "SKILL.md";
  if (!files.some((file) => file.rel === entryRel)) {
    throw new BuildError(`Entry "${entryRel}" not found in ${manifest.dir}/.`);
  }

  const shipped = [];
  for (const file of files) {
    const isPartial = file.rel.split(posix.sep)[0] === PARTIALS_DIR;
    let text = file.raw;

    if (file.rel.endsWith(".md")) {
      text = resolveIncludes(text, file.rel, skillDir, files);
    }
    if (file.rel === entryRel) {
      text = stampVersion(text, manifest.version, file.rel);
      validateSkillMd(text, manifest, entryRel);
    }

    assertNoUnresolvedIncludes(text, file.rel);
    if (!isPartial) {
      shipped.push({ zipPath: posix.join(manifest.dir, file.rel), content: text });
    }
  }

  shipped.sort((a, b) => (a.zipPath < b.zipPath ? -1 : a.zipPath > b.zipPath ? 1 : 0));

  console.log(`Skill "${manifest.name}" v${manifest.version} — ${shipped.length} file(s):`);
  for (const file of shipped) {
    console.log(`  ${file.zipPath}`);
  }

  if (validateOnly) {
    console.log("Validation passed (--validate-only: no zip written).");
    return;
  }

  await rm(DIST_DIR, { recursive: true, force: true });
  await mkdir(DIST_DIR, { recursive: true });
  const zipPath = join(DIST_DIR, `${manifest.name}-${manifest.version}.zip`);
  await writeZip(zipPath, shipped);

  const hash = createHash("sha256").update(await readFile(zipPath)).digest("hex");
  console.log(`\nWrote ${relative(process.cwd(), zipPath)}`);
  console.log(`sha256: ${hash}`);
}

function requireManifestFields(manifest) {
  for (const field of ["name", "version", "dir"]) {
    if (typeof manifest[field] !== "string" || manifest[field].length === 0) {
      throw new BuildError(`skills.json is missing required string field "${field}".`);
    }
  }
}

async function collectFiles(root) {
  const out = [];
  async function walk(dir) {
    const entries = await readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const abs = join(dir, entry.name);
      if (entry.isDirectory()) {
        await walk(abs);
      } else if (entry.isFile()) {
        const rel = relative(root, abs).split(sep).join(posix.sep);
        out.push({ rel, raw: await readFile(abs, "utf8") });
      }
    }
  }
  await walk(root);
  out.sort((a, b) => (a.rel < b.rel ? -1 : a.rel > b.rel ? 1 : 0));
  return out;
}

// Replaces `<!-- include: file.md -->` with the referenced file's raw content,
// resolved relative to the skill directory. Errors on a missing target.
function resolveIncludes(text, ownerRel, skillDir, files) {
  return text.replace(INCLUDE_DIRECTIVE, (_match, target) => {
    const source = files.find((file) => file.rel === target);
    if (!source) {
      throw new BuildError(`${ownerRel}: include target "${target}" does not exist in the skill.`);
    }
    return source.raw.trimEnd();
  });
}

function assertNoUnresolvedIncludes(text, rel) {
  INCLUDE_DIRECTIVE.lastIndex = 0;
  if (INCLUDE_DIRECTIVE.test(text)) {
    throw new BuildError(`${rel}: unresolved include directive remains after resolution.`);
  }
}

function stampVersion(text, version, rel) {
  const parsed = parseFrontmatter(text, rel);
  const body = parsed.body;
  const fm = parsed.frontmatter;

  const lines = fm.split("\n");
  const versionIdx = lines.findIndex((line) => /^version:\s*/.test(line));
  const stamped = `version: ${version}`;
  if (versionIdx >= 0) {
    lines[versionIdx] = stamped;
  } else {
    lines.push(stamped);
  }
  return `---\n${lines.join("\n")}\n---\n${body}`;
}

function parseFrontmatter(text, rel) {
  if (!text.startsWith("---\n")) {
    throw new BuildError(`${rel}: missing YAML frontmatter (must start with '---').`);
  }
  const end = text.indexOf("\n---", 4);
  if (end === -1) {
    throw new BuildError(`${rel}: frontmatter is not terminated with '---'.`);
  }
  const frontmatter = text.slice(4, end).replace(/\n$/, "");
  // Body starts after the closing '---' line.
  const afterClose = text.indexOf("\n", end + 1);
  const body = afterClose === -1 ? "" : text.slice(afterClose + 1);
  return { frontmatter, body };
}

// Minimal `key: value` reader — the frontmatter we author is flat scalars only.
function readFrontmatterField(frontmatter, key) {
  const match = frontmatter.match(new RegExp(`^${key}:\\s*(.*)$`, "m"));
  if (!match) {
    return null;
  }
  return match[1].trim().replace(/^["']|["']$/g, "");
}

function validateSkillMd(text, manifest, rel) {
  const { frontmatter, body } = parseFrontmatter(text, rel);

  const name = readFrontmatterField(frontmatter, "name");
  const description = readFrontmatterField(frontmatter, "description");

  if (!name) {
    throw new BuildError(`${rel}: frontmatter "name" is required.`);
  }
  if (name !== manifest.name) {
    throw new BuildError(`${rel}: frontmatter name "${name}" must equal skills.json name "${manifest.name}".`);
  }
  if (name.length > NAME_MAX || !NAME_PATTERN.test(name)) {
    throw new BuildError(`${rel}: name must be <=${NAME_MAX} chars, lowercase letters/numbers/hyphens only.`);
  }
  if (RESERVED_NAME_WORDS.some((word) => name.includes(word))) {
    throw new BuildError(`${rel}: name must not contain reserved words (${RESERVED_NAME_WORDS.join(", ")}).`);
  }

  if (!description) {
    throw new BuildError(`${rel}: frontmatter "description" is required and must be non-empty.`);
  }
  if (description.length > DESCRIPTION_MAX) {
    throw new BuildError(`${rel}: description is ${description.length} chars; max is ${DESCRIPTION_MAX}.`);
  }
  if (/[<>]/.test(name) || /[<>]/.test(description)) {
    throw new BuildError(`${rel}: name/description must not contain angle brackets (no XML tags).`);
  }

  const bodyLines = body.split("\n").length;
  if (bodyLines > SKILL_BODY_MAX_LINES) {
    throw new BuildError(`${rel}: body is ${bodyLines} lines; keep it under ${SKILL_BODY_MAX_LINES} (progressive disclosure).`);
  }
}

function writeZip(zipPath, files) {
  return new Promise((resolve, reject) => {
    const output = createWriteStream(zipPath);
    // store: no compression -> bytes depend only on content + headers + fixed
    // date, so the archive is byte-for-byte reproducible.
    const archive = new ZipArchive({ store: true });

    output.on("close", resolve);
    output.on("error", reject);
    archive.on("error", reject);
    archive.on("warning", reject);

    archive.pipe(output);
    for (const file of files) {
      archive.append(file.content, { name: file.zipPath, date: FIXED_MTIME });
    }
    archive.finalize();
  });
}

async function assertDirectory(path, label) {
  try {
    const info = await stat(path);
    if (!info.isDirectory()) {
      throw new BuildError(`${label} is not a directory.`);
    }
  } catch (error) {
    if (error instanceof BuildError) {
      throw error;
    }
    throw new BuildError(`${label} was not found at ${path}.`);
  }
}

main().catch((error) => {
  const message = error instanceof BuildError ? error.message : (error?.stack ?? String(error));
  console.error(`skills-gen: ${message}`);
  process.exitCode = 1;
});
