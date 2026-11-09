using AppFinances_API.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace AppFinances_API.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class ReportsTests(ApiFactory factory)
    {
        private const int CategoryTypeEntrada = 0;
        private const int PaymentMethodCredito = 0;
        private const int CardTypeCredito = 0;
        private const int StatusAberta = 0;

        [Fact]
        public async Task Resumo_soma_o_periodo_e_segue_o_formato_do_app()
        {
            var user = await TestUser.CreateAsync(factory);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var start = new DateOnly(today.Year, today.Month, 1);
            var end = today.AddDays(70);

            var wallet = await user.WalletIdAsync();
            var mercado = await user.CreateCategoryAsync("Mercado", limitValueCents: 80_000);
            var salario = (await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/Categories",
                new { name = "Salário", type = CategoryTypeEntrada })))["id"]!.GetValue<long>();

            var conta = (await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/BankAccounts",
                new { bankName = "Banco", agency = "1", accountNumber = "1", balanceCents = 0 })))["id"]!.GetValue<long>();
            var cartao = (await TestUser.ReadJsonAsync(await user.Client.PostAsJsonAsync("/api/Cards",
                new { bankAccountId = conta, name = "Roxinho", type = CardTypeCredito, limitValueCents = 200_000, closingDate = 5, dueDate = 15 })))["id"]!.GetValue<long>();

            await CreateTransactionAsync(user, new { categoryId = salario, bankAccountId = wallet, description = "Salário", paymentMethod = TestUser.PaymentMethodPix, paymentValueCents = 500_000, transactionDate = Iso(today) });
            await CreateTransactionAsync(user, new { categoryId = mercado, bankAccountId = wallet, description = "Feira", paymentMethod = TestUser.PaymentMethodPix, paymentValueCents = 12_000, transactionDate = Iso(today) });
            await CreateTransactionAsync(user, new { categoryId = mercado, cardId = cartao, description = "Supermercado", paymentMethod = PaymentMethodCredito, paymentValueCents = 30_000, transactionDate = Iso(today) });

            var report = await user.GetJsonAsync($"/api/Reports/summary?startDate={Iso(start)}&endDate={Iso(end)}");

            Assert.Equal(Iso(start), report["startDate"]!.GetValue<string>());
            Assert.Equal(Iso(end), report["endDate"]!.GetValue<string>());

            var totals = report["totals"]!;
            Assert.Equal(500_000, totals["incomeCents"]!.GetValue<long>());
            Assert.Equal(42_000, totals["expenseCents"]!.GetValue<long>());
            Assert.Equal(3, totals["transactionCount"]!.GetValue<int>());
            Assert.Equal(0, totals["previousIncomeCents"]!.GetValue<long>());
            Assert.Equal(0, totals["previousExpenseCents"]!.GetValue<long>());

            var monthly = report["monthly"]!.AsArray();
            Assert.Equal($"{start:yyyy-MM}", monthly[0]!["month"]!.GetValue<string>());
            Assert.Equal(42_000, monthly[0]!["expenseCents"]!.GetValue<long>());
            Assert.Empty(report["daily"]!.AsArray()); // período com mais de 62 dias

            var categoriaMercado = report["categories"]!.AsArray().Single(c => c!["categoryId"]!.GetValue<long>() == mercado)!;
            Assert.Equal("Mercado", categoriaMercado["name"]!.GetValue<string>());
            Assert.Equal(TestUser.CategoryTypeSaida, categoriaMercado["type"]!.GetValue<int>());
            Assert.Equal(42_000, categoriaMercado["totalCents"]!.GetValue<long>());
            Assert.Equal(80_000, categoriaMercado["limitValueCents"]!.GetValue<int>());
            Assert.Equal(2, categoriaMercado["transactionCount"]!.GetValue<int>());
            Assert.True(categoriaMercado["active"]!.GetValue<bool>());

            var credito = report["paymentMethods"]!.AsArray().Single(p => p!["paymentMethod"]!.GetValue<int>() == PaymentMethodCredito)!;
            Assert.Equal(30_000, credito["totalCents"]!.GetValue<long>());
            Assert.Equal(1, credito["transactionCount"]!.GetValue<int>());

            var balance = report["balanceHistory"]!.AsArray();
            Assert.Equal(Iso(end), balance[^1]!["date"]!.GetValue<string>());
            Assert.Equal(488_000, balance[^1]!["balanceCents"]!.GetValue<long>());

            var card = report["cards"]!.AsArray().Single()!;
            Assert.Equal(cartao, card["cardId"]!.GetValue<long>());
            Assert.Equal("Roxinho", card["name"]!.GetValue<string>());
            Assert.Equal(CardTypeCredito, card["type"]!.GetValue<int>());
            Assert.Equal(200_000, card["limitValueCents"]!.GetValue<int>());
            Assert.Equal(30_000, card["usedCents"]!.GetValue<long>());

            var invoice = report["invoices"]!.AsArray().Single()!;
            Assert.Equal(StatusAberta, invoice["status"]!.GetValue<int>());
            Assert.Equal(1, invoice["count"]!.GetValue<int>());
            Assert.Equal(30_000, invoice["totalCents"]!.GetValue<long>());

            var top = report["topExpenses"]!.AsArray();
            Assert.Equal(2, top.Count);
            Assert.Equal("Supermercado", top[0]!["description"]!.GetValue<string>());
            Assert.Equal(mercado, top[0]!["categoryId"]!.GetValue<long>());
            Assert.Equal("Mercado", top[0]!["categoryName"]!.GetValue<string>());
            Assert.Equal(30_000, top[0]!["paymentValueCents"]!.GetValue<int>());
            Assert.Equal(Iso(today), top[0]!["transactionDate"]!.GetValue<string>());
            Assert.True(top[0]!["transactionId"]!.GetValue<long>() > 0);
        }

        [Theory]
        [InlineData("2026-11-08", "2026-11-01")] // início depois do fim
        [InlineData("2025-01-01", "2026-01-02")] // 367 dias
        public async Task Periodo_invalido_responde_400(string startDate, string endDate)
        {
            var user = await TestUser.CreateAsync(factory);

            var response = await user.Client.GetAsync($"/api/Reports/summary?startDate={startDate}&endDate={endDate}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

        private static async Task CreateTransactionAsync(TestUser user, object body)
            => await TestUser.EnsureSuccessAsync(await user.Client.PostAsJsonAsync("/api/transactions", body));
    }
}
