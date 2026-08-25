namespace HubPessoal.Application.Interfaces;

public record GitResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

public interface IGitClient
{
    Task<GitResult> RunAsync(string workingDirectory, params string[] arguments);
}