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

## Connect to Foundry
1. Start the server over HTTP: `python bakery_mcp_server.py --http`
2. Expose it on a public HTTPS URL Foundry can reach (`devtunnel host -p 8000` or ngrok); the URL must end in `/mcp`.
3. `export BAKERY_MCP_SERVER_URL=https://<tunnel>/mcp`
4. `python mcp-server.py`

The agent is created with `MCPTool(server_label="frankies_bakery", server_url=..., require_approval="never", allowed_tools=[...])` and answers using your tools.

## Key idea
Foundry hosts the agent; **you** host the MCP server. `require_approval` and
`allowed_tools` give you a security boundary over which tools the model may call.
