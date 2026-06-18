using System.Diagnostics;
using Azure.Core;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

// 1) Identity: DefaultAzureCredential works with az login locally and Managed Identity in Azure.
var credential = new Azure.Identity.DefaultAzureCredential();

// 2) Governance check: confirm we can obtain a token for the Foundry data plane (RBAC validation).
AccessToken token = await credential.GetTokenAsync(
    new TokenRequestContext(new[] { "https://ai.azure.com/.default" }), CancellationToken.None);
Console.WriteLine($"Token acquired, expires: {token.ExpiresOn:u}");

// 3) Observability: emit trace spans around the model calls using System.Diagnostics.Activity.
var activitySource = new ActivitySource("Workshop.Foundry");

// Local console listener so spans are visible during the workshop even without an exporter.
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Workshop.Foundry",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity =>
        Console.WriteLine($"[trace] {activity.DisplayName} took {activity.Duration.TotalMilliseconds:F0} ms"),
};
ActivitySource.AddActivityListener(listener);

// Optional: if APPLICATIONINSIGHTS_CONNECTION_STRING is set, export the same spans
// to Application Insights via the Azure Monitor OpenTelemetry exporter.
string? appInsights = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
TracerProvider? tracerProvider = null;
if (!string.IsNullOrWhiteSpace(appInsights))
{
    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource("Workshop.Foundry")
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("workshop-foundry-dotnet"))
        .AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsights)
        .Build();
    Console.WriteLine("Azure Monitor tracing enabled.");
}

AIProjectClient projectClient = new(new Uri(projectEndpoint), credential);
ProjectResponsesClient responses =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

// Nested spans: a parent "handle" operation wraps two child model calls
// (draft -> refine). The parent/child relationship is preserved in the trace
// so you can see the full operation tree in the portal.
using (Activity? root = activitySource.StartActivity("support.request.handle"))
{
    root?.SetTag("workshop.lab", "security-observability");
    root?.SetTag("foundry.model", modelDeployment);

    string tip;
    using (Activity? draft = activitySource.StartActivity("foundry.responses.draft"))
    {
        draft?.SetTag("foundry.step", "draft");
        ResponseResult first = await responses.CreateResponseAsync("Give one safety tip for AI agents.");
        tip = first.GetOutputText();
        draft?.SetTag("foundry.output.length", tip.Length);
        Console.WriteLine($"Draft: {tip}");
    }

    using (Activity? refine = activitySource.StartActivity("foundry.responses.refine"))
    {
        refine?.SetTag("foundry.step", "refine");
        ResponseResult second = await responses.CreateResponseAsync(
            $"Rewrite this as a single concise checklist item: {tip}");
        Console.WriteLine($"Refined: {second.GetOutputText()}");
    }
}

// Flush telemetry so spans are exported before the process exits.
if (tracerProvider is not null)
{
    tracerProvider.ForceFlush();
    tracerProvider.Dispose();
    Console.WriteLine("Telemetry flushed to Azure Monitor.");
}

