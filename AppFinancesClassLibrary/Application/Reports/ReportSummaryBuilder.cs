using AppFinancesClassLibrary.Application.Reports.Entities;
using AppFinancesClassLibrary.Application.Reports.Entities.Results;
using AppFinancesClassLibrary.Application.Transactions;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Reports
{
    public static class ReportSummaryBuilder
    {
        public const int MaxPeriodDays = 366;
        public const int MaxDailyDays = 62;
        public const int TopExpensesCount = 5;

        public static int PeriodDays(DateOnly startDate, DateOnly endDate)
            => endDate.DayNumber - startDate.DayNumber + 1;

        public static (DateOnly Start, DateOnly End) PreviousPeriod(DateOnly startDate, DateOnly endDate)
        {
            var previousEnd = startDate.AddDays(-1);
            return (previousEnd.AddDays(-(PeriodDays(startDate, endDate) - 1)), previousEnd);
        }

        public static List<DateOnly> BalancePointDates(DateOnly startDate, DateOnly endDate)
        {
            var points = new List<DateOnly>();
            for (var month = FirstOfMonth(startDate); month <= endDate; month = month.AddMonths(1))
            {
                var lastDay = month.AddMonths(1).AddDays(-1);
                points.Add(lastDay < endDate ? lastDay : endDate);
            }
            return points;
        }

        public static ReportSummaryResult Build(
            DateOnly startDate,
            DateOnly endDate,
            IReadOnlyCollection<ReportTransactionRow> transactions,
            IReadOnlyCollection<ReportBalanceMovementRow> balanceMovements,
            long currentBalanceCents,
            IEnumerable<ReportCard> cards,
            IEnumerable<ReportInvoiceGroup> invoices)
        {
            var (previousStart, previousEnd) = PreviousPeriod(startDate, endDate);

            var inPeriod = transactions.Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate).ToList();
            var inPrevious = transactions.Where(t => t.TransactionDate >= previousStart && t.TransactionDate <= previousEnd).ToList();
            var expenses = inPeriod.Where(IsExpense).ToList();

            return new ReportSummaryResult
            {
                StartDate = startDate,
                EndDate = endDate,
                Totals = new ReportTotals
                {
                    IncomeCents = SumIncome(inPeriod),
                    ExpenseCents = SumExpense(inPeriod),
                    PreviousIncomeCents = SumIncome(inPrevious),
                    PreviousExpenseCents = SumExpense(inPrevious),
                    TransactionCount = inPeriod.Count
                },
                Monthly = BuildMonthly(startDate, endDate, inPeriod),
                Daily = BuildDaily(startDate, endDate, inPeriod),
                Categories = inPeriod
                    .GroupBy(t => t.CategoryId)
                    .Select(group =>
                    {
                        var category = group.First();
                        return new ReportCategory
                        {
                            CategoryId = category.CategoryId,
                            Name = category.CategoryName,
                            Type = (int)category.CategoryType,
                            TotalCents = group.Sum(t => (long)t.PaymentValueCents),
                            LimitValueCents = category.CategoryLimitValueCents > 0 ? category.CategoryLimitValueCents : null,
                            TransactionCount = group.Count(),
                            Active = category.CategoryActive
                        };
                    })
                    .OrderByDescending(c => c.TotalCents)
                    .ToList(),
                PaymentMethods = expenses
                    .GroupBy(t => t.PaymentMethod)
                    .Select(group => new ReportPaymentMethod((int)group.Key, group.Sum(t => (long)t.PaymentValueCents), group.Count()))
                    .OrderByDescending(p => p.TotalCents)
                    .ToList(),
                BalanceHistory = BuildBalanceHistory(startDate, endDate, balanceMovements, currentBalanceCents),
                Cards = cards.ToList(),
                Invoices = invoices.ToList(),
                TopExpenses = expenses
                    .OrderByDescending(t => t.PaymentValueCents)
                    .ThenByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.TransactionId)
                    .Take(TopExpensesCount)
                    .Select(t => new ReportTopExpense(t.TransactionId, t.Description, t.CategoryId, t.CategoryName, t.PaymentValueCents, t.TransactionDate))
                    .ToList()
            };
        }

        private static List<ReportMonth> BuildMonthly(DateOnly startDate, DateOnly endDate, List<ReportTransactionRow> inPeriod)
        {
            var byMonth = inPeriod.ToLookup(t => FirstOfMonth(t.TransactionDate));
            var months = new List<ReportMonth>();

            for (var month = FirstOfMonth(startDate); month <= endDate; month = month.AddMonths(1))
                months.Add(new ReportMonth(month.ToString("yyyy-MM"), SumIncome(byMonth[month]), SumExpense(byMonth[month])));

            return months;
        }

        private static List<ReportDay> BuildDaily(DateOnly startDate, DateOnly endDate, List<ReportTransactionRow> inPeriod)
        {
            if (PeriodDays(startDate, endDate) > MaxDailyDays)
                return [];

            var byDay = inPeriod.ToLookup(t => t.TransactionDate);
            var days = new List<ReportDay>();

            for (var day = startDate; day <= endDate; day = day.AddDays(1))
                days.Add(new ReportDay(day, SumIncome(byDay[day]), SumExpense(byDay[day])));

            return days;
        }

        private static List<ReportBalancePoint> BuildBalanceHistory(
            DateOnly startDate, DateOnly endDate, IReadOnlyCollection<ReportBalanceMovementRow> balanceMovements, long currentBalanceCents)
        {
            return BalancePointDates(startDate, endDate)
                .Select(date =>
                {
                    long afterDate = balanceMovements
                        .Where(m => m.TransactionDate > date)
                        .Sum(m => (long)BalanceDelta.Compute(m.CategoryType, m.MetaDirection, m.PaymentValueCents));
                    return new ReportBalancePoint(date, currentBalanceCents - afterDate);
                })
                .ToList();
        }

        private static bool IsExpense(ReportTransactionRow t) => t.CategoryType == CategoriesTypes.Saida;

        private static long SumIncome(IEnumerable<ReportTransactionRow> rows)
            => rows.Where(t => t.CategoryType == CategoriesTypes.Entrada).Sum(t => (long)t.PaymentValueCents);

        private static long SumExpense(IEnumerable<ReportTransactionRow> rows)
            => rows.Where(IsExpense).Sum(t => (long)t.PaymentValueCents);

        private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
    }
}
