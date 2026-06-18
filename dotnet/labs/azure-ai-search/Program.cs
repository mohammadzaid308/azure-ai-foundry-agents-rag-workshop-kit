using System.ClientModel.Primitives;
using System.Text.Json;
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

// The Azure AI Search grounding tool tags citations with internal references
// (doc_0, doc_1, ...). Resolve them to real hotel names so the answer is
// readable in the terminal. The retrieved documents are returned, in order, on
// the azure_ai_search_call_output item, with the hotel name as the first line
// of each document's content field.
var hotelNames = new List<string>();
foreach (ResponseItem item in response.OutputItems)
{
    BinaryData itemJson = ModelReaderWriter.Write(item);
    using JsonDocument outer = JsonDocument.Parse(itemJson);
    if (!outer.RootElement.TryGetProperty("type", out JsonElement typeElement)
        || typeElement.GetString() != "azure_ai_search_call_output"
        || !outer.RootElement.TryGetProperty("output", out JsonElement outputElement))
    {
        continue;
    }

    try
    {
        using JsonDocument inner = JsonDocument.Parse(outputElement.GetString() ?? "{}");
        if (inner.RootElement.TryGetProperty("documents", out JsonElement documents))
        {
            foreach (JsonElement document in documents.EnumerateArray())
            {
                string content = document.TryGetProperty("content", out JsonElement contentElement)
                    ? contentElement.GetString() ?? string.Empty
                    : string.Empty;
                hotelNames.Add(content.Split('\n')[0].Trim());
            }
        }
    }
    catch (JsonException)
    {
        // Leave hotelNames as-is; we fall back to the raw doc_N reference below.
    }
}

string DocLabel(string title)
{
    if (title.StartsWith("doc_")
        && int.TryParse(title["doc_".Length..], out int docIndex)
        && docIndex >= 0 && docIndex < hotelNames.Count
        && hotelNames[docIndex].Length > 0)
    {
        return hotelNames[docIndex];
    }
    return title;
}

// Collect every citation span (where it appears in the text + which hotel).
var spans = new List<(int Start, int End, string Label)>();
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
                    spans.Add((citation.StartIndex, citation.EndIndex, DocLabel(citation.Title)));
                }
            }
        }
    }
}

// Number the unique sources by first appearance in the text.
var sources = new List<string>();
foreach ((int Start, int End, string Label) span in spans.OrderBy(s => s.Start))
{
    if (!sources.Contains(span.Label))
    {
        sources.Add(span.Label);
    }
}

// Replace the opaque inline markers with [n] (work from the end so indices stay valid).
string text = response.GetOutputText();
foreach ((int Start, int End, string Label) span in spans.OrderByDescending(s => s.Start))
{
    if (span.Start < 0 || span.End > text.Length || span.Start > span.End)
    {
        continue;
    }
    int number = sources.IndexOf(span.Label) + 1;
    text = string.Concat(text.AsSpan(0, span.Start), $"[{number}]", text.AsSpan(span.End));
}

Console.WriteLine(text);
if (sources.Count > 0)
{
    Console.WriteLine("\nCitations:");
    for (int i = 0; i < sources.Count; i++)
    {
        Console.WriteLine($"  [{i + 1}] {sources[i]}");
    }
}
