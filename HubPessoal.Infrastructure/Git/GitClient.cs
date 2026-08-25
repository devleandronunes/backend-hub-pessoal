using System.Diagnostics;
using HubPessoal.Application.Interfaces;

namespace HubPessoal.Infrastructure.Git;

public class GitClient : IGitClient
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);
    public async Task<GitResult> RunAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_ASKPASS"] = "true";
        startInfo.Environment["GCM_INTERACTIVE"] = "never";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the git process.");

        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(CommandTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        } catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"git {string.Join(' ', arguments)} exceeded {CommandTimeout.TotalSeconds}s.");
        }

        return new GitResult(process.ExitCode, await outputTask, await errorTask);
    }
}