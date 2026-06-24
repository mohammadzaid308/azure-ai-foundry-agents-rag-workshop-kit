using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME")
    ?? throw new InvalidOperationException("FOUNDRY_AGENT_NAME is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());

// Ensure the agent exists (create or roll a new version)
DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "You are a helpful assistant for workshop questions.",
};
ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName,
    options: new(definition));

// Create a conversation so history is preserved across turns
ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient()
    .CreateProjectConversationAsync();

// Bind a responses client to this agent + conversation
ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(agentName, conversation);

ResponseResult first = await responses.CreateResponseAsync(
    "What is the size of France in square miles?");
Console.WriteLine($"Agent: {first.GetOutputText()}");

// Follow-up question in the same conversation (uses prior context)
ResponseResult second = await responses.CreateResponseAsync(
    "And what is its capital city?");
Console.WriteLine($"Agent: {second.GetOutputText()}");


// ===== PORTAL OBSERVATION =====
//   Microsoft Foundry portal -> "Agents" -> open your agent -> "Playground".
//   Use the thread / Thread logs view to inspect the conversation: BOTH turns
//   (country size + capital city) live in ONE thread, which is why the follow-up
//   "And what is its capital city?" works without repeating the country.
//   Open the agent's "Traces" tab to see a span per turn (Traces are GA for
//   prompt agents). The old standalone "Conversations" page is gone - threads
//   are viewed from the agent's Playground / Traces tabs.
//
// ===== CHALLENGE  - Add a third turn + inspect the thread =====
//   1. Add a THIRD CreateResponseAsync call on the same `responses` client:
//        "What language do people speak there, and is it an EU member?"
//      It should still resolve "there" from conversation context.
//   2. Print the conversation Id (conversation.Id) so you can find it fast in
//      the portal Conversations view.
//   3. BONUS: list the stored messages via the conversations client
//      (projectClient.ProjectOpenAIClient.GetProjectConversationsClient())
//      and print each role + text to see how context grows per turn.
