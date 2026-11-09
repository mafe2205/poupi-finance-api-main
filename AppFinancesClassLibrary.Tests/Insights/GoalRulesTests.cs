using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class GoalRulesTests
    {
        [Theory]
        [InlineData(20000, true)]   // 20% guardado com 50% do prazo
        [InlineData(45000, false)]  // 45% com 50%: dentro da folga de 15 pp
        public void R1_meta_em_ritmo_atrasado(long saved, bool fires)
        {
            var data = new InsightTestData();
            data.Goal(100000, Days(-50), Days(50), contributedCents: saved);

            Assert.Equal(fires, data.Run<GoalBehindScheduleRule>().Count == 1);
        }

        [Fact]
        public void R1_meta_com_10_dias_de_vida_nao_gera()
        {
            var data = new InsightTestData();
            data.Goal(100000, Days(-10), Days(10));

            Assert.Empty(data.Run<GoalBehindScheduleRule>());
        }

        [Fact]
        public void R1_respeita_o_cooldown_de_14_dias()
        {
            var data = new InsightTestData();
            var goal = data.Goal(100000, Days(-50), Days(50), contributedCents: 20000);
            data.History(InsightRuleCodes.MetaAtrasada, Days(-10), relatedEntityId: goal.Id);

            Assert.Empty(data.Run<GoalBehindScheduleRule>());
        }

        private static InsightTestData WithMonthlySavings(int savingsCents)
        {
            var data = new InsightTestData();
            var salary = data.Category("Salário", CategoriesTypes.Entrada);
            var food = data.Category();
            for (var month = 1; month <= 2; month++)
            {
                var start = MonthStart.AddMonths(-month);
                data.Tx(salary, 100000 + savingsCents, start.AddDays(4));
                data.Tx(food, 100000, start.AddDays(10));
            }
            return data;
        }

        [Theory]
        [InlineData(90000, true)]   // falta R$ 900 e a poupança é de R$ 300/mês
        [InlineData(20000, false)]
        public void R2_meta_em_risco_de_vencer(int remaining, bool fires)
        {
            var data = WithMonthlySavings(30000);
            data.Goal(100000, Days(-300), Days(30), contributedCents: 100000 - remaining);

            Assert.Equal(fires, data.Run<GoalDeadlineRiskRule>().Count == 1);
        }

        [Fact]
        public void R2_com_um_mes_de_historico_nao_gera()
        {
            var data = new InsightTestData();
            data.Tx(data.Category(), 1000, MonthStart.AddMonths(-1).AddDays(3));
            data.Goal(100000, Days(-300), Days(30));

            Assert.Empty(data.Run<GoalDeadlineRiskRule>());
        }

        [Fact]
        public void R3_meta_parada_calcula_o_aporte_mensal()
        {
            var data = new InsightTestData();
            data.Goal(100000, Days(-60), Days(90), lastDeposit: Days(-31));

            var result = Assert.Single(data.Run<GoalRequiredContributionRule>());
            Assert.Contains("aporte mensal necessário: R$ 333,34", result.Facts);
            Assert.Contains("meses restantes: 3", result.Facts);
        }

        [Fact]
        public void R3_aporte_recente_ou_prazo_vencido_nao_geram()
        {
            var recent = new InsightTestData();
            recent.Goal(100000, Days(-60), Days(90), lastDeposit: Days(-10));
            Assert.Empty(recent.Run<GoalRequiredContributionRule>());

            var expired = new InsightTestData();
            expired.Goal(100000, Days(-60), Days(-1));
            Assert.Empty(expired.Run<GoalRequiredContributionRule>());
        }

        [Theory]
        [InlineData(90000, true)]
        [InlineData(89000, false)]
        [InlineData(100000, false)]
        public void R4_meta_proxima_de_ser_atingida(long saved, bool fires)
        {
            var data = new InsightTestData();
            data.Goal(100000, Days(-30), Days(30), contributedCents: saved);

            Assert.Equal(fires, data.Run<GoalNearlyReachedRule>().Count == 1);
        }

        [Fact]
        public void R4_gera_uma_unica_vez()
        {
            var data = new InsightTestData();
            var goal = data.Goal(100000, Days(-30), Days(30), contributedCents: 95000);
            data.History(InsightRuleCodes.MetaQuaseAtingida, Days(-200), relatedEntityId: goal.Id);

            Assert.Empty(data.Run<GoalNearlyReachedRule>());
        }

        [Theory]
        [InlineData(100000, true)]
        [InlineData(99900, false)]
        public void R5_meta_concluida(long saved, bool fires)
        {
            var data = new InsightTestData();
            data.Goal(100000, Days(-30), Days(30), contributedCents: saved);

            Assert.Equal(fires, data.Run<GoalReachedRule>().Count == 1);
        }

        [Theory]
        [InlineData(20000, true)]  // aumento de R$ 200 contra aporte necessário de R$ 300
        [InlineData(5000, false)]
        public void R6_gasto_concorrendo_com_a_meta(int increase, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 10000, PreviousWeekStart);
            data.Tx(food, 10000 + increase, LastWeekStart);
            data.Goal(90000, Days(-60), Days(90));

            Assert.Equal(fires, data.Run<SpendingCompetesWithGoalRule>().Count == 1);
        }

        [Fact]
        public void R6_sem_meta_ativa_nao_gera()
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 10000, PreviousWeekStart);
            data.Tx(food, 50000, LastWeekStart);

            Assert.Empty(data.Run<SpendingCompetesWithGoalRule>());
        }
    }
}
