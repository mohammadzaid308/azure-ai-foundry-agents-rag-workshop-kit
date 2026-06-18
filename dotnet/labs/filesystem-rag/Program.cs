using System.Text.RegularExpressions;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4.1-mini";

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());

string question = "What is our policy for travel expense approval and receipts?";
string context = RetrieveContext(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data"), question);

ProjectResponsesClient responseClient =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

ResponseResult response = await responseClient.CreateResponseAsync(
    "You are an assistant that must answer using only the provided context. " +
    "If the answer is not in context, say you do not know.\n\n" +
    $"Context:\n{context}\n\nQuestion: {question}");

Console.WriteLine(response.GetOutputText());

static string RetrieveContext(string dataDir, string question)
{
    var questionTokens = Tokenize(question);
    var scored = new List<(int Score, string Name, string Content)>();

    foreach (string filePath in Directory.GetFiles(dataDir, "*.md"))
    {
        string content = File.ReadAllText(filePath);
        int score = Tokenize(content).Intersect(questionTokens).Count();
        scored.Add((score, Path.GetFileName(filePath), content));
    }

    var top = scored.OrderByDescending(x => x.Score).Take(2);
    return string.Join("\n\n", top.Select(x => $"[{x.Name}] (score={x.Score})\n{x.Content}"));
}

static HashSet<string> Tokenize(string text)
{
    return Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
        .Select(m => m.Value)
        .ToHashSet();
}
