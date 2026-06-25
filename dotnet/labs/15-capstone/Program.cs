using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

// Capstone starter: combine what you learned (agent + tools + grounding + conversation).
// TODO: pick a scenario, add the tools your scenario needs, and iterate.

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "capstone-agent";
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "You are the capstone agent. Replace these instructions for your scenario.",
};
// TODO: definition.Tools.Add(...) to add Bing grounding, Azure AI Search, OpenAPI, or functions.

ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName, options: new(definition));
Console.WriteLine($"Capstone agent ready: {agent.Name} (v{agent.Version})");

ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient().CreateProjectConversationAsync();
ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(agentName, conversation);

ResponseResult response = await responses.CreateResponseAsync("Introduce yourself and your capabilities.");
Console.WriteLine(response.GetOutputText());
