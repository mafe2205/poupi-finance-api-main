using AppFinances_API.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace AppFinances_API.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class ReceiptFlowTests(ApiFactory factory, ITestOutputHelper output)
    {
        private const string Receipt = "34.png";

        [Fact]
        public async Task Fluxo_completo_da_foto_ate_a_analise()
        {
            var user = await TestUser.CreateAsync(factory);
            var mercado = await user.CreateCategoryAsync("Mercado", limitValueCents: 3000);
            var wallet = await user.WalletIdAsync();

            var document = await user.UploadAndWaitForOcrAsync(Receipt);
            var suggestion = document["suggestion"]!;
            output.WriteLine($"TrustOCR: {document["trustOCR"]}\nTextOCR:\n{document["textOCR"]}\nSugestão: {suggestion.ToJsonString()}");

            Assert.Equal(TestUser.StatusProcessado, document["status"]!.GetValue<int>());
            Assert.Equal(8894, suggestion["paymentValueCents"]!.GetValue<int>());
            Assert.Equal("2026-10-22", suggestion["transactionDate"]!.GetValue<string>());
            Assert.Equal(4, document["items"]!.AsArray().Count);
            Assert.False(suggestion["itemsTotalDivergence"]!.GetValue<bool>());

            Assert.Equal(mercado, suggestion["categoryId"]!.GetValue<long>());
            Assert.False(suggestion["categoryRequiresConfirmation"]!.GetValue<bool>());

            var transaction = await ConfirmAsync(user, document, mercado, wallet);
            Assert.False(transaction["categoryConfirmedManually"]!.GetValue<bool>());

            var linked = await user.GetJsonAsync($"/api/documents/{document["id"]}");
            Assert.Equal(transaction["id"]!.GetValue<long>(), linked["transactionId"]!.GetValue<long>());

            var insight = await WaitForCurrentInsightAsync(user, "LIMITE_CATEGORIA");
            Assert.Equal(mercado, insight["categoryId"]!.GetValue<long>());
            Assert.Equal(FixedOllamaClient.Text, insight["description"]!.GetValue<string>());
            Assert.Contains(factory.Ollama.Prompts, p => p.Contains("R$ 88,94") && p.Contains("R$ 30,00"));
        }

        [Fact]
        public async Task Imagem_ilegivel_nao_inventa_valor_e_pede_revisao()
        {
            var user = await TestUser.CreateAsync(factory);

            var document = await user.UploadAndWaitForOcrAsync("03.jpeg");

            Assert.True(document["needsManualReview"]!.GetValue<bool>());
            var reasons = document["reviewReasons"]!.AsArray().Select(r => r!.GetValue<string>()).ToList();
            Assert.Contains("valor_nao_encontrado", reasons);
            Assert.Null(document["suggestion"]?["paymentValueCents"]?.GetValue<int?>());
        }

        [Fact]
        public async Task Reenvio_da_mesma_foto_nao_duplica_o_documento()
        {
            var user = await TestUser.CreateAsync(factory);
            var key = Guid.NewGuid().ToString();

            var first = await user.UploadAsync(Receipt, key);
            var resent = await user.UploadAsync(Receipt, key);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, resent.StatusCode);

            var firstBody = await TestUser.ReadJsonAsync(first);
            var resentBody = await TestUser.ReadJsonAsync(resent);
            Assert.True(firstBody["created"]!.GetValue<bool>());
            Assert.False(resentBody["created"]!.GetValue<bool>());
            Assert.Equal(firstBody["id"]!.GetValue<long>(), resentBody["id"]!.GetValue<long>());

            var documents = await user.GetJsonAsync($"/api/documents/user/{user.UserId}");
            Assert.Single(documents.AsArray());
        }

        [Fact]
        public async Task Confirmar_o_mesmo_documento_duas_vezes_nao_duplica_a_despesa()
        {
            var user = await TestUser.CreateAsync(factory);
            var mercado = await user.CreateCategoryAsync("Mercado");
            var wallet = await user.WalletIdAsync();
            var document = await user.UploadAndWaitForOcrAsync(Receipt);

            await ConfirmAsync(user, document, mercado, wallet);
            var second = await user.Client.PostAsJsonAsync($"/api/transactions/from-document/{document["id"]}", ConfirmBody(document, mercado, wallet));

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
            var transactions = await user.GetJsonAsync("/api/transactions");
            Assert.Single(transactions.AsArray());
        }

        [Fact]
        public async Task Correcao_manual_de_categoria_sobrevive_a_reclassificacao()
        {
            var user = await TestUser.CreateAsync(factory);
            var mercado = await user.CreateCategoryAsync("Mercado");
            var restaurantes = await user.CreateCategoryAsync("Restaurantes");
            var wallet = await user.WalletIdAsync();
            var document = await user.UploadAndWaitForOcrAsync(Receipt);

            var transaction = await ConfirmAsync(user, document, mercado, wallet);
            var id = transaction["id"]!.GetValue<long>();

            var update = await user.Client.PutAsJsonAsync($"/api/transactions/{id}", new
            {
                description = transaction["description"]!.GetValue<string>(),
                categoryId = restaurantes,
                paymentValueCents = transaction["paymentValueCents"]!.GetValue<int>(),
                transactionDate = transaction["transactionDate"]!.GetValue<string>(),
                updatedAt = DateTime.UtcNow
            });
            Assert.True((await TestUser.ReadJsonAsync(update))["categoryConfirmedManually"]!.GetValue<bool>());

            var reclassification = await TestUser.ReadJsonAsync(await user.Client.PostAsync("/api/transactions/reclassify", null));

            Assert.Empty(reclassification["changed"]!.AsArray());
            var after = await user.GetJsonAsync($"/api/transactions/{id}");
            Assert.Equal(restaurantes, after["categoryId"]!.GetValue<long>());
        }

        private static object ConfirmBody(JsonNode document, long categoryId, long bankAccountId)
        {
            var suggestion = document["suggestion"]!;
            return new
            {
                categoryId,
                bankAccountId,
                description = suggestion["description"]!.GetValue<string>(),
                paymentValueCents = suggestion["paymentValueCents"]!.GetValue<int>(),
                paymentMethod = TestUser.PaymentMethodPix,
                transactionDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
            };
        }

        private static async Task<JsonNode> ConfirmAsync(TestUser user, JsonNode document, long categoryId, long bankAccountId)
        {
            var response = await user.Client.PostAsJsonAsync($"/api/transactions/from-document/{document["id"]}",
                ConfirmBody(document, categoryId, bankAccountId));
            return await TestUser.ReadJsonAsync(response);
        }

        internal static Task<JsonNode> WaitForCurrentInsightAsync(TestUser user, string ruleCode, long? notId = null) =>
            Wait.UntilAsync(
                async () => (JsonNode)(await user.GetJsonAsync($"/api/BehavioralInsights/user/{user.UserId}"))
                    .AsArray()
                    .FirstOrDefault(i => i!["ruleCode"]?.GetValue<string>() == ruleCode && i["id"]!.GetValue<long>() != notId)!,
                insight => insight is not null,
                $"o insight {ruleCode}");
    }
}
