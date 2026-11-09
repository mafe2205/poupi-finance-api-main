using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Goals.Entities.Dtos
{
    public record CreateGoalDto
    {
        [Required] public long UserId { get; set; }
        [Required] public DateOnly InicialDate { get; set; }
        [Required] public DateOnly FinalDate { get; set; }
        [Required] public int InicialValueCents { get; set; }
        [Required] public int FinalValueCents { get; set; }
        [Required] public string Description { get; set; }
    }
}