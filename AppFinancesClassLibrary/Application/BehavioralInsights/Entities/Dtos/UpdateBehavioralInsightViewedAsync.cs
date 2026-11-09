using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos
{
    public class UpdateBehavioralInsightViewedAsync
    {
        [Required] public long Id { get; set; }
        [Required] public long UserId { get; set; }
        [Required] public DateTime ViewedAt { get; set; }
        [Required] public bool WasInteracted { get; set; }
    }
}
