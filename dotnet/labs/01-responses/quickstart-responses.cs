using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

// Create the Foundry project client (Azure AI Projects 2.x)
AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());

// Call the model directly via the Responses API (no agent needed)
ProjectResponsesClient responseClient =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

ResponseResult response = await responseClient.CreateResponseAsync(
    "What is the size of France in square miles?");

Console.WriteLine(response.GetOutputText());


// ===== PORTAL OBSERVATION =====
//   After running, go to: Foundry portal -> your project -> Monitoring ->
//   "Traces" (or "Activity"). You should see a new trace entry for the
//   responses call with its latency and the model name. Nothing shows if
//   APPLICATIONINSIGHTS_CONNECTION_STRING is not set, but the call still
//   appears in the project's audit log.
//
// ===== CHALLENGE  (complete the code, then re-run) =====
//   The lab asks one hard-coded question. Add a SECOND CreateResponseAsync call
//   that:
//     1. Asks the model to convert the answer to square KILOMETRES.
//     2. Passes the previous answer (response.GetOutputText()) as context in
//        the new prompt string.
//     3. Prints both answers side by side.
//   HINT:
//     ResponseResult response2 = await responseClient.CreateResponseAsync(
//         $"Convert '{response.GetOutputText()}' from square miles to square km.");
//     Console.WriteLine(response2.GetOutputText());
