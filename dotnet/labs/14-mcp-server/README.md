# Lab: Build your own MCP server (Frankie's Bakery)

Build a **Model Context Protocol (MCP)** server in .NET and connect a Foundry
agent to it using the hosted MCP tool. This mirrors the Python lab
(`python/labs/14-mcp-server`) tool-for-tool.

One binary, several modes:

| Command | What it does |
|---------|--------------|
| `dotnet run -- --test` | Offline assertions over the tool logic (no Azure). |
| `dotnet run -- --offline` | Exercise the tools in-process (no Azure). |
| `dotnet run -- --http` | Serve the 5 tools over Streamable HTTP at `/mcp` (for Foundry / Inspector). |
| `dotnet run -- --stdio` | Serve the tools over stdio (for local MCP clients / Inspector). |
| `dotnet run` | Register the running server with a Foundry agent and ask a question. |

## Files
| File | Purpose |
|------|---------|
| `BakeryStore.cs` | Pure catalog + order logic (no Azure). Backed by `data/products/*.json`. |
| `BakeryTools.cs` | The 5 MCP tools: `list_products`, `get_product`, `search_products`, `place_order`, `list_orders`. |
| `Program.cs` | Mode dispatcher: MCP server (http/stdio), offline demo/tests, and the Foundry hosted-MCP client. |

> All `dotnet` commands need `export DOTNET_ROLL_FORWARD=LatestMajor` (the labs
> target `net8.0`).

## Run offline (no Azure)
```bash
export DOTNET_ROLL_FORWARD=LatestMajor
dotnet run -- --test        # 7 assertions
dotnet run -- --offline     # in-process tool demo
```

## Inspect with the MCP Inspector (no Azure)
The [MCP Inspector](https://modelcontextprotocol.io/docs/tools/inspector) is an
interactive browser tool (run via `npx`, needs Node.js) for listing and calling
your tools manually before wiring up Foundry.

**Option A - Streamable HTTP (recommended; same transport Foundry uses):**
```bash
# Terminal 1 - start the server over HTTP
export DOTNET_ROLL_FORWARD=LatestMajor
dotnet run -- --http                 # http://127.0.0.1:8000/mcp
# Terminal 2 - launch the Inspector with no command
npx @modelcontextprotocol/inspector
```
In the UI set **Transport Type** = `Streamable HTTP`, **URL** =
`http://127.0.0.1:8000/mcp`, **Connect**. The UI opens at `http://localhost:6274`
(recent versions print a pre-authenticated URL with a session token - click that link).

**Option B - stdio (Inspector launches the server for you):**
`dotnet run` writes build output to stdout, which corrupts the stdio JSON-RPC
stream, so build first and point the Inspector at the compiled DLL:
```bash
export DOTNET_ROLL_FORWARD=LatestMajor
dotnet build
npx @modelcontextprotocol/inspector dotnet bin/Debug/net8.0/mcp-server.dll --stdio
```
Transport `STDIO` is pre-filled; click **Connect**.

**What to test:** open the **Tools** tab -> **List Tools** (expect all 5:
`list_products`, `get_product`, `search_products`, `place_order`, `list_orders`),
then run each one. Try edge cases (`get_product` with `NOPE-999`,
`search_products` with `zzzznotreal`) and watch the raw JSON in the history pane.

## Connect to Foundry
1. Start the server over HTTP: `dotnet run -- --http`
2. Expose it on a public HTTPS URL Foundry can reach (`devtunnel host -p 8000` or ngrok); the URL must end in `/mcp`.
3. `export BAKERY_MCP_SERVER_URL=https://<tunnel>/mcp`
4. `set -a && source ../../.env && set +a && dotnet run`

The agent is created with
`new McpTool("frankies_bakery", new Uri(serverUrl)) { ToolCallApprovalPolicy = NeverRequireApproval, AllowedTools = {...} }`,
added to `DeclarativeAgentDefinition.Tools`, and answers using your tools.

## Recommended test order
1. `dotnet run -- --test` - unit-test the tool logic.
2. `dotnet run -- --offline` - in-process integration demo.
3. **MCP Inspector** (above) - verify the live MCP protocol surface + schemas interactively.
4. `--http` + tunnel + `dotnet run` - connect the real Foundry agent.

Always validate offline before spending time on the tunnel / Azure path.

## Key idea
You are running the same MCP server twice over: locally (Inspector / offline) and
through Foundry's agent (hosted MCP tool). The agent never sees your code - only
the tool **names, schemas, and JSON responses** your server advertises over MCP.
