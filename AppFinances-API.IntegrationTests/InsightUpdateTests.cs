using AppFinances_API.IntegrationTests.Infrastructure;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AppFinances_API.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class InsightUpdateTests(ApiFactory factory)
    {
        [Fact]
        public async Task Editar_a_transacao_desatualiza_o_insight_e_gera_outro()
        {
            var user = await TestUser.CreateAsync(factory);
            var mercado = await user.CreateCategoryAsync("Mercado", limitValueCents: 3000);
            var wallet = await user.WalletIdAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

            var created = await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/transactions", new
            {
                categoryId = mercado,
                bankAccountId = wallet,
                description = "Compra do mês",
                paymentMethod = TestUser.PaymentMethodPix,
                paymentValueCents = 4000,
                transactionDate = today
            }));

            var original = await ReceiptFlowTests.WaitForCurrentInsightAsync(user, "LIMITE_CATEGORIA");

            await TestUser.EnsureSuccessAsync(await user.Client.PutAsJsonAsync($"/api/transactions/{created["id"]}", new
            {
                description = "Compra do mês",
                categoryId = mercado,
                paymentValueCents = 5500,
                transactionDate = today,
                updatedAt = DateTime.UtcNow
            }));

            var replacement = await ReceiptFlowTests.WaitForCurrentInsightAsync(user, "LIMITE_CATEGORIA", notId: original["id"]!.GetValue<long>());

            var all = (await user.GetJsonAsync($"/api/BehavioralInsights/user/{user.UserId}?includeOutdated=true")).AsArray();
            var old = all.Single(i => i!["id"]!.GetValue<long>() == original["id"]!.GetValue<long>())!;

            Assert.Equal("dados_alterados", old["outdatedReason"]!.GetValue<string>());
            Assert.NotNull(old["outdatedAt"]?.GetValue<DateTime?>());
            Assert.Null(replacement["outdatedAt"]?.GetValue<DateTime?>());

            var current = (await user.GetJsonAsync($"/api/BehavioralInsights/user/{user.UserId}")).AsArray();
            Assert.Single(current, i => i!["ruleCode"]?.GetValue<string>() == "LIMITE_CATEGORIA");
        }

        [Fact]
        public async Task Excluir_a_transacao_marca_o_insight_como_nao_aplicavel()
        {
            var user = await TestUser.CreateAsync(factory);
            var mercado = await user.CreateCategoryAsync("Mercado", limitValueCents: 3000);
            var wallet = await user.WalletIdAsync();

            var created = await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/transactions", new
            {
                categoryId = mercado,
                bankAccountId = wallet,
                description = "Compra do mês",
                paymentMethod = TestUser.PaymentMethodPix,
                paymentValueCents = 4000,
                transactionDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
            }));

            var insight = await ReceiptFlowTests.WaitForCurrentInsightAsync(user, "LIMITE_CATEGORIA");

            await TestUser.EnsureSuccessAsync(await user.Client.DeleteAsync($"/api/transactions/{created["id"]}"));

            var outdated = await Wait.UntilAsync(
                async () => (JsonNode)(await user.GetJsonAsync($"/api/BehavioralInsights/user/{user.UserId}?includeOutdated=true"))
                    .AsArray()
                    .Single(i => i!["id"]!.GetValue<long>() == insight["id"]!.GetValue<long>())!,
                i => i["outdatedReason"]?.GetValue<string>() is not null,
                "o insight ser marcado como desatualizado");

            Assert.Equal("nao_se_aplica", outdated["outdatedReason"]!.GetValue<string>());
        }
    }
}
