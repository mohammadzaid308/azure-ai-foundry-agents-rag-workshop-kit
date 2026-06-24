"""Frankie's Bakery MCP server (FastMCP).

Exposes the bakery catalog and ordering logic as Model Context Protocol tools.
Run it locally and connect a Foundry agent to it with the hosted MCP tool
(see mcp-server.py), or call it from any MCP client (Claude Desktop, VS Code, etc.).

Run (Streamable HTTP, recommended for Foundry):
    python bakery_mcp_server.py --http        # serves http://127.0.0.1:8000/mcp

Run (stdio, for local MCP clients):
    python bakery_mcp_server.py
"""
import argparse

from mcp.server.fastmcp import FastMCP

import bakery_store as store

mcp = FastMCP("frankies-bakery")


@mcp.tool()
def list_products(category: str = "", available_only: bool = False) -> list:
    """List bakery products. Optionally filter by category (Bread, Pastry, Cake...) and stock."""
    return store.list_products(category=category or None, available_only=available_only)


@mcp.tool()
def get_product(product_id: str) -> dict:
    """Get full details for a single product by its id (e.g. BD-001)."""
    product = store.get_product(product_id)
    return product or {"error": f"No product with id '{product_id}'."}


@mcp.tool()
def search_products(query: str) -> list:
    """Search products by keyword across name, description, ingredients, and tags."""
    return store.search_products(query)


@mcp.tool()
def place_order(product_id: str, quantity: int = 1, customer: str = "guest") -> dict:
    """Place an order for a product. Returns the created order or an error."""
    return store.place_order(product_id, quantity=quantity, customer=customer)


@mcp.tool()
def list_orders(customer: str = "") -> list:
    """List previously placed orders, optionally filtered by customer name."""
    return store.list_orders(customer=customer or None)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--http", action="store_true", help="Serve over Streamable HTTP")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()

    if args.http:
        mcp.settings.host = args.host
        mcp.settings.port = args.port
        print(f"Serving MCP over http://{args.host}:{args.port}/mcp")
        mcp.run(transport="streamable-http")
    else:
        mcp.run()
