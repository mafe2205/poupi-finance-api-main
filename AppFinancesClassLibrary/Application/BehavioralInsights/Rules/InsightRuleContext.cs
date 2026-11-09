using AppFinancesClassLibrary.Application.Options;
using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public class InsightRuleContext
    {
        private readonly Dictionary<long, InsightCategory> categoriesById;
        private readonly Dictionary<long, InsightCard> cardsById;

        public InsightRuleContext(InsightUserData data, InsightRuleOptions options, DateOnly today)
        {
            Data = data;
            Options = options;
            Today = today;
            categoriesById = data.Categories.ToDictionary(c => c.Id);
            cardsById = data.Cards.ToDictionary(c => c.Id);

            var dayOfWeek = (int)today.DayOfWeek;
            var diffToMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            CurrentWeekStart = today.AddDays(-diffToMonday);
        }

        public InsightUserData Data { get; }
        public InsightRuleOptions Options { get; }
        public DateOnly Today { get; }

        #region Períodos
        public DateOnly CurrentWeekStart { get; }
        public DateOnly LastClosedWeekStart => CurrentWeekStart.AddDays(-7);
        public DateOnly LastClosedWeekEnd => CurrentWeekStart.AddDays(-1);

        public DateOnly PreviousWeekStart => CurrentWeekStart.AddDays(-14);
        public DateOnly PreviousWeekEnd => CurrentWeekStart.AddDays(-8);

        public DateOnly CurrentMonthStart => new(Today.Year, Today.Month, 1);
        public DateOnly LastClosedMonthStart => CurrentMonthStart.AddMonths(-1);
        public DateOnly ClosedMonthStart(int monthsAgo) => CurrentMonthStart.AddMonths(-monthsAgo);

        public static DateOnly MonthEnd(DateOnly monthStart) => monthStart.AddMonths(1).AddDays(-1);
        public bool HasHistorySince(DateOnly date) => Data.FirstTransactionDate is { } first && first <= date;
        public bool HasClosedMonths(int months) => HasHistorySince(MonthEnd(ClosedMonthStart(months)));

        public int? DaysSinceLastRegistration => Data.LastRegisteredAt is { } last
            ? Today.DayNumber - DateOnly.FromDateTime(last).DayNumber
            : null;
        #endregion

        #region Categorias e transações
        public InsightCategory Category(long id) => categoriesById.GetValueOrDefault(id);
        public InsightCard Card(long id) => cardsById.GetValueOrDefault(id);
        public bool IsGoalMovement(InsightTransaction t) => t.MetaId is not null || (Category(t.CategoryId)?.IsDefault ?? false);

        public bool IsExpense(InsightTransaction t) => !IsGoalMovement(t) && Category(t.CategoryId)?.Type == CategoriesTypes.Saida;
        public bool IsIncome(InsightTransaction t) => !IsGoalMovement(t) && Category(t.CategoryId)?.Type == CategoriesTypes.Entrada;

        public IEnumerable<InsightTransaction> Between(DateOnly from, DateOnly to) =>
            Data.Transactions.Where(t => t.TransactionDate >= from && t.TransactionDate <= to);

        public IEnumerable<InsightTransaction> Expenses(DateOnly from, DateOnly to) => Between(from, to).Where(IsExpense);
        public IEnumerable<InsightTransaction> Incomes(DateOnly from, DateOnly to) => Between(from, to).Where(IsIncome);

        public long SumExpenses(DateOnly from, DateOnly to) => Expenses(from, to).Sum(t => (long)t.PaymentValueCents);
        public long SumIncomes(DateOnly from, DateOnly to) => Incomes(from, to).Sum(t => (long)t.PaymentValueCents);
        public Dictionary<long, long> ExpensesByCategory(DateOnly from, DateOnly to) =>
            Expenses(from, to)
                .GroupBy(t => t.CategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(t => (long)t.PaymentValueCents));
        public long MonthlyNet(DateOnly monthStart) =>
            SumIncomes(monthStart, MonthEnd(monthStart)) - SumExpenses(monthStart, MonthEnd(monthStart));
        public decimal AverageMonthlySavings(int months) =>
            Enumerable.Range(1, months).Select(i => (decimal)MonthlyNet(ClosedMonthStart(i))).Average();
        public decimal AverageMonthlyExpenses(int months) =>
            Enumerable.Range(1, months)
                .Select(i => (decimal)SumExpenses(ClosedMonthStart(i), MonthEnd(ClosedMonthStart(i))))
                .Average();
        #endregion

        #region Contas
        public long? AccountOf(InsightTransaction t)
        {
            if (t.PaymentMethod == PaymentMethod.cartao_credito) return null;
            if (t.BankAccountId is not null) return t.BankAccountId;
            if (t.CardId is not null) return Card(t.CardId.Value)?.BankAccountId;
            return null;
        }

        public long SignedDelta(InsightTransaction t)
        {
            if (t.MetaDirection is not null)
                return t.MetaDirection == GoalContributionDirection.retirada ? t.PaymentValueCents : -t.PaymentValueCents;

            return Category(t.CategoryId)?.Type switch
            {
                CategoriesTypes.Entrada => t.PaymentValueCents,
                CategoriesTypes.Saida => -t.PaymentValueCents,
                _ => 0
            };
        }

        public long BalanceAt(InsightAccount account, DateOnly date) =>
            account.BalanceCents - Data.Transactions
                .Where(t => t.TransactionDate > date && AccountOf(t) == account.Id)
                .Sum(SignedDelta);

        public long CurrentBalance(InsightAccount account) => BalanceAt(account, Today);
        #endregion

        #region Cartões e faturas
        public IEnumerable<InsightCard> ActiveCreditCards =>
            Data.Cards.Where(c => c.Active && c.Type == CardsType.credito);
        public InsightStatement OpenStatement(InsightCard card) =>
            Data.Statements
                .Where(s => s.CardId == card.Id && s.ClosingDate >= Today)
                .OrderBy(s => s.ClosingDate)
                .FirstOrDefault();
        public IEnumerable<InsightStatement> ClosedStatements(InsightCard card) =>
            Data.Statements
                .Where(s => s.CardId == card.Id && s.ClosingDate < Today)
                .OrderByDescending(s => s.ClosingDate);
        public static DateOnly CycleStart(InsightStatement statement) => statement.ClosingDate.AddMonths(-1).AddDays(1);
        #endregion
    }
}
