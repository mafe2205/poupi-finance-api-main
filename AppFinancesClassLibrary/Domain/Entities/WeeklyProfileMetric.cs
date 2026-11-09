namespace AppFinancesClassLibrary.Domain.Entities
{
    public class WeeklyProfileMetric
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string WeekReference { get; set; }
        public int TotalExpensesCents { get; set; }
        public int TotalRevenueCents { get; set; }
        public decimal PercentageExpenseRelativeRevenue { get; set; }
        public int QuantityLateTransactions { get; set; }
        public int QuantityInsightsGenerated { get; set; }
        public int QuantityInsightsInteracted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
