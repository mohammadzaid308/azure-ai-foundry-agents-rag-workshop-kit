using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "openapi-agent";
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

// Load the OpenAPI spec and register it as a tool (public API, anonymous auth)
BinaryData spec = BinaryData.FromBytes(
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "weather-openapi.json")));
var openapi = new OpenAPITool(
    new OpenApiFunctionDefinition("getWeather", spec, new OpenAPIAnonymousAuthenticationDetails()));

DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "Use the weather API tool to answer weather questions. Always call it with format=j1 so you get JSON, then summarize the current conditions.",
};
definition.Tools.Add(openapi);

ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName, options: new(definition));

ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient().CreateProjectConversationAsync();
ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(agentName, conversation);

ResponseResult response = await responses.CreateResponseAsync("What is the weather in London right now?");
Console.WriteLine(response.GetOutputText());
