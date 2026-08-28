using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pty.Net;

namespace CodingSahayi;

public static class PtyManager
{
    public static async Task<string> ExecuteCommandAsync(string app, string arguments, string workingDirectory, int timeoutSeconds = 60)
    {
        var options = new PtyOptions
        {
            Name = "xterm",
            Cols = 120,
            Rows = 50,
            Cwd = workingDirectory,
            App = app,
            CommandLine = new[] { app, arguments }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        
        IPtyConnection pty;
        try
        {
            pty = await PtyProvider.SpawnAsync(options, cts.Token);
        }
        catch (Exception ex)
        {
            return $"Failed to start process: {ex.Message}";
        }

        var outputBuilder = new StringBuilder();
        
        var readTask = Task.Run(async () =>
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    int bytesRead = await pty.ReaderStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    if (bytesRead == 0) break;
                    outputBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
            }
            catch { }
        });

        try 
        {
            var tcs = new TaskCompletionSource<int>();
            pty.ProcessExited += (sender, args) => tcs.TrySetResult(0);
            
            // Allow waiting for exit or cancellation
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            pty.Dispose();
            return $"Process timed out after {timeoutSeconds} seconds.\nPartial Output:\n{outputBuilder.ToString()}";
        }
        
        // Give the read stream a moment to flush
        await Task.WhenAny(readTask, Task.Delay(500));
        pty.Dispose();

        return outputBuilder.ToString();
    }
}
