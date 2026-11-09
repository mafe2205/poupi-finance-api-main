namespace AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos
{
    public record WeeklyProfileMetricResult
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string WeekReference { get; set; } = string.Empty;
        public int TotalExpensesCents { get; set; }
        public int TotalRevenueCents { get; set; }
        public decimal PercentageExpenseRelativeRevenue { get; set; }
        public int QuantityLateTransactions { get; set; }
        public int QuantityInsightsGenerated { get; set; }
        public int QuantityInsightsInteracted { get; set; }
    }
}
