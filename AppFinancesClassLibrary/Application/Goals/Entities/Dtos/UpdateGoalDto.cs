using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Goals.Entities.Dtos
{
    public record UpdateGoalDto
    {
        [Required] public long Id { get; set; }
        public DateOnly FinalDate { get; set; }
        public int FinalValueCents { get; set; }
        public string Description { get; set; }
    }
}
