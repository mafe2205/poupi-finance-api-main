using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class AccountRulesTests
    {
        private static InsightTestData WithDailySpending(int balanceCents, int lastExpenseOffset = -1)
        {
            var data = new InsightTestData { HistorySince = Days(-40) };
            var account = data.Account(balanceCents);
            var food = data.Category();
            for (var i = 0; i < 10; i++)
                data.Tx(food, 9000, Days(lastExpenseOffset - i * 2), accountId: account.Id);
            return data;
        }

        [Theory]
        [InlineData(10000, true)]   // cobre 3 dias
        [InlineData(40000, false)]  // cobre 13 dias
        [InlineData(-5000, true)]   // saldo negativo
        public void R13_saldo_baixo(int balance, bool fires)
        {
            Assert.Equal(fires, WithDailySpending(balance).Run<LowBalanceRule>().Count == 1);
        }

        [Fact]
        public void R13_com_15_dias_de_historico_nao_gera()
        {
            var data = WithDailySpending(10000);
            data.HistorySince = Days(-15);

            Assert.Empty(data.Run<LowBalanceRule>());
        }

        [Fact]
        public void R13_e_suprimida_quando_o_usuario_parou_de_registrar()
        {
            Assert.Empty(WithDailySpending(10000, lastExpenseOffset: -6).Run<LowBalanceRule>());
        }

        [Fact]
        public void R13_desconta_transacoes_futuras_ja_somadas_ao_saldo()
        {
            var data = WithDailySpending(10000 + 500000);
            var salary = data.Category("Salário", CategoriesTypes.Entrada);
            data.Tx(salary, 500000, Days(20), accountId: data.Data.Accounts[0].Id, createdAt: Days(-1).ToDateTime(TimeOnly.MinValue));

            Assert.Single(data.Run<LowBalanceRule>());
        }

        private static InsightTestData WithStatementDue(int balance, BankStatementsStatus status = BankStatementsStatus.aberta, bool linked = true)
        {
            var data = new InsightTestData();
            var account = data.Account(balance);
            var card = data.Card(500000, accountId: linked ? account.Id : 999);
            data.Statement(card.Id, Days(-5), Days(4), 80000, status: status);
            return data;
        }

        [Fact]
        public void R14_saldo_insuficiente_para_a_fatura()
        {
            Assert.Single(WithStatementDue(50000).Run<InsufficientBalanceForStatementRule>());
            Assert.Empty(WithStatementDue(90000).Run<InsufficientBalanceForStatementRule>());
            Assert.Empty(WithStatementDue(50000, BankStatementsStatus.paga).Run<InsufficientBalanceForStatementRule>());
            Assert.Empty(WithStatementDue(50000, linked: false).Run<InsufficientBalanceForStatementRule>());
        }

        [Fact]
        public void R14_fatura_paga_em_parte_so_conta_o_que_falta()
        {
            var data = WithStatementDue(50000);
            data.Data.Statements[0].PaidCents = 40000;
            Assert.Empty(data.Run<InsufficientBalanceForStatementRule>());

            data = WithStatementDue(30000);
            data.Data.Statements[0].PaidCents = 40000;
            var candidate = Assert.Single(data.Run<InsufficientBalanceForStatementRule>());
            Assert.Contains("valor da fatura: R$ 400,00", candidate.Facts);
        }

        private static InsightTestData WithIdleBalance()
        {
            var data = new InsightTestData { HistorySince = new DateOnly(2026, 7, 1) };
            data.Account(1000000);
            var food = data.Category();
            for (var month = 1; month <= 3; month++)
                data.Tx(food, 200000, MonthStart.AddMonths(-month).AddDays(5), method: PaymentMethod.cartao_credito);
            return data;
        }

        [Fact]
        public void R15_saldo_parado()
        {
            Assert.Single(WithIdleBalance().Run<IdleBalanceRule>());
        }

        [Fact]
        public void R15_saldo_que_caiu_ha_3_semanas_nao_gera()
        {
            var data = WithIdleBalance();
            var account = data.Data.Accounts[0];
            account.BalanceCents = 400000;
            data.Tx(data.Category("Reforma"), 600000, Days(-21), accountId: account.Id);

            Assert.Empty(data.Run<IdleBalanceRule>());
        }

        [Fact]
        public void R15_aporte_em_meta_nos_ultimos_60_dias_nao_gera()
        {
            var data = WithIdleBalance();
            var metas = data.Category("Metas", isDefault: true);
            data.Tx(metas, 10000, Days(-20), metaId: 50, direction: GoalContributionDirection.deposito, method: PaymentMethod.cartao_credito);

            Assert.Empty(data.Run<IdleBalanceRule>());
        }
    }
}
