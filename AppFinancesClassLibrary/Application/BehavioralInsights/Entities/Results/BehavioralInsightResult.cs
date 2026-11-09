using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Results
{
    public record BehavioralInsightResult
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long? CategoryId { get; set; }
        public BehavioralInsightTypes Type { get; set; }
        public string RuleCode { get; set; }
        public long? RelatedEntityId { get; set; }
        public string Description { get; set; }
        public DateOnly ReferenceDate { get; set; }
        public bool WasInteracted { get; set; }
        public DateTime? ViewedAt { get; set; }
        public Feedback Feedback { get; set; }
        public bool ActionTaken { get; set; }
        public DateTime? OutdatedAt { get; set; }
        public string OutdatedReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; } 
    }
}
