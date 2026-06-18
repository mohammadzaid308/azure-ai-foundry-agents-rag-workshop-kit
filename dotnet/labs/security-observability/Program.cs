using System.Diagnostics;
using Azure.Core;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4.1-mini";

// 1) Identity: DefaultAzureCredential works with az login locally and Managed Identity in Azure.
var credential = new Azure.Identity.DefaultAzureCredential();

// 2) Governance check: confirm we can obtain a token for the Foundry data plane (RBAC validation).
AccessToken token = await credential.GetTokenAsync(
    new TokenRequestContext(new[] { "https://ai.azure.com/.default" }), CancellationToken.None);
Console.WriteLine($"Token acquired, expires: {token.ExpiresOn:u}");

// 3) Observability: emit a trace span around the model call using System.Diagnostics.Activity.
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Workshop.Foundry",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity =>
        Console.WriteLine($"[trace] {activity.DisplayName} took {activity.Duration.TotalMilliseconds:F0} ms"),
};
ActivitySource.AddActivityListener(listener);
var activitySource = new ActivitySource("Workshop.Foundry");

AIProjectClient projectClient = new(new Uri(projectEndpoint), credential);
ProjectResponsesClient responses =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

using (Activity? activity = activitySource.StartActivity("foundry.responses.create"))
{
    activity?.SetTag("foundry.model", modelDeployment);
    ResponseResult response = await responses.CreateResponseAsync("Give one safety tip for AI agents.");
    Console.WriteLine(response.GetOutputText());
}

// To export traces to Application Insights, add the OpenTelemetry + Azure Monitor exporter
// packages and replace the ActivityListener above with the Azure Monitor distro.
