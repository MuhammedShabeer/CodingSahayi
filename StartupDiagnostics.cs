using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CodingSahayi;

public static class StartupDiagnostics
{
    public static string WorkspaceEnvironment { get; private set; } = "Unknown";

    public static async Task<List<string>> RunChecksAsync(string workspacePath = "")
    {
        var missingDependencies = new List<string>();
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";

        var availableRuntimes = new List<string>();
        if (pathVariable.Contains("dotnet", StringComparison.OrdinalIgnoreCase)) availableRuntimes.Add(".NET");
        if (pathVariable.Contains("python", StringComparison.OrdinalIgnoreCase)) availableRuntimes.Add("Python");
        if (pathVariable.Contains("node", StringComparison.OrdinalIgnoreCase) || pathVariable.Contains("npm", StringComparison.OrdinalIgnoreCase)) availableRuntimes.Add("Node.js");
        if (pathVariable.Contains("cargo", StringComparison.OrdinalIgnoreCase)) availableRuntimes.Add("Rust");
        if (pathVariable.Contains("go", StringComparison.OrdinalIgnoreCase)) availableRuntimes.Add("Go");

        WorkspaceEnvironment = availableRuntimes.Count > 0 ? string.Join(", ", availableRuntimes) : "No major runtimes detected";

        Serilog.Log.Information("Detected workspace environment runtimes: {Runtimes}", WorkspaceEnvironment);

        // Check local LLM runtimes (still critical for local agent execution)
        bool ollamaActive = false;
        bool lmStudioActive = false;
        
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        try
        {
            var ollamaResponse = await client.GetAsync("http://localhost:11434/");
            if (ollamaResponse.IsSuccessStatusCode) ollamaActive = true;
        }
        catch { }

        try
        {
            var lmResponse = await client.GetAsync("http://localhost:1234/v1/models");
            if (lmResponse.IsSuccessStatusCode) lmStudioActive = true;
        }
        catch { }

        if (!ollamaActive && !lmStudioActive)
        {
            missingDependencies.Add("Local LLM Runtime (Neither Ollama nor LM Studio were detected running on default ports)");
        }

        return missingDependencies;
    }
}
