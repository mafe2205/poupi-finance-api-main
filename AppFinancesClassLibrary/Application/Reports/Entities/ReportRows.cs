using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Reports.Entities
{
    public class ReportTransactionRow
    {
        public long TransactionId { get; set; }
        public string Description { get; set; }
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }
        public CategoriesTypes CategoryType { get; set; }
        public int CategoryLimitValueCents { get; set; }
        public bool CategoryActive { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public int PaymentValueCents { get; set; }
        public DateOnly TransactionDate { get; set; }
    }

    public class ReportBalanceMovementRow
    {
        public CategoriesTypes CategoryType { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public int PaymentValueCents { get; set; }
        public DateOnly TransactionDate { get; set; }
    }
}
