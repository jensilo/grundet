# Grundet Skills

The user-facing **agent skill** Grundet distributes to its users — it teaches an MCP client
(Claude Code, Claude Desktop, and other agents) how to connect to a Grundet workspace over the
[MCP server](../../docs/architecture.md#mcp-server-mcp) and read versioned product intent.

This is a **product artifact**, not repo dev tooling. It is published to users as a downloadable
bundle by the release pipeline. (Repo-internal briefing skills such as `before-backend` live under
`.claude/skills/` and are **not** shipped.)

## Layout — one skill, progressive disclosure

Grundet ships as a **single skill** following Anthropic's
[Agent Skills best practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices):
one directory the agent loads on demand. `SKILL.md` is the always-considered root; the other files
are read only when the task needs them (progressive disclosure), keeping the loaded context lean.

```
src/Grundet.Skills/
  skills.json          manifest: the single bundle's name, version, description (drives the generator)
  README.md            this file
  grundet/
    SKILL.md           root — what Grundet is, the guardrail, and routing to the files below
    setup.md           mint an API key, configure the MCP client, resolve a project
    read-intent.md     the three intent tools and how to read the versioned data
    concepts.md        the model: hierarchy, Intent vs Work, references, versioning
```

`SKILL.md` frontmatter needs `name` and `description`; the generator stamps `version` from
`skills.json`. Keep the `SKILL.md` body under ~500 lines and keep reference links one level deep.

## Building the bundle

The generator ([`tools/skills-gen/generate.mjs`](../../tools/skills-gen/)) reads `skills.json`,
resolves any `<!-- include: file.md -->` directives, stamps the version into the frontmatter,
validates the frontmatter, and emits a deterministic `dist/grundet-<version>.zip` containing the
`grundet/` folder.

```bash
cd tools/skills-gen && pnpm install && pnpm run build
# or from repo root: make skills
```

`dist/` is git-ignored. See the generator's README for options.

## Download

Published bundles are attached as assets to the GitHub Release cut from a `skills-v*` tag. Grab
`grundet-<version>.zip` from the latest release and install it in your MCP client as a skill.

## Changing the skill

1. Edit the files under `grundet/` (frontmatter `name` must stay `grundet`).
2. Bump `version` in `skills.json` on a meaningful change.
3. Rebuild and validate (`make skills`); CI also validates on every PR.
