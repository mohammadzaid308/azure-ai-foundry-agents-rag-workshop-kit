"""Offline tests for the bakery MCP server logic (run with: pytest)."""
import asyncio

import bakery_mcp_server as srv
import bakery_store as store


def test_server_registers_five_tools():
    tools = asyncio.run(srv.mcp.list_tools())
    names = {t.name for t in tools}
    assert names == {"list_products", "get_product", "search_products", "place_order", "list_orders"}


def test_list_and_filter_products():
    assert len(store.list_products()) == 20
    bread = store.list_products(category="Bread")
    assert bread and all(p["category"] == "Bread" for p in bread)


def test_search_finds_products():
    assert store.search_products("zzzznotreal") == []
    assert len(store.search_products("flour")) > 0


def test_place_order_validates_product():
    bad = store.place_order("NOPE-999")
    assert bad["ok"] is False
    good = store.place_order(store.list_products()[0]["product_id"], quantity=3, customer="tester")
    assert good["ok"] is True
    assert good["order"]["quantity"] == 3
    assert any(o["customer"] == "tester" for o in store.list_orders(customer="tester"))
