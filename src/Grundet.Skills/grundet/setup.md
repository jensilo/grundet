# Connecting to Grundet over MCP

Read this to get an agent connected to a Grundet workspace: mint an API key, configure the MCP
connection, and resolve a project. Tool names below assume you name the MCP server `grundet`.

## Contents
- 1. Get an API key
- 2. Configure the MCP connection
- 3. Verify the connection
- 4. Pick a project
- Troubleshooting

## 1. Get an API key

The Grundet MCP server authenticates with a **per-Team API key** sent as a Bearer token. A key is
scoped to one Team and acts as the issuing member, so it can only read what that member can read.

Mint one in the Grundet web app (a Team **Lead** can do this):

**Teams → your team → API Keys → Create.**

Copy the **raw key** immediately — it is shown exactly once (it starts with `grundet_`). Treat it
like a password: never commit it; prefer an environment variable or your client's secret store
over an inline literal.

If a key is revoked or expired, or the issuing member is removed from the team, every call fails —
membership is checked live on each request.

## 2. Configure the MCP connection

The server speaks **Streamable HTTP** at `/mcp` on the Grundet API host. Replace
`YOUR-GRUNDET-API-HOST` with your Grundet API base URL throughout.

### Claude Desktop (via `mcp-remote`)

Claude Desktop connects to remote HTTP servers through the `mcp-remote` bridge. Edit
`claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{
  "mcpServers": {
    "grundet": {
      "command": "npx",
      "args": [
        "-y", "mcp-remote",
        "https://YOUR-GRUNDET-API-HOST/mcp",
        "--header", "Authorization: Bearer ${GRUNDET_API_KEY}"
      ],
      "env": { "GRUNDET_API_KEY": "grundet_YOUR_RAW_KEY" }
    }
  }
}
```

`mcp-remote` substitutes `${GRUNDET_API_KEY}` from `env`, so the raw key stays out of the args
list. Fully restart Claude Desktop after editing. For a **local, non-HTTPS** host (e.g.
`http://localhost:5xxx/mcp`), add `"--allow-http"` to `args` and use the `http://` URL.

### Claude Code (native HTTP)

Claude Code speaks Streamable HTTP directly — no bridge needed:

```json
{
  "mcpServers": {
    "grundet": {
      "type": "http",
      "url": "https://YOUR-GRUNDET-API-HOST/mcp",
      "headers": { "Authorization": "Bearer grundet_YOUR_RAW_KEY" }
    }
  }
}
```

## 3. Verify the connection

The MCP handshake (`initialize`) runs on connect; if the key is bad, connecting fails outright.
Once connected, the `grundet:list_projects` tool should be available — calling it is the quickest
proof the whole path works (see step 4).

With `mcp-remote` you will see benign log lines about OAuth ("Token not found", "Connecting to
remote server") — these are expected. Success looks like **"Proxy established successfully"**. The
server uses Bearer auth, not OAuth, so those OAuth lines are noise, not errors.

## 4. Pick a project

Every intent read needs a project **GUID**, but never ask the user for a raw GUID. Resolve it from
a human name:

1. Ask the user which **project name** they mean.
2. Call **`grundet:list_projects`** — it returns every project in the key's team as
   `{ id, name, description, createdAt }`, newest first.
3. Match on name.

**Project names are not unique** within a team. If more than one project matches the name, do
**not** pick the first — list the candidates (name + `description` + `createdAt` as
disambiguators) and ask the user which `id` they mean. Then use that `id` with the intent tools
(see [read-intent.md](read-intent.md)).

## Troubleshooting

- **Connection fails immediately** → the key is wrong, revoked, or expired, or the issuer was
  removed from the team. Mint a fresh key (step 1).
- **`mcp-remote` seems stuck on OAuth** → clear its cache (`rm -rf ~/.mcp-auth`) and fully restart
  the client.
- **`list_projects` returns nothing** → the key's team has no projects yet, or you are pointed at
  the wrong host. Confirm `YOUR-GRUNDET-API-HOST` and that the team owns projects in the web app.
- **A project you expect is missing** → it belongs to a different team than the key. Use a key for
  that team.
