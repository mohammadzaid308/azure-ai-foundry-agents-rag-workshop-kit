using System.Text.Json;
using System.Text.Json.Nodes;

namespace BakeryMcp;

/// <summary>
/// Pure bakery data + order logic shared by the MCP server and the offline
/// demo/tests. No Azure or network dependencies, so the same functions can be
/// exercised offline and exposed as MCP tools by <see cref="BakeryTools"/>.
/// Mirrors python/labs/14-mcp-server/bakery_store.py.
/// </summary>
public static class BakeryStore
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string ProductsDir = Path.Combine(DataDir, "products");
    private static readonly string OrdersPath = Path.Combine(DataDir, "orders.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static List<JsonObject> LoadProducts()
    {
        var products = new List<JsonObject>();
        if (!Directory.Exists(ProductsDir))
            return products;

        foreach (var path in Directory.GetFiles(ProductsDir, "*.json").OrderBy(p => p))
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is JsonObject obj)
                products.Add(obj);
        }
        return products;
    }

    public static List<JsonObject> ListProducts(string? category = null, bool availableOnly = false)
    {
        IEnumerable<JsonObject> products = LoadProducts();
        if (!string.IsNullOrWhiteSpace(category))
            products = products.Where(p => Str(p, "category").Equals(category, StringComparison.OrdinalIgnoreCase));
        if (availableOnly)
            products = products.Where(p => !Str(p, "availability").Equals("out of stock", StringComparison.OrdinalIgnoreCase));
        return products.ToList();
    }

    public static JsonObject? GetProduct(string productId)
    {
        return LoadProducts()
            .FirstOrDefault(p => Str(p, "product_id").Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    public static List<JsonObject> SearchProducts(string query)
    {
        var q = query.ToLowerInvariant().Trim();
        var matches = new List<JsonObject>();
        foreach (var p in LoadProducts())
        {
            var tags = p["tags"] is JsonArray arr
                ? string.Join(" ", arr.Select(t => t?.GetValue<string>() ?? ""))
                : "";
            var haystack = string.Join(" ", new[]
            {
                Str(p, "name"), Str(p, "description"), Str(p, "ingredients"), tags
            }).ToLowerInvariant();
            if (haystack.Contains(q))
                matches.Add(p);
        }
        return matches;
    }

    public static JsonObject PlaceOrder(string productId, int quantity = 1, string customer = "guest")
    {
        var product = GetProduct(productId);
        if (product is null)
            return new JsonObject { ["ok"] = false, ["error"] = $"Unknown product_id '{productId}'." };
        if (Str(product, "availability").Equals("out of stock", StringComparison.OrdinalIgnoreCase))
            return new JsonObject { ["ok"] = false, ["error"] = $"'{Str(product, "name")}' is out of stock." };
        if (quantity < 1)
            return new JsonObject { ["ok"] = false, ["error"] = "Quantity must be at least 1." };

        var orders = ReadOrders();
        var unitPrice = product["price"]?.GetValue<double>() ?? 0;
        var order = new JsonObject
        {
            ["order_id"] = $"ORD-{orders.Count + 1:D4}",
            ["product_id"] = Str(product, "product_id"),
            ["name"] = Str(product, "name"),
            ["quantity"] = quantity,
            ["unit_price"] = unitPrice,
            ["total"] = Math.Round(unitPrice * quantity, 2),
            ["customer"] = customer,
        };
        orders.Add(order);
        WriteOrders(orders);
        return new JsonObject { ["ok"] = true, ["order"] = order.DeepClone() };
    }

    public static List<JsonObject> ListOrders(string? customer = null)
    {
        IEnumerable<JsonObject> orders = ReadOrders();
        if (!string.IsNullOrWhiteSpace(customer))
            orders = orders.Where(o => Str(o, "customer").Equals(customer, StringComparison.OrdinalIgnoreCase));
        return orders.ToList();
    }

    // ----- helpers -----

    private static List<JsonObject> ReadOrders()
    {
        if (!File.Exists(OrdersPath))
            return new List<JsonObject>();
        return JsonNode.Parse(File.ReadAllText(OrdersPath)) is JsonArray arr
            ? arr.OfType<JsonObject>().Select(o => (JsonObject)o.DeepClone()).ToList()
            : new List<JsonObject>();
    }

    private static void WriteOrders(List<JsonObject> orders)
    {
        Directory.CreateDirectory(DataDir);
        var arr = new JsonArray(orders.Select(o => (JsonNode)o.DeepClone()).ToArray());
        File.WriteAllText(OrdersPath, arr.ToJsonString(Indented));
    }

    private static string Str(JsonObject obj, string key) => obj[key]?.GetValue<string>() ?? "";

    /// <summary>Serialize any node (or list) to a JSON string for MCP tool output.</summary>
    public static string ToJson(object? value) => JsonSerializer.Serialize(value, Indented);
}
