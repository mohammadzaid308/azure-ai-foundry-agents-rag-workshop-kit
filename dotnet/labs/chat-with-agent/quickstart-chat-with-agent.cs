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
