using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodingSahayi;

public static class TestRunnerTool
{
    public static async Task<string> RunTestsAsync(string projectPath)
    {
        string result = await PtyManager.ExecuteCommandAsync("dotnet", "test", projectPath, 60);
        
        if (result.Contains("Passed!") && !result.Contains("Failed!"))
        {
            return "All tests passed successfully.";
        }
        
        if (result.Contains("Failed"))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Test Failures Detected:");
            
            // Basic regex to extract xUnit/NUnit/MSTest failures
            var match = Regex.Match(result, @"Failed\s+[^\n]+(.*?)Total tests:", RegexOptions.Singleline);
            if (match.Success)
            {
                sb.AppendLine(match.Groups[1].Value.Trim());
            }
            else
            {
                sb.AppendLine(result);
            }
            
            return sb.ToString();
        }

        return $"Test execution completed with unexpected output:\n{result}";
    }
}
