using System.Net;
using System.Net.Http.Json;

namespace HubPessoal.IntegrationTests;

public class NotesEndpointsTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public NotesEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateNote_ThenGetTree_ReturnsIt()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        var createResponse = await client.PostAsJsonAsync("/notes", new
        {
            title = "Nota de teste",
            content = "conteúdo",
            folderId = (Guid?)null,
            tags = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var tree = await client.GetFromJsonAsync<List<Dictionary<string, object>>>("/notes/tree");
        Assert.Contains(tree!, n => n["name"].ToString() == "Nota de teste");
    }

    [Fact]
    public async Task CreateNote_WithNonexistentFolder_ReturnsBadRequest()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        var response = await client.PostAsJsonAsync("/notes", new
        {
            title = "Nota órfã",
            content = "conteúdo",
            folderId = Guid.NewGuid(),
            tags = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_WithDuplicateTitleInSameFolder_ReturnsConflict()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);
        var payload = new
        {
            title = "Nota duplicada",
            content = "conteúdo",
            folderId = (Guid?)null,
            tags = Array.Empty<string>(),
        };

        var first = await client.PostAsJsonAsync("/notes", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/notes", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_Nonexistent_ReturnsNotFound()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        var response = await client.PutAsJsonAsync($"/notes/{Guid.NewGuid()}", new
        {
            title = "Não existe",
            content = "conteúdo",
            tags = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
