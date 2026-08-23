using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace CodingSahayi;

public class NativeTools
{
    public static string ReadFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return $"Error: File not found at {filePath}";

            return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    public static string WriteFile(string filePath, string content)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
            return $"Success: Wrote {content.Length} characters to {filePath}";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }

    public static string PatchFile(string filePath, string targetSnippet, string replacementSnippet)
    {
        try
        {
            if (!File.Exists(filePath)) return $"Error: File not found at {filePath}";
            
            string content = File.ReadAllText(filePath);
            string normalizedContent = content.Replace("\r\n", "\n");
            string normalizedTarget = targetSnippet.Replace("\r\n", "\n");
            string normalizedReplacement = replacementSnippet.Replace("\r\n", "\n");
            
            int firstIndex = normalizedContent.IndexOf(normalizedTarget);
            if (firstIndex == -1) return "Error: Target snippet not found in file. Please read the file first and provide the exact text to match.";
            
            int lastIndex = normalizedContent.LastIndexOf(normalizedTarget);
            bool hadMultiple = (firstIndex != lastIndex);
            
            // Replace only the first occurrence
            string newContent = normalizedContent.Substring(0, firstIndex) 
                + normalizedReplacement 
                + normalizedContent.Substring(firstIndex + normalizedTarget.Length);
            File.WriteAllText(filePath, newContent);
            
            string msg = $"Success: Replaced snippet in {filePath}";
            if (hadMultiple) msg += " (Warning: snippet appeared multiple times, only replaced the FIRST occurrence. Use more context lines for precision.)";
            return msg;
        }
        catch (Exception ex)
        {
            return $"Error patching file: {ex.Message}";
        }
    }

    public static string ListDirectory(string directoryPath, int maxDepth = 3)
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return $"Error: Directory not found at {directoryPath}";
            
            var sb = new System.Text.StringBuilder();
            var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", ".vs", "dist", "build" };
            
            void WalkDirectory(string path, int currentDepth, string prefix)
            {
                if (currentDepth > maxDepth) return;
                
                var dirInfo = new DirectoryInfo(path);
                sb.AppendLine($"{prefix}{dirInfo.Name}/");
                
                try
                {
                    var dirs = dirInfo.GetDirectories().Where(d => !ignoredDirs.Contains(d.Name)).OrderBy(d => d.Name).ToList();
                    var files = dirInfo.GetFiles().OrderBy(f => f.Name).ToList();
                    
                    for (int i = 0; i < dirs.Count; i++)
                    {
                        bool isLast = (i == dirs.Count - 1) && (files.Count == 0);
                        WalkDirectory(dirs[i].FullName, currentDepth + 1, prefix + (isLast ? "    " : "│   "));
                    }
                    
                    for (int i = 0; i < files.Count; i++)
                    {
                        bool isLast = (i == files.Count - 1);
                        sb.AppendLine($"{prefix}{(isLast ? "└── " : "├── ")}{files[i].Name}");
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
            
            WalkDirectory(directoryPath, 0, "");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    public static string SearchDirectory(string directoryPath, string searchPattern = "*")
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return $"Error: Directory not found at {directoryPath}";
            
            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
            if (files.Length == 0) return "No files found matching the pattern.";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {files.Length} file(s):");
            foreach (var f in files)
            {
                // Make relative to the search root for cleaner output
                string relPath = f.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase) 
                    ? f.Substring(directoryPath.Length).TrimStart('\\', '/') 
                    : f;
                sb.AppendLine($"- {relPath}");
            }
            return sb.ToString();
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"Access denied: {ex.Message}";
        }
        catch (DirectoryNotFoundException ex)
        {
            return $"Directory not found: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error searching directory: {ex.Message}";
        }
    }

    public static string SearchCode(string directoryPath, string searchQuery, string fileExtensionFilter = "*.*")
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return $"Error: Directory not found at {directoryPath}";
            
            var sb = new System.Text.StringBuilder();
            var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", ".vs", "dist", "build" };
            
            var files = Directory.EnumerateFiles(directoryPath, string.IsNullOrEmpty(fileExtensionFilter) ? "*.*" : fileExtensionFilter, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            }).Where(f => !ignoredDirs.Any(ig => f.Contains($"\\{ig}\\") || f.Contains($"/{ig}/")));
            
            int matchCount = 0;
            foreach (var file in files)
            {
                if (matchCount >= 50) { sb.AppendLine("Warning: Reached maximum match limit (50). Truncated."); break; }
                
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        matchCount++;
                        sb.AppendLine($"File: {file}:{i + 1}");
                        if (i > 0) sb.AppendLine($"  {i}: {lines[i - 1]}");
                        sb.AppendLine($"  {i + 1}: {lines[i]}");
                        if (i < lines.Length - 1) sb.AppendLine($"  {i + 2}: {lines[i + 1]}");
                        sb.AppendLine("---");
                    }
                }
            }
            
            if (matchCount == 0) return "No matches found.";
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching code: {ex.Message}";
        }
    }

    public static string ExecuteTerminalSafe(string command, string workingDirectory = "", int timeoutSeconds = 45, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? AppContext.BaseDirectory : workingDirectory
                }
            };
            
            var outputBuilder = new System.Text.StringBuilder();
            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine($"ERROR: {e.Data}"); };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            bool cancelled = false;
            bool exited = false;
            
            // Loop waiting for exit, breaking early if the caller cancels.
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                if (process.WaitForExit(100))
                {
                    exited = true;
                    break;
                }
            }

            if (cancelled)
            {
                process.Kill(true); // Kill process tree
                outputBuilder.AppendLine($"\n\n--- COMMAND CANCELLED ---");
            }
            else if (!exited)
            {
                process.Kill(true); // Kill process tree
                outputBuilder.AppendLine($"\n\n--- COMMAND TIMED OUT AFTER {timeoutSeconds} SECONDS ---");
            }
            
            string output = outputBuilder.ToString();
            
            if (output.Length > 10240)
            {
                output = "... [TRUNCATED] ...\n" + output.Substring(output.Length - 10240);
            }
            
            var lines = output.Split('\n');
            if (lines.Length > 200)
            {
                output = "... [TRUNCATED] ...\n" + string.Join('\n', lines.Skip(lines.Length - 200));
            }
            
            return string.IsNullOrWhiteSpace(output) ? "Command executed successfully with no output." : output;
        }
        catch (Exception ex)
        {
            return $"Failed to start process: {ex.Message}";
        }
    }
}
