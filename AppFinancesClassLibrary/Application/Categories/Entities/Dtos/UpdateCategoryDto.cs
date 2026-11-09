using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Categories.Entities.Dtos
{
    public record UpdateCategoryDto
    {
        [Required] public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int LimitValueCents { get; set; }
    }
}
