using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class BudgetRulesTests
    {
        [Fact]
        public void LimiteCategoria_usa_o_gasto_acumulado_do_mes()
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, 30000, MonthStart.AddDays(1));
            data.Tx(food, 22000, Days(-1));

            var result = Assert.Single(data.Run<CategoryLimitExceededRule>());
            Assert.Equal(InsightRuleCodes.LimiteCategoria, result.RuleCode);
            Assert.Equal(MonthStart, result.ReferenceDate);
        }

        [Fact]
        public void LimiteCategoria_ignora_gasto_do_mes_anterior()
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, 60000, MonthStart.AddDays(-3));
            data.Tx(food, 10000, Days(-1));

            Assert.Empty(data.Run<CategoryLimitExceededRule>());
        }

        [Fact]
        public void LimiteCategoria_nao_conta_aporte_em_meta_como_gasto()
        {
            var data = new InsightTestData();
            var metas = data.Category("Metas", isDefault: true, limitCents: 1000);
            data.Tx(metas, 50000, Days(-1), metaId: 99, direction: GoalContributionDirection.deposito);

            Assert.Empty(data.Run<CategoryLimitExceededRule>());
        }

        [Theory]
        [InlineData(20000, true)]
        [InlineData(12000, false)]
        public void AumentoSemanal_compara_com_a_semana_anterior(int lastWeekCents, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 10000, PreviousWeekStart);
            data.Tx(food, lastWeekCents, LastWeekStart);

            Assert.Equal(fires, data.Run<WeekOverWeekIncreaseRule>().Count == 1);
        }

        [Theory]
        [InlineData(41000, true)]   // 82%
        [InlineData(39000, false)]  // 78%
        [InlineData(50000, false)]  // 100%: quem avisa é o limite estourado
        public void R24_aviso_antecipado_aos_80_por_cento(int spentCents, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, spentCents, Days(-1));

            Assert.Equal(fires, data.Run<BudgetEarlyWarningRule>().Count == 1);
        }

        [Fact]
        public void R24_categoria_sem_limite_nao_gera()
        {
            var data = new InsightTestData();
            data.Tx(data.Category(), 41000, Days(-1));

            Assert.Empty(data.Run<BudgetEarlyWarningRule>());
        }

        [Fact]
        public void R24_novo_mes_permite_novo_aviso()
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, 41000, Days(-1));
            data.History(InsightRuleCodes.AvisoLimite, MonthStart.AddMonths(-1), categoryId: food);

            Assert.Single(data.Run<BudgetEarlyWarningRule>());

            data.History(InsightRuleCodes.AvisoLimite, MonthStart, categoryId: food);
            Assert.Empty(data.Run<BudgetEarlyWarningRule>());
        }

        [Theory]
        [InlineData(60000, 55000, true)]
        [InlineData(60000, 45000, false)]
        public void R25_limite_estourado_dois_meses_seguidos(int lastMonth, int monthBefore, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, lastMonth, MonthStart.AddMonths(-1).AddDays(5));
            data.Tx(food, monthBefore, MonthStart.AddMonths(-2).AddDays(5));
            data.HistorySince = MonthStart.AddMonths(-2);

            var result = data.Run<BudgetUndersizedRule>();
            Assert.Equal(fires, result.Count == 1);
            if (fires) Assert.Contains(result[0].Facts, f => f.Contains("575,00"));
        }

        [Fact]
        public void R25_com_um_mes_fechado_so_nao_gera()
        {
            var data = new InsightTestData();
            var food = data.Category(limitCents: 50000);
            data.Tx(food, 60000, MonthStart.AddMonths(-1).AddDays(5));

            Assert.Empty(data.Run<BudgetUndersizedRule>());
        }

        [Fact]
        public void R26_sugere_limite_so_para_o_top_3_sem_limite()
        {
            var data = new InsightTestData();
            var september = MonthStart.AddMonths(-1);
            var top1 = data.Category("Mercado");
            var top2 = data.Category("Restaurante", limitCents: 10000);
            var top3 = data.Category("Transporte");
            var fourth = data.Category("Lazer");
            for (var i = 0; i < 4; i++) data.Tx(top1, 10000, september.AddDays(i));
            for (var i = 0; i < 3; i++) data.Tx(top2, 9000, september.AddDays(i));
            for (var i = 0; i < 2; i++) data.Tx(top3, 8000, september.AddDays(i));
            data.Tx(fourth, 1000, september.AddDays(1));

            var categories = data.Run<CategoryWithoutLimitRule>().Select(c => c.CategoryId).ToList();
            Assert.Equal(new long?[] { top1, top3 }, categories);
        }

        [Fact]
        public void R26_com_poucas_transacoes_nao_gera()
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 100000, MonthStart.AddMonths(-1).AddDays(3));

            Assert.Empty(data.Run<CategoryWithoutLimitRule>());
        }
    }
}
