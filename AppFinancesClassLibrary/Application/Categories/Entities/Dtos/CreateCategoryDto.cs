using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Categories.Entities.Dtos
{
    public record CreateCategoryDto
    {
        [Required] public long UserId { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public CategoriesTypes Type { get; set; }
        public bool IsDefault { get; set; }
        public int LimitValueCents { get; set; }
    }
}
