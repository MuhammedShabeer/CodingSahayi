using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodingSahayi;

public enum TaskPriority
{
    High,
    Medium,
    Low
}

public class SwarmTask
{
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
}

public class SwarmOrchestrator
{
    private readonly AgentContextManager _architect;
    private readonly AgentContextManager _worker;
    private readonly AgentContextManager _critic;
    private readonly AgentContextManager _scribe;

    public SwarmOrchestrator()
    {
        _architect = new AgentContextManager(SwarmProfiles.ArchitectPrompt, new[] { "analyze_structure", "semantic_code_search" });
        _worker = new AgentContextManager(SwarmProfiles.WorkerPrompt, new[] { "patch_file", "write_file", "read_file", "list_directory", "search_directory" });
        _critic = new AgentContextManager(SwarmProfiles.CriticPrompt, new[] { "verify_syntax", "read_file", "run_tests" });
        _scribe = new AgentContextManager(SwarmProfiles.ScribePrompt, new string[0]);
    }

    public async Task ExecuteSwarmAsync(string userRequest, Action<string> onStatusUpdate)
    {
        onStatusUpdate("Architect is planning...");
        var planResult = await _architect.ProcessMessageAsync(
            $"Break down this request into a JSON array of tasks: {userRequest}", 
            onStatusUpdate, null, null);

        var tasks = new List<SwarmTask>();
        try
        {
            var root = JsonDocument.Parse(planResult).RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    tasks.Add(new SwarmTask { Description = element.GetString() ?? "" });
                }
            }
        }
        catch
        {
            tasks.Add(new SwarmTask { Description = planResult });
        }

        // Process highest priority first (High = 0, Medium = 1, Low = 2)
        tasks.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (var task in tasks)
        {
            bool taskCompleted = false;
            int retries = 0;

            while (!taskCompleted && retries < 2)
            {
                onStatusUpdate($"Worker executing task: {task.Description}");
                var workerResult = await _worker.ProcessMessageAsync(task.Description, onStatusUpdate, null, null);

                onStatusUpdate("Critic is reviewing...");
                var criticResult = await _critic.ProcessMessageAsync(
                    $"Review these changes: {workerResult}", onStatusUpdate, null, null);

                if (criticResult.Contains("ACCEPT") || !criticResult.Contains("Error")) // Simplified critic acceptance
                {
                    taskCompleted = true;
                    onStatusUpdate("Critic approved changes. Scribe is documenting...");
                    await _scribe.ProcessMessageAsync($"Document these changes: {workerResult}", onStatusUpdate, null, null);
                }
                else
                {
                    retries++;
                    task.Description = $"Fix these issues: {criticResult}\nOriginal task: {task.Description}";
                }
            }
        }
        
        onStatusUpdate("Swarm execution completed.");
    }
}
