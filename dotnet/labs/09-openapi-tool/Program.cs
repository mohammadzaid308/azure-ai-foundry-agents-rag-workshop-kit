using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "openapi-agent";
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());

// Load the OpenAPI spec and register it as a tool (public API, anonymous auth)
BinaryData spec = BinaryData.FromBytes(
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "weather-openapi.json")));
var openapi = new OpenAPITool(
    new OpenApiFunctionDefinition("getWeather", spec, new OpenAPIAnonymousAuthenticationDetails()));

DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "Use the weather API tool to answer weather questions. Always call it with format=j1 so you get JSON, then summarize the current conditions.",
};
definition.Tools.Add(openapi);

ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName, options: new(definition));

ProjectConversation conversation = await projectClient.ProjectOpenAIClient
    .GetProjectConversationsClient().CreateProjectConversationAsync();
ProjectResponsesClient responses = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(agentName, conversation);

ResponseResult response = await responses.CreateResponseAsync("What is the weather in London right now?");
Console.WriteLine(response.GetOutputText());


// ===== PORTAL OBSERVATION =====
//   Microsoft Foundry portal -> "Agents" -> open this agent -> "Playground" ->
//   ask a weather question. Expand "Show details" (and the agent's "Traces" tab)
//   to see:
//     * The OpenAPI operation the model resolved the call from.
//     * The exact HTTP request the model formed (URL + query params).
//     * The raw JSON response before the model summarized it.
//   Because this IS an agent (unlike the client-side function loop in the
//   agent-function lab), the call runs server-side and shows in the agent traces.
//
// ===== CHALLENGE  - Add a second OpenAPI tool =====
//   A tiny free API: https://dog.ceo/api/breeds/list/all
//   1. Write a minimal OpenAPI 3.0 JSON spec for GET /api/breeds/list/all
//      (one path, no parameters) and save it next to weather-openapi.json.
//   2. Build a second tool the same way:
//        var dogs = new OpenAPITool(new OpenApiFunctionDefinition(
//            "listDogBreeds", dogSpec, new OpenAPIAnonymousAuthenticationDetails()));
//        definition.Tools.Add(dogs);
//   3. Ask: "Is it raining in London, and if so suggest a dog breed that
//      matches the mood?" Watch the model decide WHICH tool to call (and in
//      what order) in the portal tool-call trace.
