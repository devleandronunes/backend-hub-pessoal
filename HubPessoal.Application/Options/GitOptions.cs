namespace HubPessoal.Application.Options;

public class GitOptions
{
    public const string SectionName = "Git";

    public string RepositoryUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;

    public string AuthenticatedUrl =>
        RepositoryUrl.Replace("https://", $"https://x-access-token:{Token}@");

    public string ResolvedWorkingDirectory =>
        string.IsNullOrWhiteSpace(WorkingDirectory)
            ? Path.Combine(Path.GetTempPath(), $"hub-pessoal-notes-{EnvironmentTag}")
            : WorkingDirectory;

    private static string EnvironmentTag =>
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLowerInvariant() ?? "local";

    public void EnsureConfigured()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(RepositoryUrl)) missing.Add(nameof(RepositoryUrl));
        if (string.IsNullOrWhiteSpace(Token)) missing.Add(nameof(Token));
        if (string.IsNullOrWhiteSpace(AuthorName)) missing.Add(nameof(AuthorName));
        if (string.IsNullOrWhiteSpace(AuthorEmail)) missing.Add(nameof(AuthorEmail));

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Git configuration is incomplete for environment '{EnvironmentTag}'. Missing: {string.Join(", ", missing.Select(m => $"Git:{m}"))}.");
        }
    }
}