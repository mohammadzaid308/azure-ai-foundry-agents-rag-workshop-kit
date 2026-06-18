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

// 1) Declare a local tool the model can call
ResponseTool weatherTool = ResponseTool.CreateFunctionTool(
    functionName: "get_weather",
    functionParameters: BinaryData.FromString(
        "{\"type\":\"object\",\"properties\":{\"location\":{\"type\":\"string\"}},\"required\":[\"location\"]}"),
    strictModeEnabled: false,
    functionDescription: "Get the current weather for a city.");

string GetWeather(string location) => $"It is 21C and sunny in {location}.";

var options = new CreateResponseOptions(modelDeployment,
    new List<ResponseItem> { ResponseItem.CreateUserMessageItem("What is the weather in Seattle?") });
options.Tools.Add(weatherTool);

// 2) First call: the model decides to call the function
ResponseResult response = await responses.CreateResponseAsync(options);

// 3) Execute any requested function calls and feed the results back
bool hadToolCall = false;
foreach (ResponseItem item in response.OutputItems)
{
    if (item is FunctionCallResponseItem call && call.FunctionName == "get_weather")
    {
        hadToolCall = true;
        using var doc = System.Text.Json.JsonDocument.Parse(call.FunctionArguments);
        string location = doc.RootElement.GetProperty("location").GetString() ?? "";
        options.InputItems.Add(call);
        options.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(call.CallId, GetWeather(location)));
    }
}

// 4) Second call: the model produces the final grounded answer
ResponseResult final = hadToolCall ? await responses.CreateResponseAsync(options) : response;
Console.WriteLine(final.GetOutputText());
