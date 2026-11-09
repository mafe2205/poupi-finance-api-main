using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class CardRulesTests
    {
        [Theory]
        [InlineData(80000, "FATURA_LIMITE_80")]
        [InlineData(79000, null)]
        [InlineData(100000, "FATURA_LIMITE_100")] // só o nível 100, sem repetir o 80
        public void R7_fatura_perto_do_limite(long openCents, string expectedRule)
        {
            var data = new InsightTestData();
            var card = data.Card(100000);
            data.Statement(card.Id, Days(10), Days(20), openCents);

            var rules = data.Run<StatementNearLimitRule>().Select(c => c.RuleCode).ToList();
            Assert.Equal(expectedRule is null ? Array.Empty<string>() : new[] { expectedRule }, rules);
        }

        [Fact]
        public void R7_nao_repete_o_nivel_no_mesmo_ciclo_e_limite_zero_nao_gera()
        {
            var data = new InsightTestData();
            var card = data.Card(100000);
            data.Statement(card.Id, Days(10), Days(20), 85000);
            data.History("FATURA_LIMITE_80", Days(10), relatedEntityId: card.Id);
            Assert.Empty(data.Run<StatementNearLimitRule>());

            var noLimit = new InsightTestData();
            var other = noLimit.Card(0);
            noLimit.Statement(other.Id, Days(10), Days(20), 85000);
            Assert.Empty(noLimit.Run<StatementNearLimitRule>());
        }

        [Theory]
        [InlineData(3, 50000, BankStatementsStatus.aberta, InsightRuleCodes.VencimentoProximo)]
        [InlineData(3, 50000, BankStatementsStatus.paga, null)]
        [InlineData(4, 50000, BankStatementsStatus.aberta, null)]
        [InlineData(3, 0, BankStatementsStatus.aberta, null)]
        [InlineData(-2, 50000, BankStatementsStatus.aberta, InsightRuleCodes.FaturaVencida)]
        public void R8_vencimento_proximo(int daysToDue, long total, BankStatementsStatus status, string expectedRule)
        {
            var data = new InsightTestData();
            var card = data.Card(100000);
            data.Statement(card.Id, Days(-5), Days(daysToDue), total, status: status);

            var rules = data.Run<StatementDueSoonRule>().Select(c => c.RuleCode).ToList();
            Assert.Equal(expectedRule is null ? Array.Empty<string>() : new[] { expectedRule }, rules);
        }

        [Fact]
        public void R8_fatura_paga_em_parte_continua_avisando_com_o_que_falta()
        {
            var data = new InsightTestData();
            var card = data.Card(100000);
            data.Statement(card.Id, Days(-5), Days(3), 50000).PaidCents = 20000;

            var candidate = Assert.Single(data.Run<StatementDueSoonRule>());
            Assert.Equal(InsightRuleCodes.VencimentoProximo, candidate.RuleCode);
            Assert.Contains("já pago: R$ 200,00", candidate.Facts);
            Assert.Contains("falta pagar: R$ 300,00", candidate.Facts);
        }

        [Fact]
        public void R8_fatura_quitada_pelos_pagamentos_nao_avisa()
        {
            var data = new InsightTestData();
            var card = data.Card(100000);
            data.Statement(card.Id, Days(-5), Days(3), 50000).PaidCents = 50000;

            Assert.Empty(data.Run<StatementDueSoonRule>());
        }

        [Theory]
        [InlineData(130000, true)]
        [InlineData(120000, false)]
        public void R9_fatura_maior_que_a_anterior(long latest, bool fires)
        {
            var data = new InsightTestData { HistorySince = Days(-60) };
            var card = data.Card(500000);
            data.Statement(card.Id, Days(-35), Days(-25), 100000);
            data.Statement(card.Id, Days(-5), Days(5), latest);

            Assert.Equal(fires, data.Run<StatementGrowthRule>().Count == 1);
        }

        [Fact]
        public void R9_cartao_com_uma_fatura_so_nao_gera()
        {
            var data = new InsightTestData();
            var card = data.Card(500000);
            data.Statement(card.Id, Days(-5), Days(5), 130000);

            Assert.Empty(data.Run<StatementGrowthRule>());
        }

        [Theory]
        [InlineData("2026-11-04", 60000, true)]   // 10 de 31 dias: projeção de R$ 1.860
        [InlineData("2026-11-10", 60000, false)]  // ciclo com 4 dias
        [InlineData("2026-11-04", 40000, false)]  // projeção dentro do limite
        public void R10_projecao_de_fatura(string closing, long openCents, bool fires)
        {
            var data = new InsightTestData { HistorySince = Days(-14) };
            var card = data.Card(150000);
            data.Statement(card.Id, DateOnly.Parse(closing), DateOnly.Parse(closing).AddDays(10), openCents, transactionCount: 5);

            Assert.Equal(fires, data.Run<StatementProjectionRule>().Count == 1);
        }

        private static InsightTestData WithConcentratedCard(int otherCardLimit)
        {
            var data = new InsightTestData();
            var food = data.Category();
            var main = data.Card(100000, name: "Principal");
            var other = data.Card(otherCardLimit, name: "Reserva");
            var september = MonthStart.AddMonths(-1);
            for (var i = 0; i < 9; i++) data.Tx(food, 8000, september.AddDays(i), cardId: main.Id, method: PaymentMethod.cartao_credito);
            data.Tx(food, 10000, september.AddDays(12), cardId: other.Id, method: PaymentMethod.cartao_credito);
            return data;
        }

        [Fact]
        public void R11_concentracao_em_um_cartao()
        {
            Assert.Single(WithConcentratedCard(otherCardLimit: 100000).Run<CardConcentrationRule>()); // outro com 10% de uso
            Assert.Empty(WithConcentratedCard(otherCardLimit: 15000).Run<CardConcentrationRule>());   // outro com 66% de uso
        }

        [Fact]
        public void R11_usuario_com_um_cartao_so_nao_gera()
        {
            var data = new InsightTestData();
            var food = data.Category();
            var card = data.Card(100000);
            for (var i = 0; i < 10; i++) data.Tx(food, 8000, MonthStart.AddMonths(-1).AddDays(i), cardId: card.Id, method: PaymentMethod.cartao_credito);

            Assert.Empty(data.Run<CardConcentrationRule>());
        }

        [Theory]
        [InlineData(-61, true)]
        [InlineData(-59, false)]
        public void R12_cartao_sem_uso(int lastUseOffset, bool fires)
        {
            var data = new InsightTestData { HistorySince = Days(-90) };
            data.Card(100000, lastTransaction: Days(lastUseOffset));

            Assert.Equal(fires, data.Run<UnusedCardRule>().Count == 1);
        }

        [Fact]
        public void R12_cartao_novo_sem_transacoes_nao_gera()
        {
            var data = new InsightTestData();
            data.Card(100000, createdOn: Days(-30));

            Assert.Empty(data.Run<UnusedCardRule>());
        }
    }
}
