using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());

ProjectResponsesClient responseClient =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

// Stream the response token-by-token as it is generated
await foreach (StreamingResponseUpdate update in responseClient.CreateResponseStreamingAsync(
    "Explain what Azure AI Foundry is in three short sentences."))
{
    if (update is StreamingResponseOutputTextDeltaUpdate delta)
    {
        Console.Write(delta.Delta);
    }
}
Console.WriteLine();


// ===== PORTAL OBSERVATION =====
//   Foundry portal -> Monitoring -> Traces. Streaming calls appear as a single
//   trace entry; the output-text delta events are collapsed into one span.
//   Compare the latency here vs the non-streaming call in the responses lab -
//   streaming usually shows a LOWER time-to-first-byte but roughly the same
//   total time.
//
// ===== CHALLENGE =====
//   Measure time-to-first-token (TTFT) yourself:
//     1. Add `using System.Diagnostics;` at the top.
//     2. Start a stopwatch before the stream:  var sw = Stopwatch.StartNew();
//     3. Inside the loop, the first time you receive a delta, print
//        $"[TTFT {sw.ElapsedMilliseconds} ms]" and set a `bool first = true;`
//        flag so you only print it once.
//     4. After the loop, print sw.ElapsedMilliseconds as the total time.
//   Why it matters: TTFT is a key UX metric for chat apps and you can surface
//   it in Application Insights as a custom metric later.
