using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001, AAIP001

// Lab: Sequential multi-agent with CONDITIONAL (if/else) routing.
//
//   bakery-orchestrator        (classify the ticket -> route)
//           |
//           v  ConditionGroup / if-elif-else  (exactly ONE branch fires)
//   +-------+--------+-----------+---------+
//   menu  orders  complaints   hours     else (out of scope)
//   +-------+--------+-----------+
//           v
//   bakery-synthesizer          (warm customer-facing reply)
//
// Contrast with Lab 11 (concurrent): there, ALL department specialists run in
// parallel (fan-out) and a synthesizer merges them (fan-in). Here, the
// orchestrator picks exactly ONE specialist - conditional routing.
//
// Department knowledge is loaded from ./data/bakery so the bakery's real
// menu/orders/complaints/hours rules drive the specialists.

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string workflowName = Environment.GetEnvironmentVariable("FOUNDRY_WORKFLOW_NAME") ?? "bakery-support-workflow";

string bakeryDir = Path.Combine(AppContext.BaseDirectory, "data", "bakery");

string InstructionsOf(string fileName)
{
    string text = File.ReadAllText(Path.Combine(bakeryDir, fileName));
    string[] parts = text.Split("---");
    return parts.Length > 1 ? parts[1].Trim() : text.Trim();
}

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());
AgentAdministrationClient admin = projectClient.AgentAdministrationClient;

// One orchestrator (router), four department specialists, one synthesizer.
// The orchestrator only emits a route; each specialist owns its department
// knowledge; the synthesizer writes the final customer-facing message.
(string Name, string Instructions)[] agentSpecs =
{
    ("bakery-orchestrator", InstructionsOf("Orchestrator_agent.md")),
    ("bakery-menu", InstructionsOf("MenuAgent.md")),
    ("bakery-orders", InstructionsOf("OrdersAgent.md")),
    ("bakery-complaints", InstructionsOf("ComplaintsAgent.md")),
    ("bakery-hours", InstructionsOf("HoursAgent.md")),
    ("bakery-synthesizer", InstructionsOf("SynthesizerAgent.md")),
};

foreach ((string name, string instructions) in agentSpecs)
{
    DeclarativeAgentDefinition definition = new(model: modelDeployment) { Instructions = instructions };
    ProjectsAgentVersion agent = await admin.CreateAgentVersionAsync(
        agentName: name,
        options: new(definition));
    Console.WriteLine($"Created agent '{agent.Name}' (version {agent.Version})");
}

// Deploy the conditional pipeline as a workflow agent so the BRANCHING shows up
// in the Foundry portal. The CSDL uses a ConditionGroup (switch/case) to route
// to exactly one department. Definition lives in workflow.yaml next to this lab.
string workflowYaml = await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "workflow.yaml"));

ProjectsAgentVersion workflow = await admin.CreateAgentVersionAsync(
    agentName: workflowName,
    options: new(WorkflowAgentDefinition.FromYaml(workflowYaml)),
    foundryFeatures: "WorkflowAgents=V1Preview"); // Workflow agents are a preview feature (opt-in required).
Console.WriteLine($"Created workflow agent '{workflow.Name}' (version {workflow.Version})");

// A single-intent ticket so the router picks ONE branch. Try changing it to an
// orders / complaints / hours question and watch the chosen route change.
string ticket =
    "Hi! My daughter is allergic to tree nuts. Is the almond croissant safe for her, " +
    "and what gluten-free options do you have?";
Console.WriteLine("\n--- Customer ticket ---\n" + ticket);

var routeToAgent = new Dictionary<string, string>
{
    ["menu"] = "bakery-menu",
    ["orders"] = "bakery-orders",
    ["complaints"] = "bakery-complaints",
    ["hours"] = "bakery-hours",
};

const string outOfScope =
    "That question is outside what I can help with at Frankie's Bakery. I can help " +
    "with menu items and allergens, your orders, complaints, and store hours - try " +
    "one of those and I'll get you the right answer.";

