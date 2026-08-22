using System;
using Windows.Security.Credentials;
using Windows.Storage;

namespace CodingSahayi;

public static class SettingsManager
{
    private const string ResourceName = "VibeCoderAgent";
    private const string ApiKeyUserName = "ApiKey";
    private const int PromptVersion = 3; // Bump this when the default prompt changes
    
    private static readonly ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;

    public static string ApiEndpoint
    {
        get => LocalSettings.Values["ApiEndpoint"] as string ?? "https://integrate.api.nvidia.com/v1";
        set => LocalSettings.Values["ApiEndpoint"] = value;
    }

    public static string ModelName
    {
        get => LocalSettings.Values["ModelName"] as string ?? "meta/llama-3.1-70b-instruct";
        set => LocalSettings.Values["ModelName"] = value;
    }

    private const string DefaultSystemPrompt = """
You are an expert native Windows coding agent operating inside a WinUI 3 IDE called Coding Sahayi.

OPERATIONAL RULES:
1. EXPLORE FIRST: Never assume file paths. Use list_directory or search_code to locate files before reading or editing them.
2. READ BEFORE EDITING: Always read_file to see the current contents before modifying a file.
3. SMALL CHANGES: Use patch_file for 1-2 small changes. Include enough surrounding lines in targetSnippet to make it unique.
4. LARGE CHANGES: When you need to make 3 or more changes to a single file, use write_file to rewrite the ENTIRE file at once instead of multiple patch_file calls. This is much more efficient.
5. FIX ALL ERRORS AT ONCE: When a build fails with multiple errors, read ALL affected files, plan ALL fixes, then apply them all before rebuilding. Use write_file to rewrite each affected file with all fixes included.
6. VERIFY: After modifications, run dotnet build via execute_terminal to verify.
7. AUTO-CORRECT: If a build fails, inspect ALL errors, fix everything, then rebuild. Do NOT fix one error at a time.

TOOL USAGE RULES:
- You must only execute ONE tool call at a time.
- You must invoke tools using the native tool calling API. Do NOT output raw JSON tool calls in your chat text.
- The execute_terminal working directory defaults to the user's workspace. You do not need to specify it.
""";

    public static string SystemPrompt
    {
        get
        {
            int storedVersion = LocalSettings.Values["SystemPromptVersion"] as int? ?? 0;
            if (storedVersion < PromptVersion)
            {
                // New default prompt available — auto-upgrade
                LocalSettings.Values["SystemPrompt"] = DefaultSystemPrompt;
                LocalSettings.Values["SystemPromptVersion"] = PromptVersion;
                return DefaultSystemPrompt;
            }
            return LocalSettings.Values["SystemPrompt"] as string ?? DefaultSystemPrompt;
        }
        set
        {
            LocalSettings.Values["SystemPrompt"] = value;
            LocalSettings.Values["SystemPromptVersion"] = PromptVersion;
        }
    }

    public static string SecureApiKey
    {
        get
        {
            var vault = new PasswordVault();
            try
            {
                var credential = vault.Retrieve(ResourceName, ApiKeyUserName);
                credential.RetrievePassword();
                return credential.Password;
            }
            catch (Exception)
            {
                return "YOUR_API_KEY";
            }
        }
        set
        {
            var vault = new PasswordVault();
            vault.Add(new PasswordCredential(ResourceName, ApiKeyUserName, value));
        }
    }
}
