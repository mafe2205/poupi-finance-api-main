using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos
{
    public record UpdateBehavioralInsightDto
    {
        [Required] public long Id { get; set; }
        [Required] public long? CategoryId { get; set; }
        [Required] public BehavioralInsightTypes Type { get; set; }
        [Required] public string Description { get; set; } = string.Empty;
        [Required] public DateOnly ReferenceDate { get; set; }
        public DateTime? ViewedAt { get; set; }
        public Feedback? Feedback { get; set; }
        public bool? ActionTaken { get; set; }
        public DateTime? ActionTakenEvaluatedAt { get; set; }
    }
}