// Pull {"route":"..."} out of the orchestrator reply; default to "else".
string ParseRoute(string text)
{
    try
    {
        Match m = Regex.Match(text, "\\{.*\\}", RegexOptions.Singleline);
        if (m.Success)
        {
            using JsonDocument doc = JsonDocument.Parse(m.Value);
            if (doc.RootElement.TryGetProperty("route", out JsonElement routeEl))
            {
                string r = (routeEl.GetString() ?? "else").ToLowerInvariant().Trim();
                return routeToAgent.ContainsKey(r) ? r : "else";
            }
        }
    }
    catch (JsonException) { /* fall through */ }
    return "else";
}

ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient()
    .CreateProjectConversationAsync();

try
{
    ProjectResponsesClient workflowResponses = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForAgent(workflowName, conversation);
    ResponseResult result = await workflowResponses.CreateResponseAsync(ticket);
    Console.WriteLine("\n--- Customer reply (single trigger, server-routed) ---\n" + result.GetOutputText());
}
catch (Exception exc)
{
    // The Foundry "workflow agent" runtime is in preview and can 500 on
    // multi-step conditional flows. The workflow agent is still created and
    // visible in the portal; fall back to a client-driven run that performs the
    // SAME routing - this is where the if/else branching is demonstrated.
    Console.WriteLine("\n[preview] Single-trigger workflow run failed on the service:");
    Console.WriteLine("  " + exc.Message.Split('\n')[0]);
    Console.WriteLine("[preview] Falling back to a client-driven conditional run.\n");

    ProjectConversation conv = await projectClient.ProjectOpenAIClient
        .GetProjectConversationsClient()
        .CreateProjectConversationAsync();

    async Task<string> Run(string agentName, string text)
    {
        ProjectResponsesClient r = projectClient.ProjectOpenAIClient
            .GetProjectResponsesClientForAgent(agentName, conv);
        ResponseResult resp = await r.CreateResponseAsync(text);
        return resp.GetOutputText();
    }

    // Step 1: orchestrator classifies the ticket into a route.
    string routing = await Run("bakery-orchestrator", ticket);
    string route = ParseRoute(routing);
    Console.WriteLine($"--- Orchestrator (bakery-orchestrator) ---\nroute = {route}\n");

    // Step 2: BRANCH - exactly one department specialist handles the ticket.
    if (routeToAgent.TryGetValue(route, out string? specialistName))
    {
        string specialistAnswer = await Run(specialistName, ticket);
        Console.WriteLine($"--- Specialist ({specialistName}) ---\n{specialistAnswer}\n");

        // Step 3: synthesizer turns the specialist JSON into a warm reply.
        string synthInput = $"Customer question: {ticket}\nSpecialist answer: {specialistAnswer}";
        string reply = await Run("bakery-synthesizer", synthInput);
        Console.WriteLine("--- Customer reply (bakery-synthesizer) ---\n" + reply);
    }
    else
    {
        // else-branch: nothing matched - return the catch-all message.
        Console.WriteLine("--- Customer reply (out of scope) ---\n" + outOfScope);
    }
}

Console.WriteLine(
    $"\nDone. Open '{workflowName}' in the Foundry portal (Agents / Workflows) to " +
    "see the orchestrator -> conditional routing -> synthesizer graph.");


// ===== PORTAL OBSERVATIONS  (3 things to check) =====
//   1. Microsoft Foundry portal -> "Agents" -> "Workflows". Open
//      bakery-support-workflow. The graph shows the orchestrator fanning into a
//      ConditionGroup with one branch per department, then merging into the
//      synthesizer - the visual form of the if/else above.
//   2. Run the workflow in the Playground with different tickets (a menu
//      question, an order question, a complaint, an hours question) and watch a
//      DIFFERENT branch light up each time.
//   3. Open the workflow's "Traces" tab: the run is one trace; only the chosen
//      branch's agent span appears (the others never execute). Compare with Lab
//      11, where all four specialist spans appear and OVERLAP in time.
//
// ===== CHALLENGE  - Add a fifth branch + a guard =====
//   1. Add a "feedback" branch: create a bakery-feedback agent and a new
//      ConditionGroup case (route = "feedback") in workflow.yaml, then extend
//      routeToAgent above so the client path can reach it too.
//   2. Add a guard BEFORE the synthesizer: if the chosen specialist's JSON has
//      "escalate": true or "allergen_flag": true, prepend a "ESCALATED:" line
//      to the synthesizer input so the customer reply flags it.
//   3. Re-run with a complaint about an allergic reaction and confirm the guard
//      fires. Which branch did the orchestrator pick?
