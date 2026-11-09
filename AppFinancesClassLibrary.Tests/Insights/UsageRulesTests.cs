using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class UsageRulesTests
    {
        [Theory]
        [InlineData(1, -3, true)]
        [InlineData(3, -1, false)]
        [InlineData(0, 0, false)]
        public void R27_documentos_pendentes_de_revisao(int count, int createdOffset, bool fires)
        {
            var data = new InsightTestData();
            for (var i = 0; i < count; i++)
                data.Data.PendingDocuments.Add(new InsightPendingDocument { Id = i + 1, CreatedAt = Days(createdOffset).ToDateTime(TimeOnly.MinValue), ExtractedValueCents = 5000 });

            Assert.Equal(fires, data.Run<PendingDocumentsRule>().Count == 1);
        }

        [Theory]
        [InlineData(20, 5, true)]   // 25%
        [InlineData(20, 3, false)]  // 15%
        [InlineData(10, 5, false)]  // poucas transações
        public void R28_muitas_transacoes_sem_categoria(int total, int uncategorized, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category();
            var others = data.Category("Outros");
            for (var i = 0; i < total; i++)
                data.Tx(i < uncategorized ? others : food, 1000, MonthStart.AddDays(i % 13), $"compra {i}");

            Assert.Equal(fires, data.Run<UncategorizedTransactionsRule>().Count == 1);
        }

        private static InsightTestData WithPause(int lastOffset, int transactions = 12)
        {
            var data = new InsightTestData { HistorySince = Days(-60) };
            var food = data.Category();
            for (var i = 0; i < transactions; i++) data.Tx(food, 1000, Days(lastOffset - i * 3));
            return data;
        }

        [Theory]
        [InlineData(-6, 12, true)]
        [InlineData(-4, 12, false)]
        [InlineData(-6, 3, false)]
        public void R29_sem_registros(int lastOffset, int transactions, bool fires)
        {
            Assert.Equal(fires, WithPause(lastOffset, transactions).Run<NoRecordsRule>().Count == 1);
        }

        [Fact]
        public void R29_no_maximo_tres_lembretes_por_pausa()
        {
            var data = WithPause(-25);
            data.History(InsightRuleCodes.SemRegistros, Days(-20));
            data.History(InsightRuleCodes.SemRegistros, Days(-13));
            data.History(InsightRuleCodes.SemRegistros, Days(-6));

            Assert.Empty(data.Run<NoRecordsRule>());
        }
    }

    public class InsightRuleEngineTests
    {
        [Theory]
        [InlineData(13, false)]
        [InlineData(14, true)]
        public void Projecao_da_fatura_espera_duas_semanas(int historyDays, bool fires)
        {
            var data = new InsightTestData { HistorySince = Days(-historyDays) };
            var card = data.Card(150000);
            data.Statement(card.Id, new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 14), 60000, transactionCount: 5);

            Assert.Equal(fires, data.Run<StatementProjectionRule>().Any(c => c.RuleCode == InsightRuleCodes.ProjecaoFatura));
        }

        [Theory]
        [InlineData(29, false)]
        [InlineData(30, true)]
        public void Cartao_sem_uso_espera_um_mes(int historyDays, bool fires)
        {
            var data = new InsightTestData { HistorySince = Days(-historyDays) };
            data.Card(100000, lastTransaction: Days(-61));

            Assert.Equal(fires, data.Run<UnusedCardRule>().Count == 1);
        }

        [Theory]
        [InlineData(59, false)]
        [InlineData(60, true)]
        public void Fatura_maior_que_a_anterior_espera_dois_meses(int historyDays, bool fires)
        {
            var data = new InsightTestData { HistorySince = Days(-historyDays) };
            var card = data.Card(500000);
            data.Statement(card.Id, Days(-35), Days(-25), 100000);
            data.Statement(card.Id, Days(-5), Days(5), 130000);

            Assert.Equal(fires, data.Run<StatementGrowthRule>().Count == 1);
        }

        [Fact]
        public void Regra_da_situacao_atual_roda_no_primeiro_dia()
        {
            var data = new InsightTestData { HistorySince = Today };
            data.Goal(100000, Days(-30), Days(30), contributedCents: 95000);

            Assert.Contains(data.Run<GoalNearlyReachedRule>(), c => c.RuleCode == InsightRuleCodes.MetaQuaseAtingida);
        }

        [Fact]
        public void Regra_de_um_mes_bloqueada_depois_de_14_dias_nao_gera_aviso_de_dados_insuficientes()
        {
            var data = new InsightTestData { HistorySince = Days(-20) };
            var food = data.Category();
            data.Tx(food, 1000, Days(-20));

            Assert.Empty(data.Run<NoRecordsRule>());
        }

        [Fact]
        public void Segunda_execucao_no_mesmo_dia_nao_duplica()
        {
            var data = new InsightTestData();
            var goal = data.Goal(100000, Days(-30), Days(30), contributedCents: 95000);

            var first = Assert.Single(data.Run<GoalNearlyReachedRule>());
            data.History(first.RuleCode, first.ReferenceDate, relatedEntityId: first.RelatedEntityId);

            Assert.Empty(data.Run<GoalNearlyReachedRule>());
            Assert.Equal(goal.Id, first.RelatedEntityId);
        }

        [Fact]
        public void Insight_antigo_sem_rule_code_nao_bloqueia_regra_diferente()
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, 52000, Days(-1));
            data.History(InsightRuleCodes.AumentoSemanal, MonthStart, categoryId: food, type: BehavioralInsightTypes.sugestao);

            Assert.Single(data.Run<CategoryLimitExceededRule>());
        }

        [Fact]
        public void Todas_as_regras_rodam_sem_dados()
        {
            var data = new InsightTestData();

            var daily = Assert.Single(InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Diaria, Today));
            Assert.Equal(InsightRuleCodes.DadosInsuficientes, daily.RuleCode);

            Assert.Empty(InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Semanal, Today));
            Assert.Empty(InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Mensal, Today));
        }
    }
}
