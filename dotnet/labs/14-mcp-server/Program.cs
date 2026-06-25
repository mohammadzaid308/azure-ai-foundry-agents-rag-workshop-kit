using System.Reflection;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OpenAI.Responses;
using BakeryMcp;

#pragma warning disable OPENAI001

// Frankie's Bakery MCP lab (.NET). One binary, several modes:
//
//   dotnet run -- --http        Serve the bakery tools over Streamable HTTP (for Foundry / Inspector)
//   dotnet run -- --stdio       Serve over stdio (for local MCP clients / Inspector)
//   dotnet run -- --offline     Exercise the tools in-process (no Azure)
//   dotnet run -- --test        Run the offline assertions (no Azure)
//   dotnet run                  Connect a Foundry agent to the running MCP server
//
// Mirrors python/labs/14-mcp-server (bakery_mcp_server.py + mcp-server.py).

string mode = args.Length > 0 ? args[0] : "";

switch (mode)
{
    case "--http":
        await RunHttpServerAsync(args);
        return 0;
    case "--stdio":
        await RunStdioServerAsync();
        return 0;
    case "--offline":
        RunOfflineDemo();
        return 0;
    case "--test":
        return RunTests();
    default:
        await RunFoundryDemoAsync();
        return 0;
}

// ──────────────────────────────────────────────────────────────────────────
// MCP server (the tool provider) — Streamable HTTP, served at /mcp
async Task RunHttpServerAsync(string[] a)
{
    string host = GetArg(a, "--host") ?? "127.0.0.1";
    string port = GetArg(a, "--port") ?? "8000";

    WebApplicationBuilder builder = WebApplication.CreateBuilder();
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    WebApplication app = builder.Build();
    app.MapMcp("/mcp");

    Console.WriteLine($"Serving MCP over http://{host}:{port}/mcp");
    await app.RunAsync($"http://{host}:{port}");
}

// MCP server over stdio (for local MCP clients / the Inspector's STDIO transport).
// Logs MUST go to stderr so they don't corrupt the JSON-RPC stream on stdout.
async Task RunStdioServerAsync()
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();
    await builder.Build().RunAsync();
}

// ──────────────────────────────────────────────────────────────────────────
// Offline demo — prove the tools work without Azure.
void RunOfflineDemo()
{
    Console.WriteLine("MCP server exposes 5 tools: list_products, get_product, search_products, place_order, list_orders\n");

    Console.WriteLine("search_products('chocolate'):");
    foreach (var p in BakeryStore.SearchProducts("chocolate"))
        Console.WriteLine($"  {p["product_id"]}  {p["name"]}  ${p["price"]}");

    var bread = BakeryStore.ListProducts("Bread");
    Console.WriteLine($"\nlist_products(category='Bread') -> {bread.Count} items");

    string first = bread[0]["product_id"]!.GetValue<string>();
    var result = BakeryStore.PlaceOrder(first, quantity: 2, customer: "workshop");
    Console.WriteLine($"\nplace_order({first}, 2): ok={result["ok"]} total=${result["order"]!["total"]}");

    Console.WriteLine($"list_orders('workshop') -> {BakeryStore.ListOrders("workshop").Count} order(s)");
}

// Offline assertions — mirrors python test_offline.py.
int RunTests()
{
    int failed = 0;
    void Check(string name, bool cond)
    {
        Console.WriteLine($"{(cond ? "PASS" : "FAIL")}  {name}");
        if (!cond) failed++;
    }

    int toolCount = typeof(BakeryTools)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Count(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0);
    Check("server registers exactly 5 tools", toolCount == 5);

    var all = BakeryStore.ListProducts();
    Check("20 products load", all.Count == 20);

    var bread = BakeryStore.ListProducts("Bread");
    Check("Bread filter returns only Bread", bread.Count > 0 &&
        bread.All(p => p["category"]!.GetValue<string>() == "Bread"));

    Check("search 'flour' finds products", BakeryStore.SearchProducts("flour").Count > 0);
    Check("search 'zzzznotreal' finds nothing", BakeryStore.SearchProducts("zzzznotreal").Count == 0);

    var bad = BakeryStore.PlaceOrder("NOPE-999");
    Check("bad product_id rejected", bad["ok"]!.GetValue<bool>() == false);

    var good = BakeryStore.PlaceOrder(all[0]["product_id"]!.GetValue<string>(), quantity: 3, customer: "tester");
    Check("valid order accepted (qty 3)", good["ok"]!.GetValue<bool>() &&
        good["order"]!["quantity"]!.GetValue<int>() == 3);

    Console.WriteLine(failed == 0 ? "\nAll tests passed." : $"\n{failed} test(s) failed.");
    return failed == 0 ? 0 : 1;
}

