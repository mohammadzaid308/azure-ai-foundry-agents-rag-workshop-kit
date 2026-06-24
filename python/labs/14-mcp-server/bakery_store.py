"""Pure bakery data + order logic shared by the MCP server and offline tests.

No Azure or network dependencies, so the same functions can be unit tested
offline and exposed as MCP tools by bakery_mcp_server.py.
"""
import json
from pathlib import Path

HERE = Path(__file__).parent
PRODUCTS_DIR = HERE / "data" / "products"
ORDERS_PATH = HERE / "data" / "orders.json"


def load_products():
    products = []
    for path in sorted(PRODUCTS_DIR.glob("*.json")):
        products.append(json.loads(path.read_text()))
    return products


def list_products(category=None, available_only=False):
    """Return products, optionally filtered by category and availability."""
    products = load_products()
    if category:
        products = [p for p in products if p["category"].lower() == category.lower()]
    if available_only:
        products = [p for p in products if p.get("availability", "").lower() != "out of stock"]
    return products


def get_product(product_id):
    """Return a single product by id, or None."""
    for product in load_products():
        if product["product_id"].lower() == product_id.lower():
            return product
    return None


def search_products(query):
    """Keyword search over name, description, tags, and ingredients."""
    q = query.lower().strip()
    matches = []
    for product in load_products():
        haystack = " ".join([
            product.get("name", ""),
            product.get("description", ""),
            product.get("ingredients", ""),
            " ".join(product.get("tags", [])),
        ]).lower()
        if q in haystack:
            matches.append(product)
    return matches


def _read_orders():
    if ORDERS_PATH.exists():
        return json.loads(ORDERS_PATH.read_text())
    return []


def place_order(product_id, quantity=1, customer="guest"):
    """Place an order for a product. Validates the product exists and is in stock."""
    product = get_product(product_id)
    if product is None:
        return {"ok": False, "error": f"Unknown product_id '{product_id}'."}
    if product.get("availability", "").lower() == "out of stock":
        return {"ok": False, "error": f"'{product['name']}' is out of stock."}
    if quantity < 1:
        return {"ok": False, "error": "Quantity must be at least 1."}

    orders = _read_orders()
    order = {
        "order_id": f"ORD-{len(orders) + 1:04d}",
        "product_id": product["product_id"],
        "name": product["name"],
        "quantity": quantity,
        "unit_price": product["price"],
        "total": round(product["price"] * quantity, 2),
        "customer": customer,
    }
    orders.append(order)
    ORDERS_PATH.write_text(json.dumps(orders, indent=2))
    return {"ok": True, "order": order}


def list_orders(customer=None):
    """Return all orders, optionally filtered by customer."""
    orders = _read_orders()
    if customer:
        orders = [o for o in orders if o["customer"].lower() == customer.lower()]
    return orders
