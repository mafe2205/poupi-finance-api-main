using AppFinances_API.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace AppFinances_API.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class BankStatementPaymentsTests(ApiFactory factory)
    {
        private const int PaymentMethodCredito = 0;
        private const int CardTypeCredito = 0;
        private const int StatusAberta = 0;
        private const int StatusPaga = 2;

        [Fact]
        public async Task Fatura_paga_em_partes_desconta_do_saldo_e_guarda_os_pagamentos()
        {
            var user = await TestUser.CreateAsync(factory);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var (conta, outraConta, statementId) = await CreateStatementOf50000Async(user, today);

            var first = await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync(
                $"/api/BankStatements/{statementId}/payments", new { amountCents = 20_000, paymentDate = Iso(today.AddDays(-1)) }));
            Assert.Equal(conta, first["bankAccountId"]!.GetValue<long>());

            var statement = await user.GetJsonAsync($"/api/BankStatements/{statementId}");
            Assert.Equal(StatusAberta, statement["status"]!.GetValue<int>());
            Assert.Null(statement["paymentDate"]?.GetValue<string>());
            Assert.Equal(50_000, statement["totalCents"]!.GetValue<long>());
            Assert.Equal(20_000, statement["paidCents"]!.GetValue<long>());
            Assert.Equal(30_000, statement["remainingCents"]!.GetValue<long>());
            Assert.Equal(80_000, await BalanceAsync(user, conta));

            var tooMuch = await user.Client.PostAsJsonAsync(
                $"/api/BankStatements/{statementId}/payments", new { amountCents = 30_001, paymentDate = Iso(today) });
            Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
            Assert.Equal(80_000, await BalanceAsync(user, conta));

            await TestUser.EnsureSuccessAsync(await user.Client.PostAsJsonAsync(
                $"/api/BankStatements/{statementId}/payments", new { bankAccountId = outraConta, amountCents = 30_000, paymentDate = Iso(today) }));

            statement = await user.GetJsonAsync($"/api/BankStatements/{statementId}");
            Assert.Equal(StatusPaga, statement["status"]!.GetValue<int>());
            Assert.Equal(Iso(today), statement["paymentDate"]!.GetValue<string>());
            Assert.Equal(0, statement["remainingCents"]!.GetValue<long>());
            Assert.Equal(80_000, await BalanceAsync(user, conta));
            Assert.Equal(20_000, await BalanceAsync(user, outraConta));

            var payments = (await user.GetJsonAsync($"/api/BankStatements/{statementId}/payments")).AsArray();
            Assert.Equal(2, payments.Count);
            Assert.Equal(Iso(today.AddDays(-1)), payments[0]!["paymentDate"]!.GetValue<string>());
            Assert.Equal(20_000, payments[0]!["amountCents"]!.GetValue<int>());
            Assert.Equal(outraConta, payments[1]!["bankAccountId"]!.GetValue<long>());
            Assert.Equal(30_000, payments[1]!["amountCents"]!.GetValue<int>());

            var again = await user.Client.PostAsJsonAsync(
                $"/api/BankStatements/{statementId}/payments", new { amountCents = 1, paymentDate = Iso(today) });
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        }

        [Fact]
        public async Task Put_quita_o_que_falta_com_a_conta_do_cartao()
        {
            var user = await TestUser.CreateAsync(factory);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var (conta, _, statementId) = await CreateStatementOf50000Async(user, today);

            await TestUser.EnsureSuccessAsync(await user.Client.PostAsJsonAsync(
                $"/api/BankStatements/{statementId}/payments", new { amountCents = 10_000, paymentDate = Iso(today) }));
            await TestUser.EnsureSuccessAsync(await user.Client.PutAsJsonAsync(
                $"/api/BankStatements/{statementId}", new { id = statementId, paymentDate = Iso(today) }));

            var statement = await user.GetJsonAsync($"/api/BankStatements/{statementId}");
            Assert.Equal(StatusPaga, statement["status"]!.GetValue<int>());
            Assert.Equal(50_000, statement["paidCents"]!.GetValue<long>());
            Assert.Equal(50_000, await BalanceAsync(user, conta));
            Assert.Equal(2, (await user.GetJsonAsync($"/api/BankStatements/{statementId}/payments")).AsArray().Count);
        }

        [Fact]
        public async Task Fatura_de_outro_usuario_nao_pode_ser_paga()
        {
            var owner = await TestUser.CreateAsync(factory);
            var (_, _, statementId) = await CreateStatementOf50000Async(owner, DateOnly.FromDateTime(DateTime.UtcNow));
            var other = await TestUser.CreateAsync(factory);

            var response = await other.Client.PostAsJsonAsync(
                $"/api/BankStatements/{statementId}/payments", new { amountCents = 1_000, paymentDate = Iso(DateOnly.FromDateTime(DateTime.UtcNow)) });
            Assert.False(response.IsSuccessStatusCode);

            var payments = await other.Client.GetAsync($"/api/BankStatements/{statementId}/payments");
            Assert.False(payments.IsSuccessStatusCode);
        }

        private static async Task<(long Conta, long OutraConta, long StatementId)> CreateStatementOf50000Async(TestUser user, DateOnly today)
        {
            var mercado = await user.CreateCategoryAsync("Mercado");
            var conta = await CreateAccountAsync(user, "Banco", 100_000);
            var outraConta = await CreateAccountAsync(user, "Outro banco", 50_000);
            var cartao = (await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/Cards",
                new { bankAccountId = conta, name = "Roxinho", type = CardTypeCredito, limitValueCents = 200_000, closingDate = 5, dueDate = 15 })))["id"]!.GetValue<long>();

            var transaction = await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/transactions", new
            {
                categoryId = mercado,
                cardId = cartao,
                description = "Supermercado",
                paymentMethod = PaymentMethodCredito,
                paymentValueCents = 50_000,
                transactionDate = Iso(today)
            }));

            return (conta, outraConta, transaction["bankStatementId"]!.GetValue<long>());
        }

        private static async Task<long> CreateAccountAsync(TestUser user, string name, int balanceCents)
        {
            var id = (await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/BankAccounts",
                new { bankName = name, agency = "1", accountNumber = "1", balanceCents = 0 })))["id"]!.GetValue<long>();
            await TestUser.EnsureSuccessAsync(await user.Client.PatchAsJsonAsync($"/api/BankAccounts/{id}/balance", balanceCents));
            return id;
        }

        private static async Task<long> BalanceAsync(TestUser user, long accountId)
            => (await user.GetJsonAsync($"/api/BankAccounts/{accountId}"))["balanceCents"]!.GetValue<long>();

        private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");
    }
}
