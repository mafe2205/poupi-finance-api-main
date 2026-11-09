using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos
{
    public record CreateBehavioralInsightDto
    {
        [Required] public long UserId { get; set; }
        public long? CategoryId { get; set; }
        [Required] public BehavioralInsightTypes Type { get; set; }
        [JsonIgnore] public string RuleCode { get; set; }
        public long? RelatedEntityId { get; set; }
        [JsonIgnore] public string Facts { get; set; }

        [Required] public string Description { get; set; } = string.Empty;
        [Required] public DateOnly ReferenceDate { get; set; }
    }
}
