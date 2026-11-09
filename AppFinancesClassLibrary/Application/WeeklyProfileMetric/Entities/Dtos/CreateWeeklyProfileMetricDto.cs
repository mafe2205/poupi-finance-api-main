using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos
{
    public record CreateWeeklyProfileMetricDto
    {
        [Required] public long UserId { get; set; }
        [Required] public string WeekReference { get; set; } = string.Empty;
        [Required] public int TotalExpensesCents { get; set; }
        [Required] public int TotalRevenueCents { get; set; }
        [Required] public decimal PercentageExpenseRelativeRevenue { get; set; }
        [Required] public int QuantityLateTransactions { get; set; }
        [Required] public int QuantityInsightsGenerated { get; set; }
        [Required] public int QuantityInsightsInteracted { get; set; }
    }
}
