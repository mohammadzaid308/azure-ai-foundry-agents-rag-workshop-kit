using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001, AAIP001

// Lab: Concurrent multi-agent (Frankie's Bakery) - fan-out / fan-in.
//
// Same Frankie's Bakery cast as Lab 10, but a different orchestration shape.
// Lab 10 (sequential) classifies a ticket and routes it to EXACTLY ONE
// department (if/else). This lab handles a complex ticket that touches EVERY
// department at once, so instead of choosing one branch we:
//
//   fan-out:  run menu + orders + complaints + hours specialists IN PARALLEL
//             (Task.WhenAll - they all see the same ticket simultaneously)
//   fan-in:   bakery-synthesizer merges all four answers into one reply
//
//                      +--> bakery-menu -------+
//   customer ticket ---+--> bakery-orders ------+--> bakery-synthesizer --> reply
//                      +--> bakery-complaints --+
//                      +--> bakery-hours -------+
//
// Declarative CSDL workflows execute sequentially, so the deployed workflow.yaml
// invokes the four specialists in order (it exists for portal visibility). The
// genuinely concurrent fan-out is the Task.WhenAll run below.

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string workflowName = Environment.GetEnvironmentVariable("FOUNDRY_CONCURRENT_WORKFLOW_NAME") ?? "bakery-concurrent-workflow";

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

// The four department specialists that fan out, plus the synthesizer that fans
// in. Same agents/knowledge as Lab 10 - only the orchestration differs.
(string Name, string Instructions)[] specialists =
{
    ("bakery-menu", InstructionsOf("MenuAgent.md")),
    ("bakery-orders", InstructionsOf("OrdersAgent.md")),
    ("bakery-complaints", InstructionsOf("ComplaintsAgent.md")),
    ("bakery-hours", InstructionsOf("HoursAgent.md")),
};

(string Name, string Instructions) synthesizer =
    ("bakery-synthesizer", InstructionsOf("SynthesizerAgent.md"));

// Create every agent so they are visible in the Foundry portal.
foreach ((string name, string instructions) in specialists.Append(synthesizer))
{
    DeclarativeAgentDefinition definition = new(model: modelDeployment) { Instructions = instructions };
    ProjectsAgentVersion agent = await admin.CreateAgentVersionAsync(
        agentName: name,
        options: new(definition));
    Console.WriteLine($"Created agent '{agent.Name}' (version {agent.Version})");
}

// Deploy the fan-out/fan-in shape as a workflow agent for portal visibility.
// The CSDL definition lives in workflow.yaml next to this lab.
string workflowYaml = await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "workflow.yaml"));

ProjectsAgentVersion workflow = await admin.CreateAgentVersionAsync(
    agentName: workflowName,
    options: new(WorkflowAgentDefinition.FromYaml(workflowYaml)),
    foundryFeatures: "WorkflowAgents=V1Preview"); // Workflow agents are a preview feature (opt-in required).
Console.WriteLine($"Created workflow agent '{workflow.Name}' (version {workflow.Version})");

// One complex ticket that touches all four departments at once: a wrong (nut)
// item on a placed order (complaint + order), a refund (order), a gluten-free
// menu question (menu), and a weekend-hours question (hours).
string ticket =
    "I pre-ordered a custom birthday cake (order #4821) for Saturday pickup at your " +
    "Midtown store, but it arrived topped with almonds even though I asked for nut-free " +
    "- my son is allergic, so I'd like a refund. Can you also tell me the price of a " +
    "6-inch gluten-free cake, and whether Midtown is open on Sunday if I need a replacement?";
Console.WriteLine("\n--- Customer ticket ---\n" + ticket);

async Task<string> Ask(string agentName, string text)
{
    ProjectResponsesClient r = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForAgent(agentName);
    ResponseResult resp = await r.CreateResponseAsync(text);
    return resp.GetOutputText();
}

