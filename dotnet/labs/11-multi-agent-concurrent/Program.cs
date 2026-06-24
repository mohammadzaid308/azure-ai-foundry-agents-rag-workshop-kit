using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001, AAIP001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string workflowName = Environment.GetEnvironmentVariable("FOUNDRY_CONCURRENT_WORKFLOW_NAME") ?? "support-escalation-workflow";

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());
AgentAdministrationClient admin = projectClient.AgentAdministrationClient;

// A day-to-day customer-support scenario handled with the "concurrent" pattern:
// three specialist agents look at the same complaint *in parallel* (fan-out),
// then a supervisor agent merges their findings into one reply (fan-in).
string complaint =
    "I've been a customer for 3 years and I'm really frustrated. My internet has " +
    "dropped every evening this week, and on top of that you charged me a $15 " +
    "'equipment fee' I never agreed to. I want this sorted out today.";

(string Name, string Instructions)[] specialists =
{
    (
        "support-sentiment",
        "You are a customer-sentiment analyst. In 1-2 sentences, describe the " +
        "customer's emotion and how urgent this is."
    ),
    (
        "support-technical",
        "You are a technical support specialist. Identify the likely technical " +
        "problem and list concrete troubleshooting steps."
    ),
    (
        "support-billing",
        "You are a billing specialist. Identify any billing or refund issue and " +
        "state exactly what action should be taken."
    ),
};

(string Name, string Instructions) supervisor =
(
    "support-supervisor",
    "You are a support supervisor. Using the specialist analyses, write one " +
    "warm, professional reply to the customer that addresses every point, then " +
    "add a final line 'Recommended internal action: ...'."
);

// Create every agent so they are visible in the Foundry portal.
foreach ((string name, string instructions) in specialists.Append(supervisor))
{
    DeclarativeAgentDefinition definition = new(model: modelDeployment) { Instructions = instructions };
    ProjectsAgentVersion agent = await admin.CreateAgentVersionAsync(
        agentName: name,
        options: new(definition));
    Console.WriteLine($"Created agent '{agent.Name}' (version {agent.Version})");
}

// Deploy the escalation as a *workflow agent* so the flow (sentiment,
// technical, billing -> supervisor) shows up in the Foundry portal, not just
// the individual agents. The CSDL definition lives in workflow.yaml.
string workflowYaml = await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "workflow.yaml"));

ProjectsAgentVersion workflow = await admin.CreateAgentVersionAsync(
    agentName: workflowName,
    options: new(WorkflowAgentDefinition.FromYaml(workflowYaml)),
    foundryFeatures: "WorkflowAgents=V1Preview"); // Workflow agents are a preview feature (opt-in required).
Console.WriteLine($"Created workflow agent '{workflow.Name}' (version {workflow.Version})");

Console.WriteLine("\n--- Customer complaint ---\n" + complaint);

// Run the escalation with ONE trigger: a single request to the workflow agent.
// Foundry orchestrates the specialists -> supervisor server-side.
ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient()
    .CreateProjectConversationAsync();

async Task<string> Ask(string agentName, string text)
{
    ProjectResponsesClient r = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForAgent(agentName);
    ResponseResult resp = await r.CreateResponseAsync(text);
    return resp.GetOutputText();
}

try
{
    ProjectResponsesClient workflowResponses = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForAgent(workflowName, conversation);
    ResponseResult result = await workflowResponses.CreateResponseAsync(complaint);
    Console.WriteLine("\n--- Supervisor reply (single trigger) ---\n" + result.GetOutputText());
}
catch (Exception exc)
{
    // The Foundry "workflow agent" runtime is in preview and can be flaky for
    // multi-step flows. The workflow agent is still created and visible in the
    // portal; we fall back to the genuinely concurrent client-driven run
    // (fan-out with Task.WhenAll).
    Console.WriteLine("\n[preview] Single-trigger workflow run failed on the service:");
    Console.WriteLine("  " + exc.Message.Split('\n')[0]);
    Console.WriteLine("[preview] Falling back to a client-driven concurrent run of the same agents.\n");

    // Fan-out: all three specialists analyze the complaint at the same time.
    Task<string> sentimentTask = Ask("support-sentiment", complaint);
    Task<string> technicalTask = Ask("support-technical", complaint);
    Task<string> billingTask = Ask("support-billing", complaint);
    await Task.WhenAll(sentimentTask, technicalTask, billingTask);

    string sentiment = await sentimentTask;
    string technical = await technicalTask;
    string billing = await billingTask;
    Console.WriteLine("--- Sentiment (support-sentiment) ---\n" + sentiment + "\n");
    Console.WriteLine("--- Technical (support-technical) ---\n" + technical + "\n");
    Console.WriteLine("--- Billing (support-billing) ---\n" + billing + "\n");

    // Fan-in: the supervisor merges everything into one customer reply.
    string reply = await Ask(
        "support-supervisor",
        $"Customer complaint:\n{complaint}\n\n" +
        $"Sentiment analysis:\n{sentiment}\n\n" +
        $"Technical analysis:\n{technical}\n\n" +
        $"Billing analysis:\n{billing}");
    Console.WriteLine("--- Supervisor reply (support-supervisor) ---\n" + reply);
}

Console.WriteLine(
    $"\nDone. Open '{workflowName}' in the Foundry portal (Agents / Workflows) " +
    "to see the concurrent support escalation flow.");


// ===== PORTAL OBSERVATION =====
//   Microsoft Foundry portal -> open this workflow/agent -> "Traces" tab (or the
//   project "Tracing" page). In the client-driven fallback the three specialists
//   run via Task.WhenAll, so their spans OVERLAP in time (they start at roughly
//   the same wall-clock moment). Compare to the sequential lab where spans are
//   chained end-to-end. (Workflow tracing is preview; prompt-agent tracing is GA.)
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
//     2. Use SafeAsk for all three specialists so one failure cannot sink the
//        others.
//     3. Count how many returned "FAILED:" and, if any did, retry just those
//        once before calling the supervisor.
//   BONUS: cap each call with a timeout using
//   task.WaitAsync(TimeSpan.FromSeconds(10)) and treat a timeout as a failure.
