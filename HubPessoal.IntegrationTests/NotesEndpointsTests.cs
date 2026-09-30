using System.Net;
using System.Net.Http.Json;
using System.Text;

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

    [Fact]
    public async Task UpdateNote_WithContentBase64_StoresDecodedContent()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);
        var created = await client.PostAsJsonAsync("/notes", new
        {
            title = "Nota base64",
            contentBase64 = "",
            folderId = (Guid?)null,
            tags = Array.Empty<string>(),
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["id"].ToString();

        const string content = "# Conexão\n```\ncurl -fsSL https://example.com/install.sh | sh\n```";
        var response = await client.PutAsJsonAsync($"/notes/{id}", new
        {
            title = "Nota base64",
            contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            tags = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(content, note!["content"].ToString());
    }

    [Fact]
    public async Task UpdateNote_WithInvalidBase64_ReturnsBadRequest()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);

        var response = await client.PutAsJsonAsync($"/notes/{Guid.NewGuid()}", new
        {
            title = "Qualquer",
            contentBase64 = "não é base64",
            tags = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_WithDuplicateTitle_ReturnsProblemDetailsWithDetail()
    {
        var client = await TestClientFactory.CreateAuthorizedClientAsync(_fixture);
        var payload = new
        {
            title = "Nota problem details",
            content = "",
            folderId = (Guid?)null,
            tags = Array.Empty<string>(),
        };

        await client.PostAsJsonAsync("/notes", payload);
        var second = await client.PostAsJsonAsync("/notes", payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("A note with this title already exists in the same folder.", problem!["detail"].ToString());
    }
}
