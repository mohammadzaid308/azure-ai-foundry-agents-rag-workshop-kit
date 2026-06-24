using Azure.AI.Projects;
using Azure.AI.Projects.Agents;

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME")
    ?? throw new InvalidOperationException("FOUNDRY_AGENT_NAME is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

// Create the Foundry project client (Azure AI Projects 2.x)
AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new Azure.Identity.DefaultAzureCredential());

// Define a declarative prompt agent (model + instructions)
DeclarativeAgentDefinition definition = new(model: modelDeployment)
{
    Instructions = "You are a helpful assistant that answers general questions.",
};

// Create a new versioned agent in the project
ProjectsAgentVersion agent = projectClient.AgentAdministrationClient.CreateAgentVersion(
    agentName: agentName,
    options: new(definition));

Console.WriteLine($"Agent created (id: {agent.Id}, name: {agent.Name}, version: {agent.Version})");


// ===== PORTAL OBSERVATION =====
//   Microsoft Foundry portal (https://ai.azure.com) -> "Agents" (classic) /
//   "Build -> Agents" (new Foundry). Find the agent by FOUNDRY_AGENT_NAME and
//   note:
//     * Version number (starts at 1; re-running increments it).
//     * Model deployment name linked to it.
//     * System prompt / instructions as stored server-side.
//   The agent page has tabs - Playground, Traces, Monitor, Evaluation - used in
//   later labs. Edit the instructions in the portal and save to create a new
//   version you can see without running code.
//
// ===== CHALLENGE =====
//   A DeclarativeAgentDefinition can carry more than instructions.
//   1. Set a deterministic temperature on the definition initializer, e.g.:
//          DeclarativeAgentDefinition definition = new(model: modelDeployment)
//          {
//              Instructions = "...",
//              Temperature = 0.2f,   // explore the initializer for available knobs
//          };
//   2. Add a SECOND CreateAgentVersion call with a different agentName
//      (e.g. "creative-agent") and Temperature = 1.0f.
//   3. Print both agents' Id and Version, then compare the two agents in the
//      portal Agents list.
