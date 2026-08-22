using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI.Chat;
using OpenAI;

namespace CodingSahayi;

public class AgentContextManager
{
    private ChatClient _chatClient;
    private readonly ChatCompletionOptions _chatOptions;
    private readonly List<ChatMessage> _apiHistory = new();
    
    private const int MaxContextMessages = 20;

    public string WorkspaceDirectory { get; set; } = AppContext.BaseDirectory;

    public delegate void ToolStartHandler(string toolCallId, string toolName, string arguments);
    public delegate void ToolEndHandler(string toolCallId, string output, bool success);

    public AgentContextManager()
    {
        _chatOptions = new ChatCompletionOptions
        {
            AllowParallelToolCalls = false
        };
        _chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
            "read_file",
            "Reads a file from the disk.",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"}},\"required\":[\"filePath\"]}")
        ));
        _chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
            "write_file",
            "Writes content to a file on the disk.",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"filePath\",\"content\"]}")
        ));
        _chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
            "patch_file",
            "Replaces a specific snippet of text in a file with a new snippet.",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"},\"targetSnippet\":{\"type\":\"string\"},\"replacementSnippet\":{\"type\":\"string\"}},\"required\":[\"filePath\",\"targetSnippet\",\"replacementSnippet\"]}")
        ));
        _chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
            "list_directory",
            "Lists files and folders in a directory.",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"directoryPath\":{\"type\":\"string\"},\"maxDepth\":{\"type\":\"integer\"}},\"required\":[\"directoryPath\"]}")
        ));
        _chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
            "search_code",
            "Searches for a string across files in a directory.",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"searchQuery\":{\"type\":\"string\"},\"fileExtensionFilter\":{\"type\":\"string\"}},\"required\":[\"searchQuery\"]}")
        ));
        _chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
            "execute_terminal",
            "Executes a command. You do not need to provide a working directory; it will automatically default to the user's selected workspace root.",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"},\"workingDirectory\":{\"type\":\"string\"},\"timeoutSeconds\":{\"type\":\"integer\"}},\"required\":[\"command\"]}")
        ));

        InitializeClient();
        _apiHistory.Add(new SystemChatMessage(SettingsManager.SystemPrompt));
    }

    public void ReinitializeClient()
    {
        InitializeClient();
        if (_apiHistory.Count > 0)
        {
            _apiHistory[0] = new SystemChatMessage(SettingsManager.SystemPrompt);
        }
        else
        {
            _apiHistory.Add(new SystemChatMessage(SettingsManager.SystemPrompt));
        }
    }

    private void InitializeClient()
    {
        var apiKey = SettingsManager.SecureApiKey;
        var endpoint = SettingsManager.ApiEndpoint;
        var model = SettingsManager.ModelName;
        
        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
        _chatClient = client.GetChatClient(model);
    }

    public async Task<string> ProcessMessageAsync(
        string userMessage, 
        Action<string> onStatusUpdate, 
        ToolStartHandler onToolStart,
        ToolEndHandler onToolEnd,
        int maxIterations = 15)
    {
        _apiHistory.Add(new UserChatMessage(userMessage));
        PruneContextIfNecessary();

        int iterationCount = 0;
        bool requiresAction = true;
        string finalResponse = string.Empty;

        while (requiresAction)
        {
            if (iterationCount >= maxIterations)
            {
                finalResponse = "I've hit my iteration limit. How would you like me to proceed?";
                break;
            }
            
            iterationCount++;
            requiresAction = false;
            
            try
            {
                ChatCompletion completion = await _chatClient.CompleteChatAsync(_apiHistory, _chatOptions);

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    _apiHistory.Add(new AssistantChatMessage(completion));
                    onStatusUpdate($"Running {completion.ToolCalls.Count} tool(s)... (Iteration {iterationCount}/{maxIterations})");

                    foreach (var toolCall in completion.ToolCalls)
                    {
                        string toolResult = string.Empty;
                        bool success = true;
                        try
                        {
                            var argsStr = toolCall.FunctionArguments.ToString();
                            onToolStart?.Invoke(toolCall.Id, toolCall.FunctionName, argsStr);

                            var args = JsonSerializer.Deserialize<JsonElement>(argsStr);

                            switch (toolCall.FunctionName)
                            {
                                case "read_file":
                                    toolResult = NativeTools.ReadFile(ResolvePath(args.GetProperty("filePath").GetString() ?? ""));
                                    if (toolResult.StartsWith("Error")) success = false;
                                    break;
                                case "write_file":
                                    toolResult = NativeTools.WriteFile(
                                        ResolvePath(args.GetProperty("filePath").GetString() ?? ""), 
                                        args.GetProperty("content").GetString() ?? "");
                                    if (toolResult.StartsWith("Error")) success = false;
                                    break;
                                case "patch_file":
                                    toolResult = NativeTools.PatchFile(
                                        ResolvePath(args.GetProperty("filePath").GetString() ?? ""), 
                                        args.GetProperty("targetSnippet").GetString() ?? "", 
                                        args.GetProperty("replacementSnippet").GetString() ?? "");
                                    if (toolResult.StartsWith("Error")) success = false;
                                    break;
                                case "list_directory":
                                    string dirPath = args.TryGetProperty("directoryPath", out var p) ? p.GetString() ?? WorkspaceDirectory : WorkspaceDirectory;
                                    toolResult = NativeTools.ListDirectory(ResolvePath(dirPath), GetIntProperty(args, "maxDepth", 3));
                                    if (toolResult.StartsWith("Error")) success = false;
                                    break;
                                case "search_code":
                                    toolResult = NativeTools.SearchCode(
                                        WorkspaceDirectory,
                                        args.GetProperty("searchQuery").GetString() ?? "",
                                        args.TryGetProperty("fileExtensionFilter", out var fe) ? fe.GetString() : "*.*");
                                    if (toolResult.StartsWith("Error")) success = false;
                                    break;
                                case "execute_terminal":
                                    toolResult = NativeTools.ExecuteTerminalSafe(
                                        args.GetProperty("command").GetString() ?? "",
                                        args.TryGetProperty("workingDirectory", out var wd) ? (string.IsNullOrWhiteSpace(wd.GetString()) ? WorkspaceDirectory : wd.GetString()) : WorkspaceDirectory,
                                        GetIntProperty(args, "timeoutSeconds", 45));
                                    if (toolResult.StartsWith("Failed") || toolResult.Contains("TIMED OUT")) success = false;
                                    break;
                                default:
                                    toolResult = $"Unknown tool: {toolCall.FunctionName}";
                                    success = false;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            toolResult = $"Error executing tool: {ex.Message}";
                            success = false;
                        }

                        onToolEnd?.Invoke(toolCall.Id, toolResult, success);
                        _apiHistory.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    }
                    
                    PruneContextIfNecessary();
                    requiresAction = true; 
                }
                else
                {
                    _apiHistory.Add(new AssistantChatMessage(completion));
                    finalResponse = completion.Content[0].Text;
                    PruneContextIfNecessary();
                }
            }
            catch (Exception ex)
            {
                finalResponse = $"**Error:** {ex.Message}";
                requiresAction = false;
            }
        }
        
        return finalResponse;
    }
    
    private void PruneContextIfNecessary()
    {
        if (_apiHistory.Count <= MaxContextMessages) return;

        var systemMessage = _apiHistory.FirstOrDefault();
        
        while (_apiHistory.Count > MaxContextMessages * 0.8)
        {
            if (_apiHistory.Count > 1)
            {
                _apiHistory.RemoveAt(1);
            }
        }
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return WorkspaceDirectory;
        return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(WorkspaceDirectory, path);
    }

    private int GetIntProperty(JsonElement element, string propertyName, int defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int num)) return num;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int strNum)) return strNum;
        }
        return defaultValue;
    }
}
