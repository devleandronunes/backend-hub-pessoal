using HubPessoal.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace HubPessoal.IntegrationTests;

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public string GitRemoteUrl { get; private set; } = string.Empty;
    public string GitRemotePath { get; private set; } = string.Empty;
    private string _gitWorkingDirectory = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        GitRemotePath = Path.Combine(Path.GetTempPath(), $"hub-tests-remote-{Guid.NewGuid()}");
        Directory.CreateDirectory(GitRemotePath);
        await RunGitAsync(GitRemotePath, "init", "--bare", "--initial-branch=main");

        var seedPath = Path.Combine(Path.GetTempPath(), $"hub-tests-seed-{Guid.NewGuid()}");
        await RunGitAsync(Path.GetTempPath(), "clone", GitRemotePath, seedPath);
        await File.WriteAllTextAsync(Path.Combine(seedPath, "README.md"), "seed");
        await RunGitAsync(seedPath, "add", "-A");
        await RunGitAsync(seedPath, "-c", "user.name=test", "-c", "user.email=test@local", "commit", "-m", "seed");
        await RunGitAsync(seedPath, "push", "origin", "main");
        Directory.Delete(seedPath, recursive: true);

        GitRemoteUrl = $"file://{GitRemotePath}";
        _gitWorkingDirectory = Path.Combine(Path.GetTempPath(), $"hub-tests-workspace-{Guid.NewGuid()}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Git:RepositoryUrl"] = GitRemoteUrl,
                ["Git:Token"] = "not-needed-for-local-file-remote",
                ["Git:AuthorName"] = "integration-tests",
                ["Git:AuthorEmail"] = "tests@local",
                ["Git:WorkingDirectory"] = _gitWorkingDirectory,
                ["Jwt:Key"] = "chave-de-teste-suficientemente-longa-1234567890",
                ["Jwt:Issuer"] = "hub-pessoal-tests",
                ["Jwt:Audience"] = "hub-pessoal-tests",
                ["Auth:SeedUsername"] = "test-user",
                ["Auth:SeedPassword"] = "Test_Only_Pass1",
            });
        });

        // `HubPessoal.Infrastructure.DependencyInjection.AddInfrastructure` lê a connection string
        // e chama `UseNpgsql` de forma antecipada, dentro do próprio Program.cs, antes deste hook
        // rodar — então o `AddInMemoryCollection` acima não alcança a hora em que o DbContext é
        // configurado. Sem isto, os testes acabam batendo no Postgres real de dev, não no
        // container efêmero do Testcontainers. Solução: substituir o registro do DbContext aqui,
        // depois que o resto do Program.cs já rodou.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    // Cria um novo clone local do repositório bare, isolado por chamada, para os testes que
    // precisam simular uma alteração feita "de fora" (direto no remoto) sem interferir no
    // workspace que a API usa internamente.
    public async Task<string> CloneRemoteAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hub-tests-clone-{Guid.NewGuid()}");
        await RunGitAsync(Path.GetTempPath(), "clone", GitRemotePath, path);
        return path;
    }

    public static Task RunGitAsync(string workingDirectory, params string[] arguments) =>
        RunGitInternalAsync(workingDirectory, arguments);

    private static async Task RunGitInternalAsync(string workingDirectory, string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        await process.WaitForExitAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        if (Directory.Exists(GitRemotePath)) Directory.Delete(GitRemotePath, recursive: true);
        if (Directory.Exists(_gitWorkingDirectory)) Directory.Delete(_gitWorkingDirectory, recursive: true);
    }
}
