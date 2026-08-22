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
You are an expert native Windows coding agent.
Follow these operational guidelines:
1. Always explore the workspace first using list_directory or search_code before assuming file locations.
2. Read a file before modifying it.
3. Make minimal, surgical edits rather than rewriting working code.
4. After writing or modifying code, execute the build command via PowerShell to verify that there are no compilation errors.
5. If a build fails or a tool returns an error, inspect the error output, diagnose the issue, and apply a fix immediately before reporting back to the user.
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
