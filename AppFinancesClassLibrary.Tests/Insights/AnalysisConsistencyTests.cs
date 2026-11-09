using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class AnalysisConsistencyTests
    {
        [Fact]
        public void Pouco_historico_bloqueia_regras_de_padrao_e_avisa_a_limitacao()
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 30000, Days(-3));
            data.Tx(food, 1000, Days(-10));
            data.Data.FirstTransactionDate = Days(-10);

            var weekly = InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Semanal, Today);
            var daily = InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Diaria, Today);

            Assert.Empty(weekly);
            Assert.Contains(daily, c => c.RuleCode == InsightRuleCodes.DadosInsuficientes);

            data.Data.FirstTransactionDate = Days(-90);
            var weeklyWithHistory = InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Semanal, Today);
            Assert.Contains(weeklyWithHistory, c => c.RuleCode == InsightRuleCodes.AumentoSemanal);
        }

        [Fact]
        public void Aviso_de_dados_insuficientes_sai_uma_vez_so()
        {
            var data = new InsightTestData();
            data.History(InsightRuleCodes.DadosInsuficientes, Days(-5));

            var daily = InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Diaria, Today);

            Assert.DoesNotContain(daily, c => c.RuleCode == InsightRuleCodes.DadosInsuficientes);
        }

        [Fact]
        public void Mesmos_dados_dao_os_mesmos_insights()
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, 60000, MonthStart.AddDays(2));
            data.HistorySince = Days(-90);
            data.Data.FirstTransactionDate = data.HistorySince;

            var first = InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Diaria, Today);
            var second = InsightRuleEngine.Evaluate(data.Data, data.Options, InsightRuleFrequency.Diaria, Today);

            Assert.Contains(first, c => c.RuleCode == InsightRuleCodes.LimiteCategoria);
            Assert.Equal(first.Select(c => (c.Key, c.ReferenceDate, c.Type, c.FactsText)),
                         second.Select(c => (c.Key, c.ReferenceDate, c.Type, c.FactsText)));
        }

        [Fact]
        public void Numeros_alterados_marcam_o_insight_como_desatualizado()
        {
            var candidate = LimitCandidate(categoryId: 7, spentFact: "gasto no mês até agora: R$ 650,00");
            var stored = Stored(id: 1, candidate, facts: "gasto no mês até agora: R$ 600,00");

            var outdated = Assert.Single(InsightStalenessEvaluator.FindOutdated([stored], [candidate], Today));
            Assert.Equal(InsightOutdatedReason.dados_alterados, outdated.Reason);
        }

        [Fact]
        public void Regra_que_nao_dispara_mais_marca_o_insight_como_nao_aplicavel()
        {
            var candidate = LimitCandidate(categoryId: 7, spentFact: "gasto no mês até agora: R$ 600,00");
            var stored = Stored(id: 1, candidate, facts: candidate.FactsText);

            var outdated = Assert.Single(InsightStalenessEvaluator.FindOutdated([stored], [], Today));
            Assert.Equal(InsightOutdatedReason.nao_se_aplica, outdated.Reason);
        }

        [Fact]
        public void Insight_atualizado_e_de_periodo_antigo_nao_mudam()
        {
            var candidate = LimitCandidate(categoryId: 7, spentFact: "gasto no mês até agora: R$ 600,00");
            var same = Stored(id: 1, candidate, facts: candidate.FactsText);

            var old = Stored(id: 2, candidate, facts: "outros números");
            old.ReferenceDate = MonthStart.AddMonths(-3);

            Assert.Empty(InsightStalenessEvaluator.FindOutdated([same, old], [candidate], Today));
        }

        private static InsightCandidate LimitCandidate(long categoryId, string spentFact) => new()
        {
            RuleCode = InsightRuleCodes.LimiteCategoria,
            Type = BehavioralInsightTypes.alerta_gasto,
            CategoryId = categoryId,
            ReferenceDate = MonthStart,
            Facts = ["categoria: Mercado", spentFact, "limite mensal: R$ 500,00"]
        };

        private static BehavioralInsight Stored(long id, InsightCandidate candidate, string facts) => new()
        {
            Id = id,
            RuleCode = candidate.RuleCode,
            CategoryId = candidate.CategoryId,
            RelatedEntityId = candidate.RelatedEntityId,
            ReferenceDate = candidate.ReferenceDate,
            Facts = facts
        };
    }
}
