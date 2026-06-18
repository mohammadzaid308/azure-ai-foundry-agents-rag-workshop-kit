using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "search-agent";
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string searchConnectionId = Environment.GetEnvironmentVariable("FOUNDRY_SEARCH_CONNECTION_ID")
    ?? throw new InvalidOperationException("FOUNDRY_SEARCH_CONNECTION_ID is required.");
string indexName = Environment.GetEnvironmentVariable("FOUNDRY_SEARCH_INDEX_NAME") ?? "workshop-index";

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

// Ground the agent on an Azure AI Search index
var index = new AzureAISearchToolIndex
{
    ProjectConnectionId = searchConnectionId,
    IndexName = indexName,
    QueryType = AzureAISearchQueryType.VectorSemanticHybrid,
    TopK = 5,
};
var search = new AzureAISearchTool(new AzureAISearchToolOptions(new[] { index }));

DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "Answer questions about hotels using only the provided Azure AI Search index. Cite hotel names.",
};
definition.Tools.Add(search);

ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName, options: new(definition));

ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient().CreateProjectConversationAsync();
ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(agentName, conversation);

ResponseResult response = await responses.CreateResponseAsync(
    "Which hotels are rated highly and have a pool? List a few with their ratings.");
Console.WriteLine(response.GetOutputText());
