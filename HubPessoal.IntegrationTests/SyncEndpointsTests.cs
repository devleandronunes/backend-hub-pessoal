using System.Net;
using System.Net.Http.Json;
using HubPessoal.Api.Contracts.Sync;

namespace HubPessoal.IntegrationTests;

// Cada cenário de sync fica na sua própria classe (fixture própria: Postgres + repositório bare +
// workspace git isolados) porque os testes mutam estado compartilhado que não pode vazar entre
// eles — um teste que cria uma nota sem aplicar o sync deixaria alterações locais pendentes no
// mesmo workspace do próximo teste, contaminando o cálculo de SyncState.

public class SyncFullCycleTests : IClassFixture<ApiFixture>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;

    public SyncFullCycleTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateNoteThenApply_AppearsInHistory()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        await client.PostAsJsonAsync("/notes", new
        {
            title = "Nota sincronizada",
            content = "conteúdo enviado pelo teste de integração",
            folderId = (Guid?)null,
            tags = Array.Empty<string>(),
        });

        var previewResponse = await client.PostAsync("/sync/preview", null);
        var plan = await previewResponse.Content.ReadFromJsonAsync<SyncPlanResponse>(JsonOptions);

        Assert.Equal(nameof(HubPessoal.Application.Models.SyncState.LocalChanges), plan!.State);
        Assert.True(plan.WillPush);

        var applyResponse = await client.PostAsJsonAsync("/sync/apply", new { fingerprint = plan.Fingerprint });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var applyBody = await applyResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var commitHash = applyBody!["commitHash"];
        Assert.False(string.IsNullOrWhiteSpace(commitHash));

        var history = await client.GetFromJsonAsync<List<SyncCommitSummaryResponse>>("/sync/history");
        Assert.Contains(history!, c => c.CommitHash == commitHash);
    }
}

public class SyncStaleFingerprintTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public SyncStaleFingerprintTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Apply_WithStaleFingerprint_ReturnsConflictWithPlanExpired()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        await client.PostAsJsonAsync("/notes", new
        {
            title = "Nota para fingerprint velho",
            content = "conteúdo",
            folderId = (Guid?)null,
            tags = Array.Empty<string>(),
        });

        var response = await client.PostAsJsonAsync("/sync/apply", new { fingerprint = "fingerprint-que-nunca-existiu" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplySyncErrorResponse>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("PlanExpired", body!.Reason);
    }
}

public class SyncRemoteChangesTests : IClassFixture<ApiFixture>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;

    public SyncRemoteChangesTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Status_AfterExternalPush_ReportsRemoteChangesAndImportsOnApply()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        // Um workspace recém-clonado nunca bate exatamente com o HEAD (materializar um banco
        // vazio já cria "notes/.gitkeep", que ainda não existe no commit seed) — sem aplicar essa
        // baseline primeiro, o próximo preview veria local change + remote change ao mesmo tempo
        // (Diverged) em vez do cenário puro de RemoteChanges que este teste quer verificar.
        var baselinePreview = await client.PostAsync("/sync/preview", null);
        var baselinePlan = await baselinePreview.Content.ReadFromJsonAsync<SyncPlanResponse>(JsonOptions);
        await client.PostAsJsonAsync("/sync/apply", new { fingerprint = baselinePlan!.Fingerprint });

        var externalClonePath = await _fixture.CloneRemoteAsync();
        try
        {
            var externalNotePath = Path.Combine(externalClonePath, "notes");
            Directory.CreateDirectory(externalNotePath);
            await File.WriteAllTextAsync(
                Path.Combine(externalNotePath, "Nota externa.md"),
                "---\ntags: []\npinned: false\n---\n\nCriada direto no repositório.");

            await ApiFixture.RunGitAsync(externalClonePath, "add", "-A");
            await ApiFixture.RunGitAsync(
                externalClonePath, "-c", "user.name=external", "-c", "user.email=external@local",
                "commit", "-m", "nota criada fora do hub");
            await ApiFixture.RunGitAsync(externalClonePath, "push", "origin", "main");
        }
        finally
        {
            Directory.Delete(externalClonePath, recursive: true);
        }

        var previewResponse = await client.PostAsync("/sync/preview", null);
        var plan = await previewResponse.Content.ReadFromJsonAsync<SyncPlanResponse>(JsonOptions);

        Assert.Equal(nameof(HubPessoal.Application.Models.SyncState.RemoteChanges), plan!.State);
        Assert.True(plan.WillPull);

        var applyResponse = await client.PostAsJsonAsync("/sync/apply", new { fingerprint = plan.Fingerprint });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var tree = await client.GetFromJsonAsync<List<Dictionary<string, object>>>("/notes/tree");
        Assert.Contains(tree!, n => n["name"].ToString() == "Nota externa");
    }
}
