using AppFinances_API.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AppFinances_API.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class AccessAndReactivationTests(ApiFactory factory)
    {
        private const int CardTypeCredito = 0;

        [Fact]
        public async Task Outro_usuario_recebe_403_e_registro_inexistente_404()
        {
            var dono = await TestUser.CreateAsync(factory);
            var outro = await TestUser.CreateAsync(factory);
            var conta = await CreateBankAccountAsync(dono);
            var cartao = await CreateCardAsync(dono, conta);
            var categoria = await dono.CreateCategoryAsync("Mercado");

            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/BankAccounts/{conta}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/BankAccounts/user/{dono.UserId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.DeleteAsync($"/api/BankAccounts/{conta}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/Cards/{cartao}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/Categories/{categoria}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/User/{dono.UserId}")).StatusCode);

            var transacaoComDadosDoOutro = await outro.Client.PostAsJsonAsync("/api/transactions", new
            {
                categoryId = categoria,
                bankAccountId = conta,
                description = "Não pode",
                paymentMethod = TestUser.PaymentMethodPix,
                paymentValueCents = 1000,
                transactionDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
            });
            Assert.Equal(HttpStatusCode.Forbidden, transacaoComDadosDoOutro.StatusCode);

            var inexistente = await dono.Client.GetAsync("/api/BankAccounts/999999999");
            Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
            Assert.Equal("Conta bancária não encontrada.", await ErrorAsync(inexistente));
        }

        [Fact]
        public async Task Reativar_a_conta_traz_de_volta_so_os_cartoes_desativados_com_ela()
        {
            var user = await TestUser.CreateAsync(factory);
            var conta = await CreateBankAccountAsync(user);
            var desativadoAntes = await CreateCardAsync(user, conta, "Antigo");
            var desativadoComAConta = await CreateCardAsync(user, conta, "Principal");

            await TestUser.EnsureSuccessAsync(await user.Client.DeleteAsync($"/api/Cards/{desativadoAntes}"));
            await TestUser.EnsureSuccessAsync(await user.Client.DeleteAsync($"/api/BankAccounts/{conta}"));

            var contasInativas = await user.GetJsonAsync($"/api/BankAccounts/user/{user.UserId}/inactive");
            Assert.Contains(contasInativas.AsArray(), c => c!["id"]!.GetValue<long>() == conta);
            Assert.Equal(2, (await InactiveCardIdsAsync(user)).Count);

            var cartaoAntes = await user.Client.PatchAsync($"/api/Cards/{desativadoComAConta}/reactivate", null);
            Assert.Equal(HttpStatusCode.BadRequest, cartaoAntes.StatusCode);
            Assert.Equal("Reative a conta bancária antes de reativar o cartão.", await ErrorAsync(cartaoAntes));

            var reativada = await TestUser.ReadJsonAsync(await user.Client.PatchAsync($"/api/BankAccounts/{conta}/reactivate", null));
            Assert.True(reativada["active"]!.GetValue<bool>());

            Assert.Equal(new List<long> { desativadoAntes }, await InactiveCardIdsAsync(user));

            var cartao = await TestUser.ReadJsonAsync(await user.Client.PatchAsync($"/api/Cards/{desativadoAntes}/reactivate", null));
            Assert.True(cartao["active"]!.GetValue<bool>());
            Assert.Empty(await InactiveCardIdsAsync(user));
        }

        [Fact]
        public async Task Outro_usuario_nao_reativa_nem_lista_inativos()
        {
            var dono = await TestUser.CreateAsync(factory);
            var outro = await TestUser.CreateAsync(factory);
            var conta = await CreateBankAccountAsync(dono);
            var cartao = await CreateCardAsync(dono, conta);
            await TestUser.EnsureSuccessAsync(await dono.Client.DeleteAsync($"/api/BankAccounts/{conta}"));

            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.PatchAsync($"/api/BankAccounts/{conta}/reactivate", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.PatchAsync($"/api/Cards/{cartao}/reactivate", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/BankAccounts/user/{dono.UserId}/inactive")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.GetAsync($"/api/Cards/user/{dono.UserId}/inactive")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await dono.Client.PatchAsync("/api/BankAccounts/999999999/reactivate", null)).StatusCode);
        }

        [Fact]
        public async Task Conta_sem_cartao_lista_vazio_com_200()
        {
            var user = await TestUser.CreateAsync(factory);
            var conta = await CreateBankAccountAsync(user);

            var response = await user.Client.GetAsync($"/api/Cards/bank-account/{conta}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray());
        }

        [Fact]
        public async Task Feedback_de_insight_sem_categoria_e_salvo_pelo_patch()
        {
            var dono = await TestUser.CreateAsync(factory);
            var outro = await TestUser.CreateAsync(factory);
            var created = await dono.Client.PostAsJsonAsync("/api/BehavioralInsights", new
            {
                type = 0,
                description = "Insight sem categoria",
                referenceDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            });
            await TestUser.EnsureSuccessAsync(created);
            var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!.GetValue<long>();

            var response = await dono.Client.PatchAsJsonAsync($"/api/BehavioralInsights/{id}/feedback", new { feedback = 1 });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var insight = await dono.GetJsonAsync($"/api/BehavioralInsights/{id}");
            Assert.Equal(1, insight["feedback"]!.GetValue<int>());
            Assert.Null(insight["categoryId"]);

            Assert.Equal(HttpStatusCode.BadRequest, (await dono.Client.PatchAsJsonAsync($"/api/BehavioralInsights/{id}/feedback", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outro.Client.PatchAsJsonAsync($"/api/BehavioralInsights/{id}/feedback", new { feedback = 0 })).StatusCode);
        }

        [Fact]
        public async Task Usuario_novo_ja_tem_as_categorias_basicas()
        {
            var user = await TestUser.CreateAsync(factory);

            var categories = (await user.GetJsonAsync($"/api/Categories/user/{user.UserId}")).AsArray();
            var names = categories.Select(c => c!["name"]!.GetValue<string>()).ToList();

            Assert.Contains("Metas", names);
            Assert.Contains("Mercado", names);
            Assert.Contains("Salário", names);
            Assert.Equal(1, categories.Count(c => c!["isDefault"]!.GetValue<bool>()));
            Assert.Equal(1 + AppFinancesClassLibrary.Application.User.Services.StarterCategories.All.Count, categories.Count);
        }

        private static async Task<long> CreateBankAccountAsync(TestUser user)
        {
            var created = await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/BankAccounts", new
            {
                bankName = "Banco Teste",
                agency = "0001",
                accountNumber = "12345-6",
                balanceCents = 0
            }));
            return created["id"]!.GetValue<long>();
        }

        private static async Task<long> CreateCardAsync(TestUser user, long bankAccountId, string name = "Cartão")
        {
            var created = await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/Cards", new
            {
                bankAccountId,
                name,
                type = CardTypeCredito,
                limitValueCents = 100_000,
                closingDate = 5,
                dueDate = 15
            }));
            return created["id"]!.GetValue<long>();
        }

        private static async Task<string> ErrorAsync(HttpResponseMessage response)
            => JsonNode.Parse(await response.Content.ReadAsStringAsync())!["error"]!.GetValue<string>();

        private static async Task<List<long>> InactiveCardIdsAsync(TestUser user)
            => (await user.GetJsonAsync($"/api/Cards/user/{user.UserId}/inactive"))
                .AsArray().Select(c => c!["id"]!.GetValue<long>()).Order().ToList();
    }
}
