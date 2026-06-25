using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace BakeryMcp;

/// <summary>
/// Frankie's Bakery tools exposed over the Model Context Protocol.
/// Each method delegates to <see cref="BakeryStore"/> and returns JSON text.
/// Mirrors the 5 tools in python/labs/14-mcp-server/bakery_mcp_server.py.
/// </summary>
[McpServerToolType]
public static class BakeryTools
{
    [McpServerTool(Name = "list_products")]
    [Description("List bakery products. Optionally filter by category (Bread, Pastry, Cake...) and stock.")]
    public static string ListProducts(
        [Description("Category to filter by, e.g. Bread. Empty for all.")] string category = "",
        [Description("If true, exclude out-of-stock products.")] bool availableOnly = false)
        => BakeryStore.ToJson(BakeryStore.ListProducts(string.IsNullOrEmpty(category) ? null : category, availableOnly));

    [McpServerTool(Name = "get_product")]
    [Description("Get full details for a single product by its id (e.g. BD-001).")]
    public static string GetProduct(
        [Description("The product id, e.g. BD-001.")] string productId)
    {
        var product = BakeryStore.GetProduct(productId);
        return product is null
            ? BakeryStore.ToJson(new JsonObject { ["error"] = $"No product with id '{productId}'." })
            : BakeryStore.ToJson(product);
    }

    [McpServerTool(Name = "search_products")]
    [Description("Search products by keyword across name, description, ingredients, and tags.")]
    public static string SearchProducts(
        [Description("Keyword to search for, e.g. chocolate.")] string query)
        => BakeryStore.ToJson(BakeryStore.SearchProducts(query));

    [McpServerTool(Name = "place_order")]
    [Description("Place an order for a product. Returns the created order or an error.")]
    public static string PlaceOrder(
        [Description("The product id to order, e.g. BD-001.")] string productId,
        [Description("Quantity to order (>= 1).")] int quantity = 1,
        [Description("Customer name for the order.")] string customer = "guest")
        => BakeryStore.ToJson(BakeryStore.PlaceOrder(productId, quantity, customer));

    [McpServerTool(Name = "list_orders")]
    [Description("List previously placed orders, optionally filtered by customer name.")]
    public static string ListOrders(
        [Description("Customer name to filter by. Empty for all.")] string customer = "")
        => BakeryStore.ToJson(BakeryStore.ListOrders(string.IsNullOrEmpty(customer) ? null : customer));
}
