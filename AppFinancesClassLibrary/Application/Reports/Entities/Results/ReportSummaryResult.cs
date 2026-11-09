namespace AppFinancesClassLibrary.Application.Reports.Entities.Results
{
    public class ReportSummaryResult
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public ReportTotals Totals { get; set; } = new();
        public List<ReportMonth> Monthly { get; set; } = [];
        public List<ReportDay> Daily { get; set; } = [];
        public List<ReportCategory> Categories { get; set; } = [];
        public List<ReportPaymentMethod> PaymentMethods { get; set; } = [];
        public List<ReportBalancePoint> BalanceHistory { get; set; } = [];
        public List<ReportCard> Cards { get; set; } = [];
        public List<ReportInvoiceGroup> Invoices { get; set; } = [];
        public List<ReportTopExpense> TopExpenses { get; set; } = [];
    }

    public class ReportTotals
    {
        public long IncomeCents { get; set; }
        public long ExpenseCents { get; set; }
        public long PreviousIncomeCents { get; set; }
        public long PreviousExpenseCents { get; set; }
        public int TransactionCount { get; set; }
    }

    public record ReportMonth(string Month, long IncomeCents, long ExpenseCents);

    public record ReportDay(DateOnly Date, long IncomeCents, long ExpenseCents);

    public class ReportCategory
    {
        public long CategoryId { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public long TotalCents { get; set; }
        public int? LimitValueCents { get; set; }
        public int TransactionCount { get; set; }
        public bool Active { get; set; }
    }

    public record ReportPaymentMethod(int PaymentMethod, long TotalCents, int TransactionCount);

    public record ReportBalancePoint(DateOnly Date, long BalanceCents);

    public class ReportCard
    {
        public long CardId { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public int LimitValueCents { get; set; }
        public long UsedCents { get; set; }
    }

    public class ReportInvoiceGroup
    {
        public int Status { get; set; }
        public int Count { get; set; }
        public long TotalCents { get; set; }
    }

    public record ReportTopExpense(
        long TransactionId,
        string Description,
        long CategoryId,
        string CategoryName,
        int PaymentValueCents,
        DateOnly TransactionDate);
}
