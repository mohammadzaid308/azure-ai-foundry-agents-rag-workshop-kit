using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace WorkshopShared;

/// <summary>
/// Minimal .env loader shared by every lab. Mirrors python-dotenv's behaviour:
/// it finds the nearest .env file (walking up from the working directory and the
/// app base directory), then sets any keys that are not ALREADY present in the
/// environment. Variables you export yourself (e.g. `set -a && source .env`) win.
///
/// This runs automatically via [ModuleInitializer] before each lab's top-level
/// code executes, so the labs need no extra setup lines and no NuGet package.
/// </summary>
internal static class DotEnv
{
    [ModuleInitializer]
    internal static void Load()
    {
        string? path = FindEnvFile();
        if (path is null)
        {
            return;
        }

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();

            // Strip a single layer of matching surrounding quotes.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Do not override variables already set in the process environment.
            if (key.Length > 0 && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindEnvFile()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? dir = new(start);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
        }
        return null;
    }
}
