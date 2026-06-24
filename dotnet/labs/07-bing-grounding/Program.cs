using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "grounding-agent";
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";
string bingConnectionId = Environment.GetEnvironmentVariable("FOUNDRY_BING_CONNECTION_ID")
    ?? throw new InvalidOperationException("FOUNDRY_BING_CONNECTION_ID is required for Bing grounding.");

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

// Attach a Bing grounding tool backed by a Foundry connection
var bing = new BingGroundingTool(new BingGroundingSearchToolOptions(
    new[] { new BingGroundingSearchConfiguration(bingConnectionId) }));

DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "You answer questions using up-to-date web results. Cite sources when possible.",
};
definition.Tools.Add(bing);

ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName, options: new(definition));

ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient().CreateProjectConversationAsync();
ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(agentName, conversation);

ResponseResult response = await responses.CreateResponseAsync(
    "What are the latest announcements about Azure AI Foundry?");
Console.WriteLine(response.GetOutputText());

// Print source URLs so they are clickable in the terminal.
// Bing grounding attaches UriCitation annotations to the message output.
var citations = new List<(string Title, string Url)>();
foreach (ResponseItem item in response.OutputItems)
{
    if (item is MessageResponseItem message)
    {
        foreach (ResponseContentPart part in message.Content)
        {
            foreach (ResponseMessageAnnotation annotation in part.OutputTextAnnotations)
            {
                if (annotation is UriCitationMessageAnnotation citation)
                {
                    string url = citation.Uri?.ToString() ?? string.Empty;
                    string title = string.IsNullOrEmpty(citation.Title) ? url : citation.Title;
                    if (url.Length > 0 && !citations.Any(c => c.Title == title && c.Url == url))
                    {
                        citations.Add((title, url));
                    }
                }
            }
        }
    }
}

if (citations.Count > 0)
{
    Console.WriteLine("\nSources:");
    for (int i = 0; i < citations.Count; i++)
    {
        Console.WriteLine($"  [{i + 1}] {citations[i].Title}");
        Console.WriteLine($"      {citations[i].Url}");
    }
}
