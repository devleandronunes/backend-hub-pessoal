using System.Net;
using System.Net.Http.Json;

namespace HubPessoal.IntegrationTests;

public class FoldersEndpointsTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public FoldersEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DeleteFolder_NotEmpty_ReturnsConflict()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        var folderResponse = await client.PostAsJsonAsync("/folders", new { name = "Pasta não vazia", parentFolderId = (Guid?)null });
        var folder = await folderResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var folderId = Guid.Parse(folder!["id"].ToString()!);

        await client.PostAsJsonAsync("/notes", new
        {
            title = "Nota dentro da pasta",
            content = "conteúdo",
            folderId,
            tags = Array.Empty<string>(),
        });

        var deleteResponse = await client.DeleteAsync($"/folders/{folderId}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }
}
