using System.Text.Json;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

// Lab: Function tools (Frankie's Bakery storefront assistant).
// Three local tools are backed by the JSON product catalog in ./data/products.
// The catalog reads happen locally, so tool logic works offline; only the model
// calls reach Azure AI Foundry.

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

string dataDir = Path.Combine(AppContext.BaseDirectory, "data", "products");

List<JsonElement> Catalog() =>
    Directory.EnumerateFiles(dataDir, "*.json")
        .OrderBy(p => p)
        .Select(p => JsonDocument.Parse(File.ReadAllText(p)).RootElement.Clone())
        .ToList();

string ListProducts(string? category)
{
    IEnumerable<JsonElement> items = Catalog();
    if (!string.IsNullOrWhiteSpace(category))
        items = items.Where(p => string.Equals(p.GetProperty("category").GetString(), category, StringComparison.OrdinalIgnoreCase));
    var summary = items.Select(p => new
    {
        product_id = p.GetProperty("product_id").GetString(),
        name = p.GetProperty("name").GetString(),
        category = p.GetProperty("category").GetString(),
        price = p.GetProperty("price").GetDouble(),
        availability = p.GetProperty("availability").GetString(),
    });
    return JsonSerializer.Serialize(summary);
}

string GetProduct(string productId)
{
    foreach (JsonElement p in Catalog())
        if (string.Equals(p.GetProperty("product_id").GetString(), productId, StringComparison.OrdinalIgnoreCase))
            return p.GetRawText();
    return JsonSerializer.Serialize(new { error = $"No product {productId}" });
}

int orderSeq = 0;
string PlaceOrder(string productId, int quantity)
{
    foreach (JsonElement p in Catalog())
    {
        if (string.Equals(p.GetProperty("product_id").GetString(), productId, StringComparison.OrdinalIgnoreCase))
        {
            double price = p.GetProperty("price").GetDouble();
            return JsonSerializer.Serialize(new
            {
                order_id = $"ORD-{++orderSeq:D4}",
                product_id = p.GetProperty("product_id").GetString(),
                name = p.GetProperty("name").GetString(),
                quantity,
                line_total = Math.Round(price * quantity, 2),
            });
        }
    }
    return JsonSerializer.Serialize(new { error = $"No product {productId}" });
}

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());
ProjectResponsesClient responses =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

ResponseTool listTool = ResponseTool.CreateFunctionTool(
    functionName: "list_products",
    functionParameters: BinaryData.FromString(
        "{\"type\":\"object\",\"properties\":{\"category\":{\"type\":\"string\"}},\"required\":[]}"),
    strictModeEnabled: false,
    functionDescription: "List bakery products, optionally filtered by category (Bread, Cake, Pastry, ...).");

ResponseTool getTool = ResponseTool.CreateFunctionTool(
    functionName: "get_product",
    functionParameters: BinaryData.FromString(
        "{\"type\":\"object\",\"properties\":{\"product_id\":{\"type\":\"string\"}},\"required\":[\"product_id\"]}"),
    strictModeEnabled: false,
    functionDescription: "Get full detail (ingredients, rating, availability) for one product by product_id.");

ResponseTool orderTool = ResponseTool.CreateFunctionTool(
    functionName: "place_order",
    functionParameters: BinaryData.FromString(
        "{\"type\":\"object\",\"properties\":{\"product_id\":{\"type\":\"string\"},\"quantity\":{\"type\":\"integer\"}},\"required\":[\"product_id\",\"quantity\"]}"),
    strictModeEnabled: false,
    functionDescription: "Place an order for a product_id and quantity. Returns an order confirmation.");

var options = new CreateResponseOptions(modelDeployment, new List<ResponseItem>
{
    ResponseItem.CreateUserMessageItem(
        "I'd like two loaves of your Challah Bread. First confirm it exists and tell me the price, then place the order.")
});
options.Tools.Add(listTool);
options.Tools.Add(getTool);
options.Tools.Add(orderTool);

ResponseResult response = await responses.CreateResponseAsync(options);

// Tool-call loop: resolve function calls until the model produces text.
for (int turn = 0; turn < 5; turn++)
{
    bool hadToolCall = false;
    foreach (ResponseItem item in response.OutputItems)
    {
        if (item is FunctionCallResponseItem call)
        {
            hadToolCall = true;
            using JsonDocument callArgs = JsonDocument.Parse(call.FunctionArguments);
            JsonElement root = callArgs.RootElement;
            string result = call.FunctionName switch
            {
                "list_products" => ListProducts(root.TryGetProperty("category", out var c) ? c.GetString() : null),
                "get_product" => GetProduct(root.GetProperty("product_id").GetString() ?? ""),
                "place_order" => PlaceOrder(root.GetProperty("product_id").GetString() ?? "",
                                            root.GetProperty("quantity").GetInt32()),
                _ => JsonSerializer.Serialize(new { error = "unknown tool" }),
            };
            options.InputItems.Add(call);
            options.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(call.CallId, result));
        }
    }
    if (!hadToolCall) break;
    response = await responses.CreateResponseAsync(options);
}

Console.WriteLine(response.GetOutputText());