// Try the single-trigger workflow agent first (server-orchestrated).
ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient()
    .CreateProjectConversationAsync();

try
{
    ProjectResponsesClient workflowResponses = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForAgent(workflowName, conversation);
    ResponseResult result = await workflowResponses.CreateResponseAsync(ticket);
    Console.WriteLine("\n--- Customer reply (single trigger) ---\n" + result.GetOutputText());
}
catch (Exception exc)
{
    // Workflow-agent runtime is preview and can be flaky for multi-step flows.
    // The workflow agent is still created and visible in the portal; fall back
    // to the genuinely concurrent client-driven run (fan-out with Task.WhenAll).
    Console.WriteLine("\n[preview] Single-trigger workflow run failed on the service:");
    Console.WriteLine("  " + exc.Message.Split('\n')[0]);
    Console.WriteLine("[preview] Falling back to a client-driven CONCURRENT run.\n");

    // Fan-out: all four department specialists analyze the SAME ticket at once.
    Task<string> menuTask = Ask("bakery-menu", ticket);
    Task<string> ordersTask = Ask("bakery-orders", ticket);
    Task<string> complaintsTask = Ask("bakery-complaints", ticket);
    Task<string> hoursTask = Ask("bakery-hours", ticket);
    await Task.WhenAll(menuTask, ordersTask, complaintsTask, hoursTask);

    string menu = await menuTask;
    string orders = await ordersTask;
    string complaints = await complaintsTask;
    string hours = await hoursTask;
    Console.WriteLine("--- Menu (bakery-menu) ---\n" + menu + "\n");
    Console.WriteLine("--- Orders (bakery-orders) ---\n" + orders + "\n");
    Console.WriteLine("--- Complaints (bakery-complaints) ---\n" + complaints + "\n");
    Console.WriteLine("--- Hours (bakery-hours) ---\n" + hours + "\n");

    // Fan-in: the synthesizer merges all four answers into one customer reply.
    string reply = await Ask(
        "bakery-synthesizer",
        $"Customer question:\n{ticket}\n\n" +
        $"Menu specialist:\n{menu}\n\n" +
        $"Orders specialist:\n{orders}\n\n" +
        $"Complaints specialist:\n{complaints}\n\n" +
        $"Hours specialist:\n{hours}");
    Console.WriteLine("--- Customer reply (bakery-synthesizer) ---\n" + reply);
}

Console.WriteLine(
    $"\nDone. Open '{workflowName}' in the Foundry portal (Agents / Workflows) " +
    "to see the bakery fan-out / fan-in flow.");


// ===== PORTAL OBSERVATION =====
//   Microsoft Foundry portal -> open this workflow/agent -> "Traces" tab (or the
//   project "Tracing" page). In the client-driven fallback the four specialists
//   run via Task.WhenAll, so their spans OVERLAP in time (they start at roughly
//   the same wall-clock moment). Compare to Lab 10 (sequential conditional
//   routing), where only ONE specialist span appears and it is chained before
//   the synthesizer. (Workflow tracing is preview; prompt-agent tracing is GA.)
//
// ===== CHALLENGE  - Make the fan-out resilient =====
//   Right now, if one specialist call throws, Task.WhenAll surfaces the
//   exception and the whole fan-out fails. Improve resilience:
//     1. Wrap the Ask(...) call in a helper that catches exceptions and returns
//        a fallback string, e.g.:
//          async Task<string> SafeAsk(string name, string text) {
//              try { return await Ask(name, text); }
//              catch (Exception e) { return $"FAILED: {e.Message.Split('\n')[0]}"; }
//          }
//     2. Use SafeAsk for all four specialists so one failure cannot sink the
//        others.
//     3. Count how many returned "FAILED:" and, if any did, retry just those
//        once before calling the synthesizer.
//   BONUS: cap each call with a timeout using
//   task.WaitAsync(TimeSpan.FromSeconds(10)) and treat a timeout as a failure.
