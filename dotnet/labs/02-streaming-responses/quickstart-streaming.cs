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
//   Like the responses lab this is a direct model call (no agent), so it won't
//   show on the Agents page. Watch usage under "Models + endpoints" (classic) /
//   "Build -> Models" (new Foundry) -> your deployment -> Metrics, or connect
//   Application Insights (Lab 13) to capture the span under "Tracing".
//   The streaming win is client-side: note the LOWER time-to-first-token here vs
//   the non-streaming responses call - total time is roughly the same.
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
