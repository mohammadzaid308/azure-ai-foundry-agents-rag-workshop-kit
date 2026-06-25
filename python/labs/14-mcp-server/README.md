# Lab: Build your own MCP server (Frankie's Bakery)

Build a **Model Context Protocol (MCP)** server in Python and connect a Foundry
agent to it using the hosted MCP tool.

## Files
| File | Purpose |
|------|---------|
| `bakery_store.py` | Pure catalog + order logic (no Azure). Backed by `data/products/*.json`. |
| `bakery_mcp_server.py` | FastMCP server exposing 5 tools: `list_products`, `get_product`, `search_products`, `place_order`, `list_orders`. |
| `mcp-server.py` | Registers the server with a Foundry agent via `MCPTool`; `--offline` runs an in-process demo. |
| `test_offline.py` | pytest checks that run with no Azure. |

## Run offline (no Azure)
```bash
pip install -r requirements.txt
python mcp-server.py --offline      # exercises the tools in-process
pytest -q                           # 4 tests
```

## Inspect with the MCP Inspector (no Azure)
The [MCP Inspector](https://modelcontextprotocol.io/docs/tools/inspector) is an
interactive browser tool (run via `npx`, needs Node.js) for listing and calling
your tools manually before wiring up Foundry.

**Option A - stdio (Inspector launches the server for you):**
```bash
npx @modelcontextprotocol/inspector python bakery_mcp_server.py
```
The UI opens at `http://localhost:6274` (recent versions print a pre-authenticated
URL with a session token - click that link). Transport `STDIO` is pre-filled; click
**Connect**. Use a full venv path if `python` is not on PATH:
`npx @modelcontextprotocol/inspector /path/to/.venv/bin/python bakery_mcp_server.py`.

**Option B - Streamable HTTP (same transport Foundry uses):**
```bash
# Terminal 1 - start the server over HTTP
python bakery_mcp_server.py --http          # http://127.0.0.1:8000/mcp
# Terminal 2 - launch the Inspector with no command
npx @modelcontextprotocol/inspector
```
In the UI set **Transport Type** = `Streamable HTTP`, **URL** = `http://127.0.0.1:8000/mcp`, **Connect**.

**What to test:** open the **Tools** tab -> **List Tools** (expect all 5:
`list_products`, `get_product`, `search_products`, `place_order`, `list_orders`),
then run each one. Try edge cases (`get_product` with `NOPE-999`,
`search_products` with `zzzznotreal`) and watch the raw JSON in the history pane.

## Connect to Foundry
1. Start the server over HTTP: `python bakery_mcp_server.py --http`
2. Expose it on a public HTTPS URL Foundry can reach (`devtunnel host -p 8000` or ngrok); the URL must end in `/mcp`.
3. `export BAKERY_MCP_SERVER_URL=https://<tunnel>/mcp`
4. `python mcp-server.py`

The agent is created with `MCPTool(server_label="frankies_bakery", server_url=..., require_approval="never", allowed_tools=[...])` and answers using your tools.

## Recommended test order
1. `pytest -q` - unit-test the tool logic.
2. `python mcp-server.py --offline` - in-process integration demo.
3. **MCP Inspector** (above) - verify the live MCP protocol surface + schemas interactively.
4. `--http` + tunnel + `python mcp-server.py` - connect the real Foundry agent.

Always validate offline before spending time on the tunnel / Azure path.

## Key idea
Foundry hosts the agent; **you** host the MCP server. `require_approval` and
`allowed_tools` give you a security boundary over which tools the model may call.
