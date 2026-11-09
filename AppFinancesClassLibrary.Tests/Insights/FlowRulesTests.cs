using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Insights.InsightTestData;

namespace AppFinancesClassLibrary.Tests.Insights
{
    public class FlowRulesTests
    {
        private static readonly DateOnly September = MonthStart.AddMonths(-1);

        [Theory]
        [InlineData(170000, true)]   // as duas semanas deficitárias
        [InlineData(100000, false)]  // só a última semana deficitária
        public void R16_gastos_maiores_que_receitas(int previousWeekExpenses, bool fires)
        {
            var data = new InsightTestData();
            var salary = data.Category("Salário", CategoriesTypes.Entrada);
            var food = data.Category();
            data.Tx(salary, 150000, PreviousWeekStart);
            data.Tx(food, previousWeekExpenses, PreviousWeekStart.AddDays(1));
            data.Tx(salary, 150000, LastWeekStart);
            data.Tx(food, 170000, LastWeekStart.AddDays(1));

            Assert.Equal(fires, data.Run<SpendingAboveIncomeRule>().Count == 1);
        }

        [Fact]
        public void R16_usuario_sem_entradas_nao_gera()
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 170000, PreviousWeekStart);
            data.Tx(food, 170000, LastWeekStart);

            Assert.Empty(data.Run<SpendingAboveIncomeRule>());
        }

        [Theory]
        [InlineData(380000, true)]   // poupou 5%
        [InlineData(350000, false)]  // poupou 12,5%
        [InlineData(420000, false)]  // déficit: a R16 tem prioridade
        public void R17_taxa_de_poupanca_baixa(int expenses, bool fires)
        {
            var data = new InsightTestData();
            data.Tx(data.Category("Salário", CategoriesTypes.Entrada), 400000, September.AddDays(4));
            var food = data.Category();
            for (var i = 0; i < 9; i++) data.Tx(food, expenses / 9 + (i == 0 ? expenses % 9 : 0), September.AddDays(i + 5));

            Assert.Equal(fires, data.Run<LowSavingsRateRule>().Count == 1);
        }

        [Theory]
        [InlineData(200000, 4, true)]
        [InlineData(250000, 4, false)]
        [InlineData(200000, 2, false)] // só 2 meses de histórico
        public void R18_queda_de_receita(int lastMonthIncome, int monthsOfHistory, bool fires)
        {
            var data = new InsightTestData();
            var salary = data.Category("Salário", CategoriesTypes.Entrada);
            data.Tx(salary, lastMonthIncome, September.AddDays(4));
            for (var month = 2; month <= monthsOfHistory; month++)
                data.Tx(salary, 300000, MonthStart.AddMonths(-month).AddDays(4));

            Assert.Equal(fires, data.Run<IncomeDropRule>().Count == 1);
        }

        [Theory]
        [InlineData(9000, 10, true)]   // média R$ 40, desvio R$ 10, compra de R$ 90
        [InlineData(5500, 10, false)]
        [InlineData(9000, 6, false)]   // só 6 compras no histórico da categoria
        public void R19_gasto_fora_do_padrao(int value, int historyCount, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category();
            for (var i = 0; i < historyCount; i++) data.Tx(food, i % 2 == 0 ? 3000 : 5000, Days(-10 - i * 5));
            data.Tx(food, value, Days(-1));

            Assert.Equal(fires, data.Run<OutlierExpenseRule>().Count == 1);
        }

        [Fact]
        public void R20_gasto_recorrente_detectado()
        {
            var data = new InsightTestData();
            var services = data.Category("Assinaturas");
            data.Tx(services, 3990, Days(-62), "NETFLIX.COM 0911");
            data.Tx(services, 3990, Days(-32), "Netflix.com 1011");
            data.Tx(services, 3990, Days(-2), "netflix.com 1111");

            var result = Assert.Single(data.Run<RecurringExpenseRule>());
            Assert.Equal("netflix com", result.Subject);
            Assert.Contains("total anual estimado: R$ 478,80", result.Facts);
        }

        [Theory]
        [InlineData(new[] { -32, -2 }, new[] { 3990, 3990 })]                 // duas cobranças
        [InlineData(new[] { -62, -32, -2 }, new[] { 3990, 5600, 3990 })]      // variação de 40%
        [InlineData(new[] { -50, -12, -2 }, new[] { 3990, 3990, 3990 })]      // dias aleatórios
        public void R20_nao_gera_sem_padrao_mensal(int[] offsets, int[] values)
        {
            var data = new InsightTestData();
            var services = data.Category("Assinaturas");
            for (var i = 0; i < offsets.Length; i++) data.Tx(services, values[i], Days(offsets[i]), "Netflix");

            Assert.Empty(data.Run<RecurringExpenseRule>());
        }

        [Fact]
        public void R20_nao_repete_a_mesma_cobranca_dentro_de_90_dias()
        {
            var data = new InsightTestData();
            var services = data.Category("Assinaturas");
            data.Tx(services, 3990, Days(-62), "Netflix");
            data.Tx(services, 3990, Days(-32), "Netflix");
            var last = data.Tx(services, 3990, Days(-2), "Netflix");
            data.History(InsightRuleCodes.GastoRecorrente, Days(-30), relatedEntityId: 12345, categoryId: services);
            data.Data.History[^1].RelatedDescription = "NETFLIX";

            Assert.Empty(data.Run<RecurringExpenseRule>());
            Assert.NotEqual(12345, last.Id);
        }

        [Theory]
        [InlineData(55000, 25000, null, true)]
        [InlineData(45000, 30000, null, false)]
        [InlineData(55000, 25000, "mercado", false)] // categoria estrutural excluída na configuração
        public void R21_concentracao_de_gasto(int top, int second, string excluded, bool fires)
        {
            var data = new InsightTestData();
            data.Options.Flow.Concentracao.CategoriasExcluidas = excluded is null ? null : [excluded];
            data.Tx(data.Category("Mercado"), top, September.AddDays(1));
            data.Tx(data.Category("Lazer"), second, September.AddDays(2));
            data.Tx(data.Category("Transporte"), 20000, September.AddDays(3));

            Assert.Equal(fires, data.Run<SpendingConcentrationRule>().Count == 1);
        }

        [Fact]
        public void R21_mes_com_uma_categoria_so_nao_gera()
        {
            var data = new InsightTestData();
            data.Tx(data.Category(), 100000, September.AddDays(1));

            Assert.Empty(data.Run<SpendingConcentrationRule>());
        }

        [Fact]
        public void R22_dia_da_semana_critico()
        {
            var data = new InsightTestData();
            var food = data.Category();
            var days = Enumerable.Range(0, 61).Select(i => new DateOnly(2026, 8, 1).AddDays(i)).ToList();
            var saturdays = days.Where(d => d.DayOfWeek == DayOfWeek.Saturday).ToList();
            var others = days.Where(d => d.DayOfWeek != DayOfWeek.Saturday).ToList();
            for (var i = 0; i < 14; i++) data.Tx(food, 10000, saturdays[i % saturdays.Count]);
            for (var i = 0; i < 16; i++) data.Tx(food, 10000, others[i * 3]);

            var result = Assert.Single(data.Run<CriticalWeekdayRule>());
            Assert.Contains("dia da semana: sábado", result.Facts);
        }

        [Theory]
        [InlineData(30)]  // distribuição uniforme
        [InlineData(20)]  // poucas transações
        public void R22_sem_concentracao_nao_gera(int count)
        {
            var data = new InsightTestData();
            var food = data.Category();
            for (var i = 0; i < count; i++) data.Tx(food, 10000, new DateOnly(2026, 8, 1).AddDays(i * 2));

            Assert.Empty(data.Run<CriticalWeekdayRule>());
        }

        [Theory]
        [InlineData(20000, 12000, true)]
        [InlineData(20000, 15000, false)]
        [InlineData(800, 0, false)]  // semana anterior abaixo do piso de R$ 10
        public void R23_queda_de_gasto(int previousWeek, int lastWeek, bool fires)
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, previousWeek, PreviousWeekStart);
            if (lastWeek > 0) data.Tx(food, lastWeek, LastWeekStart);

            Assert.Equal(fires, data.Run<SpendingDropRule>().Count == 1);
        }

        [Fact]
        public void R23_nao_parabeniza_categoria_que_recebeu_alerta()
        {
            var data = new InsightTestData();
            var food = data.Category();
            data.Tx(food, 20000, PreviousWeekStart);
            data.Tx(food, 12000, LastWeekStart);
            data.History(InsightRuleCodes.LimiteCategoria, MonthStart, categoryId: food,
                type: BehavioralInsightTypes.alerta_gasto, createdAt: PreviousWeekStart.AddDays(3).ToDateTime(TimeOnly.MinValue));

            Assert.Empty(data.Run<SpendingDropRule>());
        }
    }
}
