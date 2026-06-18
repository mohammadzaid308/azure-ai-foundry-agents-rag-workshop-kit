using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4.1-mini";

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());
ProjectResponsesClient responses =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

string product = "a smart water bottle";

// Concurrent pattern: run independent agents in parallel, then aggregate.
var prosTask = responses.CreateResponseAsync($"You are an optimist. List 3 pros of {product}.");
var consTask = responses.CreateResponseAsync($"You are a skeptic. List 3 cons of {product}.");

await Task.WhenAll(prosTask, consTask);

ResponseResult pros = await prosTask;
ResponseResult cons = await consTask;

Console.WriteLine("--- Pros ---");
Console.WriteLine(pros.GetOutputText());
Console.WriteLine("\n--- Cons ---");
Console.WriteLine(cons.GetOutputText());

// Aggregator agent synthesizes both views
ResponseResult verdict = await responses.CreateResponseAsync(
    "You are a product analyst. Given these pros and cons, give a one-sentence verdict.\n\n" +
    $"PROS:\n{pros.GetOutputText()}\n\nCONS:\n{cons.GetOutputText()}");
Console.WriteLine("\n--- Verdict ---");
Console.WriteLine(verdict.GetOutputText());
