using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001, AAIP001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string workflowName = Environment.GetEnvironmentVariable("FOUNDRY_WORKFLOW_NAME") ?? "support-ticket-workflow";

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());
AgentAdministrationClient admin = projectClient.AgentAdministrationClient;

//   support-triage  ->  support-resolver  ->  support-reply
//   (classify)          (figure out fix)      (write the customer reply)
//
// This is the "sequential" multi-agent pattern: a clear hand-off from one
// specialist to the next.
(string Name, string Instructions)[] agentSpecs =
{
    (
        "support-triage",
        "You are a customer-support triage agent. Read the customer's message and " +
        "classify it. Reply with exactly two lines:\n" +
        "Category: <Billing | Technical | Account | Other>\n" +
        "Priority: <Low | Medium | High>"
    ),
    (
        "support-resolver",
        "You are a customer-support resolution agent. Given the customer's message " +
        "and its triage, write clear, numbered steps that will resolve the issue."
    ),
    (
        "support-reply",
        "You are a senior support agent. Rewrite the resolution into a warm, " +
        "empathetic reply addressed to the customer. Keep it concise and " +
        "professional. Return only the reply."
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

// Deploy the pipeline as a *workflow agent* so the workflow itself (triage ->
// resolver -> reply) shows up in the Foundry portal, not just the individual
// agents. The CSDL definition lives in workflow.yaml next to this lab.
string workflowYaml = await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "workflow.yaml"));

ProjectsAgentVersion workflow = await admin.CreateAgentVersionAsync(
    agentName: workflowName,
    options: new(WorkflowAgentDefinition.FromYaml(workflowYaml)),
    foundryFeatures: "WorkflowAgents=V1Preview"); // Workflow agents are a preview feature (opt-in required).
Console.WriteLine($"Created workflow agent '{workflow.Name}' (version {workflow.Version})");

string ticket =
    "Hi, I was charged twice for my subscription this month, and the app keeps " +
    "crashing whenever I open the billing page. Can you help?";
Console.WriteLine("\n--- Customer ticket ---\n" + ticket);

// Run the whole pipeline with ONE trigger: a single request to the workflow
// agent. Foundry orchestrates triage -> resolver -> reply server-side.
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

    string triage = await Run("support-triage", ticket);
    Console.WriteLine("--- Triage (support-triage) ---\n" + triage + "\n");
    string resolution = await Run("support-resolver", $"Customer message:\n{ticket}\n\nTriage:\n{triage}");
    Console.WriteLine("--- Resolution (support-resolver) ---\n" + resolution + "\n");
    string reply = await Run("support-reply", resolution);
    Console.WriteLine("--- Customer reply (support-reply) ---\n" + reply);
}

Console.WriteLine(
    $"\nDone. Open '{workflowName}' in the Foundry portal (Agents / Workflows) " +
    "to see the triage -> resolver -> reply workflow.");
