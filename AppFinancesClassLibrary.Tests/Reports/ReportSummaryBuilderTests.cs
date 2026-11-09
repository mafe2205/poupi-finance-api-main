using AppFinancesClassLibrary.Application.Reports;
using AppFinancesClassLibrary.Application.Reports.Entities;
using AppFinancesClassLibrary.Application.Reports.Entities.Results;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Tests.Reports
{
    public class ReportSummaryBuilderTests
    {
        private static readonly DateOnly Start = new(2026, 9, 1);
        private static readonly DateOnly End = new(2026, 11, 8);

        private static long nextId = 1;

        private static ReportTransactionRow Tx(DateOnly date, int cents, CategoriesTypes type = CategoriesTypes.Saida,
            long categoryId = 10, PaymentMethod method = PaymentMethod.pix, string category = "Mercado",
            int limit = 0, bool active = true) => new()
            {
                TransactionId = nextId++,
                Description = $"tx {cents}",
                CategoryId = categoryId,
                CategoryName = category,
                CategoryType = type,
                CategoryLimitValueCents = limit,
                CategoryActive = active,
                PaymentMethod = method,
                PaymentValueCents = cents,
                TransactionDate = date
            };

        private static ReportSummaryResult Build(IReadOnlyCollection<ReportTransactionRow> rows,
            IReadOnlyCollection<ReportBalanceMovementRow> movements = null, long balance = 0,
            DateOnly? start = null, DateOnly? end = null)
            => ReportSummaryBuilder.Build(start ?? Start, end ?? End, rows, movements ?? [], balance, [], []);

        [Fact]
        public void Periodo_anterior_tem_o_mesmo_tamanho_e_termina_na_vespera()
        {
            var (previousStart, previousEnd) = ReportSummaryBuilder.PreviousPeriod(Start, End);

            Assert.Equal(new DateOnly(2026, 8, 31), previousEnd);
            Assert.Equal(ReportSummaryBuilder.PeriodDays(Start, End), ReportSummaryBuilder.PeriodDays(previousStart, previousEnd));
            Assert.Equal(new DateOnly(2026, 6, 24), previousStart);
        }

        [Fact]
        public void Totais_separam_entrada_e_saida_e_o_periodo_anterior()
        {
            var report = Build([
                Tx(new(2026, 9, 5), 500_000, CategoriesTypes.Entrada, categoryId: 1, category: "Salário"),
                Tx(new(2026, 10, 2), 12_000),
                Tx(new(2026, 11, 8), 3_000),
                Tx(new(2026, 8, 31), 7_000),                                   // período anterior
                Tx(new(2026, 7, 1), 200_000, CategoriesTypes.Entrada, categoryId: 1),
                Tx(new(2026, 6, 23), 99_999),                                   // antes do período anterior
            ]);

            Assert.Equal(500_000, report.Totals.IncomeCents);
            Assert.Equal(15_000, report.Totals.ExpenseCents);
            Assert.Equal(3, report.Totals.TransactionCount);
            Assert.Equal(200_000, report.Totals.PreviousIncomeCents);
            Assert.Equal(7_000, report.Totals.PreviousExpenseCents);
        }

        [Fact]
        public void Mensal_tem_todos_os_meses_do_periodo_com_zero_nos_vazios()
        {
            var report = Build([Tx(new(2026, 9, 10), 1_000), Tx(new(2026, 11, 1), 2_000)]);

            Assert.Equal(new[] { "2026-09", "2026-10", "2026-11" }, report.Monthly.Select(m => m.Month));
            Assert.Equal(new[] { 1_000L, 0L, 2_000L }, report.Monthly.Select(m => m.ExpenseCents));
        }

        [Fact]
        public void Diario_so_ate_62_dias()
        {
            var curto = Build([Tx(new(2026, 11, 3), 1_000)], start: new(2026, 11, 1), end: new(2026, 11, 8));
            Assert.Equal(8, curto.Daily.Count);
            Assert.Equal(1_000, curto.Daily.Single(d => d.Date == new DateOnly(2026, 11, 3)).ExpenseCents);
            Assert.Equal(0, curto.Daily.First().ExpenseCents);

            Assert.Equal(62, Build([], start: new(2026, 9, 1), end: new(2026, 11, 1)).Daily.Count);
            Assert.Empty(Build([], start: new(2026, 9, 1), end: new(2026, 11, 2)).Daily);
        }

        [Fact]
        public void Categorias_somam_e_mantem_as_desativadas()
        {
            var report = Build([
                Tx(new(2026, 9, 1), 1_000, categoryId: 10, limit: 50_000),
                Tx(new(2026, 9, 2), 2_000, categoryId: 10, limit: 50_000),
                Tx(new(2026, 9, 3), 4_000, categoryId: 20, category: "Antiga", active: false),
            ]);

            var mercado = report.Categories.Single(c => c.CategoryId == 10);
            Assert.Equal(3_000, mercado.TotalCents);
            Assert.Equal(2, mercado.TransactionCount);
            Assert.Equal(50_000, mercado.LimitValueCents);
            Assert.Equal((int)CategoriesTypes.Saida, mercado.Type);

            var antiga = report.Categories.Single(c => c.CategoryId == 20);
            Assert.False(antiga.Active);
            Assert.Null(antiga.LimitValueCents);
        }

        [Fact]
        public void Formas_de_pagamento_contam_so_saidas()
        {
            var report = Build([
                Tx(new(2026, 9, 1), 1_000, method: PaymentMethod.pix),
                Tx(new(2026, 9, 2), 2_000, method: PaymentMethod.cartao_credito),
                Tx(new(2026, 9, 3), 3_000, method: PaymentMethod.cartao_credito),
                Tx(new(2026, 9, 4), 9_000, CategoriesTypes.Entrada, categoryId: 1, method: PaymentMethod.pix),
            ]);

            var credito = report.PaymentMethods.Single(p => p.PaymentMethod == (int)PaymentMethod.cartao_credito);
            Assert.Equal(5_000, credito.TotalCents);
            Assert.Equal(2, credito.TransactionCount);
            Assert.Equal(1_000, report.PaymentMethods.Single(p => p.PaymentMethod == (int)PaymentMethod.pix).TotalCents);
        }

        [Fact]
        public void Maiores_gastos_sao_as_5_maiores_saidas()
        {
            var rows = Enumerable.Range(1, 7).Select(i => Tx(new(2026, 9, i), i * 1_000)).ToList();
            rows.Add(Tx(new(2026, 9, 9), 999_999, CategoriesTypes.Entrada, categoryId: 1));

            var report = Build(rows);

            Assert.Equal(new[] { 7_000, 6_000, 5_000, 4_000, 3_000 }, report.TopExpenses.Select(t => t.PaymentValueCents));
        }

        [Fact]
        public void Historico_de_saldo_volta_o_saldo_atual_pelas_transacoes_posteriores()
        {
            ReportBalanceMovementRow Mov(DateOnly date, int cents, CategoriesTypes type, GoalContributionDirection? meta = null)
                => new() { TransactionDate = date, PaymentValueCents = cents, CategoryType = type, MetaDirection = meta };

            var movements = new List<ReportBalanceMovementRow>
            {
                Mov(new(2026, 10, 5), 100_000, CategoriesTypes.Entrada),                         // depois de 30/09
                Mov(new(2026, 11, 2), 20_000, CategoriesTypes.Saida),                            // depois de 31/10
                Mov(new(2026, 11, 3), 5_000, CategoriesTypes.Saida, GoalContributionDirection.deposito),
                Mov(new(2026, 11, 20), 1_000, CategoriesTypes.Saida),                            // depois de endDate
            };

            var report = Build([], movements, balance: 74_000);

            Assert.Equal(
                new[] { new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 8) },
                report.BalanceHistory.Select(b => b.Date));
            Assert.Equal(75_000, report.BalanceHistory[2].BalanceCents);
            Assert.Equal(100_000, report.BalanceHistory[1].BalanceCents);
            Assert.Equal(0, report.BalanceHistory[0].BalanceCents);
        }
    }
}
