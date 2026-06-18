using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");
string modelDeployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT") ?? "gpt-4o";

AIProjectClient projectClient = new(new Uri(projectEndpoint), new Azure.Identity.DefaultAzureCredential());
ProjectResponsesClient responses =
    projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);

// Sequential pattern: the output of agent 1 becomes the input of agent 2.
string topic = "the benefits of retrieval-augmented generation";

// Agent 1: researcher produces bullet points
ResponseResult research = await responses.CreateResponseAsync(
    $"You are a researcher. List 4 concise bullet points about {topic}.");
string notes = research.GetOutputText();
Console.WriteLine("--- Researcher ---");
Console.WriteLine(notes);

// Agent 2: writer turns the notes into a short paragraph
ResponseResult article = await responses.CreateResponseAsync(
    "You are a technical writer. Turn these notes into one polished paragraph:\n\n" + notes);
Console.WriteLine("\n--- Writer ---");
Console.WriteLine(article.GetOutputText());
