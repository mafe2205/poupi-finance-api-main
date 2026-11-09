using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record GetCategoryTotalsByPeriodDto
    {
        [Required] public long userId { get; set; }
        [Required] public long categoryId { get; set; }
        [Required] public DateOnly startDate { get; set; }
        [Required] public DateOnly endDate { get; set; }
    }
}
