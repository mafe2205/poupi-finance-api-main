using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos
{
    public record UpdateBehavioralInsightFeedbackDto
    {
        public Feedback? Feedback { get; set; }
    }
}
