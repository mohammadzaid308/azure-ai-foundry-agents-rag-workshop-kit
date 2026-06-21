using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001, AAIP001

// Lab: Sequential multi-agent (Frankie's Bakery support pipeline).
//
//   bakery-intake  ->  bakery-specialist  ->  bakery-synthesizer
//   (classify+route)   (answer w/ knowledge)   (warm customer reply)
//
// Specialist knowledge is loaded from the local instruction files in
// ./data/bakery so the bakery's real menu/orders/hours/complaints rules drive
// the agent. Agent creation + runs reach Azure AI Foundry.

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

string specialistKnowledge = string.Join("\n\n", new[]
{
    ("Menu", "MenuAgent.md"),
    ("Orders", "OrdersAgent.md"),
    ("Complaints", "ComplaintsAgent.md"),
    ("Hours", "HoursAgent.md"),
}.Select(d => $"## {d.Item1}\n{InstructionsOf(d.Item2)}"));

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());
AgentAdministrationClient admin = projectClient.AgentAdministrationClient;

(string Name, string Instructions)[] agentSpecs =
{
    (
        "bakery-intake",
        "You are Frankie's Bakery support intake. Classify the customer's message and " +
        "restate it cleanly. Reply with exactly two lines:\n" +
        "Route: <menu | orders | complaints | hours | else>\n" +
        "Summary: <one-sentence restatement of what the customer needs>"
    ),
    (
        "bakery-specialist",
        "You are a Frankie's Bakery support specialist. Use the department knowledge " +
        "below to answer the customer's request accurately. If allergens or dietary " +
        "restrictions are involved, call them out explicitly.\n\n" + specialistKnowledge
    ),
    (
        "bakery-synthesizer",
        "You are a senior Frankie's Bakery agent. Rewrite the specialist's answer into a " +
        "warm, concise, on-brand reply addressed directly to the customer. Return only the reply."
    ),
};

foreach ((string name, string instructions) in agentSpecs)
{
    DeclarativeAgentDefinition definition = new(model: modelDeployment) { Instructions = instructions };
    ProjectsAgentVersion agent = await admin.CreateAgentVersionAsync(
        agentName: name,
        options: new(definition));
    Console.WriteLine($"Created agent '{agent.Name}' (version {agent.Version})");
}

// Deploy the pipeline as a *workflow agent* so the workflow itself shows up in
// the Foundry portal. The CSDL definition lives in workflow.yaml next to this lab.
string workflowYaml = await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "workflow.yaml"));

ProjectsAgentVersion workflow = await admin.CreateAgentVersionAsync(
    agentName: workflowName,
    options: new(WorkflowAgentDefinition.FromYaml(workflowYaml)),
    foundryFeatures: "WorkflowAgents=V1Preview"); // Workflow agents are a preview feature (opt-in required).
Console.WriteLine($"Created workflow agent '{workflow.Name}' (version {workflow.Version})");

string ticket =
    "Hi! My daughter is allergic to tree nuts. Is the almond croissant safe for her, " +
    "and what gluten-free options do you have?";
Console.WriteLine("\n--- Customer ticket ---\n" + ticket);

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
    // The Foundry "workflow agent" runtime is in preview and can be flaky for
    // multi-step flows. The workflow agent itself is still created and visible
    // in the portal; we fall back to driving the same agents over one shared
    // conversation so the lab still produces a result.
    Console.WriteLine("\n[preview] Single-trigger workflow run failed on the service:");
    Console.WriteLine("  " + exc.Message.Split('\n')[0]);
    Console.WriteLine("[preview] Falling back to a client-driven sequential run of the same agents.\n");

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

    string intake = await Run("bakery-intake", ticket);
    Console.WriteLine("--- Intake (bakery-intake) ---\n" + intake + "\n");
    string answer = await Run("bakery-specialist", $"Customer message:\n{ticket}\n\nIntake:\n{intake}");
    Console.WriteLine("--- Specialist (bakery-specialist) ---\n" + answer + "\n");
    string reply = await Run("bakery-synthesizer", answer);
    Console.WriteLine("--- Customer reply (bakery-synthesizer) ---\n" + reply);
}

Console.WriteLine(
    $"\nDone. Open '{workflowName}' in the Foundry portal (Agents / Workflows) " +
    "to see the intake -> specialist -> synthesizer workflow.");
