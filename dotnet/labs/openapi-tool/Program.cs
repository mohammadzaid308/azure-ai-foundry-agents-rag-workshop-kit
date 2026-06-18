using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "openapi-agent";
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string openApiConnectionId = Environment.GetEnvironmentVariable("FOUNDRY_OPENAPI_CONNECTION_ID")
    ?? throw new InvalidOperationException("FOUNDRY_OPENAPI_CONNECTION_ID is required.");

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

// Load the OpenAPI spec and register it as a tool, authenticated via a Foundry connection
BinaryData spec = BinaryData.FromBytes(
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "weather-openapi.json")));
var auth = new OpenApiProjectConnectionAuthenticationDetails(
    new OpenApiProjectConnectionSecurityScheme(openApiConnectionId));
var openapi = new OpenAPITool(new OpenApiFunctionDefinition("getWeather", spec, auth));

DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "Use the weather API tool to answer weather questions.",
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