// ──────────────────────────────────────────────────────────────────────────
// Foundry agent — register the running MCP server as a hosted MCP tool.
async Task RunFoundryDemoAsync()
{
    // ──────────────────────────────────────────────────────────────────────
    // IMPORTANT — this is the ONE path that needs a PUBLIC HTTPS tunnel.
    //
    // Foundry calls your MCP server SERVER-SIDE from Azure, so it CANNOT reach
    // http://127.0.0.1 / http://localhost. BAKERY_MCP_SERVER_URL must be a
    // public HTTPS URL that ends in /mcp. Expose your local server first, e.g.:
    //     devtunnel host -p 8000            // then use the https://...devtunnels.ms/mcp URL
    //     ngrok http 8000                   // then use the https://....ngrok.app/mcp URL
    //     export BAKERY_MCP_SERVER_URL="https://<public-host>/mcp"
    //
    // The offline demo (--offline), the unit tests (--test), the MCP Inspector,
    // and the live HTTP protocol surface all work WITHOUT a tunnel. Only this
    // hosted-MCP call to the Azure agent requires the public URL above.
    // ──────────────────────────────────────────────────────────────────────
    string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
        ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
    string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "bakery-mcp-agent";
    string model = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
    string serverUrl = Environment.GetEnvironmentVariable("BAKERY_MCP_SERVER_URL")
        ?? throw new InvalidOperationException(
            "BAKERY_MCP_SERVER_URL is required (public https URL ending in /mcp).");

    AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

    // The hosted MCP tool points the agent at YOUR server. Foundry calls it server-side.
    McpTool mcpTool = new(serverLabel: "frankies_bakery", serverUri: new Uri(serverUrl))
    {
        ToolCallApprovalPolicy = new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval),
        AllowedTools = new McpToolFilter(),
    };
    foreach (var name in new[] { "list_products", "search_products", "place_order", "list_orders" })
        mcpTool.AllowedTools.ToolNames.Add(name);

    DeclarativeAgentDefinition definition = new(model: model)
    {
        Instructions = "You are Frankie's Bakery assistant. Use the MCP tools to answer "
            + "questions about products and to place orders. Never invent products.",
    };
    definition.Tools.Add(mcpTool);

    ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
        agentName: agentName, options: new(definition));

    ProjectConversation conversation = await projectClient.ProjectOpenAIClient
        .GetProjectConversationsClient().CreateProjectConversationAsync();
    ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForAgent(agentName, conversation);

    ResponseResult response = await responses.CreateResponseAsync(
        "What chocolate items do you have, and order me two of the cheapest one.");
    Console.WriteLine(response.GetOutputText());
}

// ──────────────────────────────────────────────────────────────────────────
static string? GetArg(string[] a, string flag)
{
    int i = Array.IndexOf(a, flag);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

// ──────────────────────────────────────────────────────────────────────────
// PORTAL OBSERVATION (live Foundry path only)
//   After running the default path (not --offline):
//     Microsoft Foundry portal -> "Agents" -> open <agent> -> "Playground".
//     Ask: "Do you have any seasonal bread?"
//     Expand "Show details" (and the agent's "Traces" tab) on the MCP tool call
//     to see the MCP server URL, the tool name + arguments, and the raw JSON your
//     server returned. Your server, Foundry's agent.
//
// CHALLENGE — Add a "get_recommendations" tool
//   1. Add a new [McpServerTool] method to BakeryTools that returns up to 3
//      products under a budget matching a dietary tag (sort by rating).
//   2. Add a --test assertion for it.
//   3. Add "get_recommendations" to AllowedTools above.
//   4. Ask the agent: "I have $10 and I'm gluten-free. Any suggestions?"
