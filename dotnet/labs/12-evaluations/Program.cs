using System.Text.Json;
using System.Text.RegularExpressions;

// Lab: Offline evaluation harness (Frankie's Bakery quality gates).
//
// The managed Azure AI Foundry Evaluations service (coherence/violence/etc. via
// LLM judges) is driven from Python in the companion lab. This .NET lab shows the
// same *idea* with deterministic, offline metrics over the same dataset
// (data/bakery_eval_dataset.jsonl) so it runs with NO Azure calls: token-overlap
// F1 and exact-match against ground_truth. Use it as a fast pre-flight gate in CI
// before paying for LLM-judge evaluations.

string datasetPath = Path.Combine(AppContext.BaseDirectory, "data", "bakery_eval_dataset.jsonl");
if (!File.Exists(datasetPath))
    throw new FileNotFoundException($"Dataset not found: {datasetPath}");

double f1Total = 0;
int exactMatches = 0;
int rows = 0;

Console.WriteLine($"{"Query",-50} {"F1",6} {"Exact",6}");
Console.WriteLine(new string('-', 64));

foreach (string line in File.ReadLines(datasetPath))
{
    if (string.IsNullOrWhiteSpace(line)) continue;
    using JsonDocument doc = JsonDocument.Parse(line);
    JsonElement root = doc.RootElement;
    string query = root.GetProperty("query").GetString() ?? "";
    string response = root.GetProperty("response").GetString() ?? "";
    string groundTruth = root.GetProperty("ground_truth").GetString() ?? "";

    double f1 = TokenF1(response, groundTruth);
    bool exact = Normalize(response) == Normalize(groundTruth);

    f1Total += f1;
    if (exact) exactMatches++;
    rows++;

    string shortQuery = query.Length > 48 ? query[..48] : query;
    Console.WriteLine($"{shortQuery,-50} {f1,6:F2} {(exact ? "yes" : "no"),6}");
}

Console.WriteLine(new string('-', 64));
Console.WriteLine($"Rows: {rows}");
Console.WriteLine($"Mean token-F1:  {(rows == 0 ? 0 : f1Total / rows):F3}");
Console.WriteLine($"Exact matches:  {exactMatches}/{rows}");

// A simple gate you could enforce in CI.
double meanF1 = rows == 0 ? 0 : f1Total / rows;
const double threshold = 0.30;
Console.WriteLine(meanF1 >= threshold
    ? $"\nPASS: mean F1 {meanF1:F3} >= {threshold:F2}"
    : $"\nFAIL: mean F1 {meanF1:F3} < {threshold:F2}");

static string Normalize(string text) => Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9 ]", "").Trim();

static List<string> Tokenize(string text) =>
    Regex.Matches(Normalize(text), "[a-z0-9]+").Select(m => m.Value).ToList();

static double TokenF1(string prediction, string reference)
{
    List<string> pred = Tokenize(prediction);
    List<string> reff = Tokenize(reference);
    if (pred.Count == 0 || reff.Count == 0) return 0;

    var refCounts = reff.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
    int overlap = 0;
    foreach (string token in pred)
    {
        if (refCounts.TryGetValue(token, out int c) && c > 0)
        {
            overlap++;
            refCounts[token] = c - 1;
        }
    }
    if (overlap == 0) return 0;
    double precision = (double)overlap / pred.Count;
    double recall = (double)overlap / reff.Count;
    return 2 * precision * recall / (precision + recall);
}


// ===== PORTAL OBSERVATION =====
//   This .NET lab is the OFFLINE pre-flight gate (deterministic token-F1 and
//   exact-match, no Azure calls). The managed LLM-judge evaluation runs in the
//   Python evaluations lab. After you run that one, go to Microsoft Foundry
//   portal -> "Evaluation" (classic) / "Build -> Evaluations" (new Foundry) to
//   see per-row relevance / groundedness / fluency scores. Mental model: run THIS
//   cheap gate in CI first, and only pay for the LLM-judge eval when it passes.
//
// ===== CHALLENGE  - Add a custom "tone" metric =====
//   Add a third, offline metric that scores how "friendly" each response is.
//     1. Write a static method `double ToneScore(string answer)` that returns
//        a 0..1 score, e.g. based on counting exclamation marks and positive
//        words ("great","happy","delicious","fresh","enjoy"), capped at 1.0.
//     2. Compute it per row, accumulate a running total, and add a "Tone"
//        column to the table header and each printed row.
//     3. Print the mean tone at the end next to mean F1.
//     4. BONUS: add a second gate - fail the run if mean tone < 0.20 - and
//        decide: should tone be a hard gate or just a warning? Why?
