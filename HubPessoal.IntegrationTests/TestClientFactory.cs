using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HubPessoal.IntegrationTests;

public static class TestClientFactory
{
    public static async Task<HttpClient> CreateAuthorizedClientAsync(ApiFixture fixture)
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login", new { username = "test-user", password = "Test_Only_Pass1" });
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]);
        return client;
    }
}
