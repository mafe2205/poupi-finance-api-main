using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AppFinances_API.IntegrationTests.Infrastructure
{
    public sealed class TestUser
    {
        public const int CategoryTypeSaida = 1;
        public const int PaymentMethodPix = 3;
        public const int StatusPendente = 0;
        public const int StatusProcessado = 1;

        private TestUser(HttpClient client, long userId)
        {
            Client = client;
            UserId = userId;
        }

        public HttpClient Client { get; }
        public long UserId { get; }

        public static async Task<TestUser> CreateAsync(ApiFactory factory)
        {
            var client = factory.CreateClient();
            var suffix = Guid.NewGuid().ToString("N")[..12];
            const string password = "senha-de-teste";

            var created = await client.PostAsJsonAsync("/api/User", new
            {
                name = "Teste",
                surname = "Integracao",
                birthDate = "1995-05-20",
                email = $"teste-{suffix}@example.com",
                phone = $"55{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
                user = $"teste_{suffix}",
                password
            });
            await EnsureSuccessAsync(created);

            var login = await client.PostAsJsonAsync("/api/Authentication/login", new { identifier = $"teste_{suffix}", password });
            var session = await ReadJsonAsync(login);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session["token"]!.GetValue<string>());
            return new TestUser(client, session["userId"]!.GetValue<long>());
        }

        public async Task<long> CreateCategoryAsync(string name, int limitValueCents = 0)
        {
            var existing = (await GetJsonAsync($"/api/Categories/user/{UserId}")).AsArray()
                .FirstOrDefault(c => c!["name"]!.GetValue<string>() == name && !c["isDefault"]!.GetValue<bool>());
            if (existing is not null)
            {
                var id = existing["id"]!.GetValue<long>();
                await EnsureSuccessAsync(await Client.PutAsJsonAsync($"/api/Categories/{id}", new { id, name, limitValueCents }));
                return id;
            }

            var response = await Client.PostAsJsonAsync("/api/Categories", new
            {
                userId = UserId,
                name,
                type = CategoryTypeSaida,
                limitValueCents
            });
            return (await ReadJsonAsync(response))["id"]!.GetValue<long>();
        }

        public async Task<long> WalletIdAsync()
        {
            var accounts = await GetJsonAsync($"/api/BankAccounts/user/{UserId}");
            return accounts.AsArray().First()!["id"]!.GetValue<long>();
        }

        public async Task<HttpResponseMessage> UploadAsync(string fixtureName, string? idempotencyKey = null)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName));
            using var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(file, "file", fixtureName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents/upload") { Content = content };
            if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);

            return await Client.SendAsync(request);
        }

        public async Task<JsonNode> UploadAndWaitForOcrAsync(string fixtureName)
        {
            var id = (await ReadJsonAsync(await UploadAsync(fixtureName)))["id"]!.GetValue<long>();

            return await Wait.UntilAsync(
                () => GetJsonAsync($"/api/documents/{id}"),
                document => document["status"]!.GetValue<int>() != StatusPendente,
                "o OCR do documento terminar");
        }

        public async Task<JsonNode> GetJsonAsync(string url) => await ReadJsonAsync(await Client.GetAsync(url));

        public static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response)
        {
            await EnsureSuccessAsync(response);
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }

        public static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {body}",
                null, response.StatusCode == 0 ? HttpStatusCode.InternalServerError : response.StatusCode);
        }
    }
}
