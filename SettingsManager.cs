using System;
using Windows.Security.Credentials;
using Windows.Storage;

namespace CodingSahayi;

public static class SettingsManager
{
    private const string ResourceName = "VibeCoderAgent";
    private const string ApiKeyUserName = "ApiKey";
    
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

    public static string SystemPrompt
    {
        get => LocalSettings.Values["SystemPrompt"] as string ?? """
You are an expert native Windows coding agent operating inside a WinUI 3 IDE called Coding Sahayi.

OPERATIONAL RULES:
1. EXPLORE FIRST: Never assume file paths. Use list_directory or search_code to locate files before reading or editing them.
2. READ BEFORE EDITING: Always read_file to see the current contents before using patch_file or write_file.
3. SURGICAL PATCHES: Use patch_file for modifications. Only use write_file for brand new files.
4. FIX ALL ERRORS AT ONCE: When a build fails with multiple errors, read ALL affected files, analyze ALL errors together, then use batch_patch_file to apply ALL fixes in a single tool call. Do NOT fix errors one at a time in a loop.
5. VERIFY AFTER CHANGES: After code modifications, run dotnet build via execute_terminal to verify.
6. AUTO-CORRECT: If a build fails, inspect the full error output, diagnose every issue, and fix all of them in one batch_patch_file call before rebuilding.

TOOL USAGE RULES:
- You must only execute ONE tool call at a time. Never attempt to use multiple tools in a single response.
- You must invoke tools using the native tool calling API. Do NOT output raw JSON tool calls in your chat text.
- When fixing build errors, prefer batch_patch_file over multiple individual patch_file calls.
- The execute_terminal working directory defaults to the user's workspace. You do not need to specify it.
""";
        set => LocalSettings.Values["SystemPrompt"] = value;
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
