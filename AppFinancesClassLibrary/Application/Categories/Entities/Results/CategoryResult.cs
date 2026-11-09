using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Categories.Entities.Results
{
    public record CategoryResult
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public CategoriesTypes Type { get; set; }
        public bool IsDefault { get; set; }
        public int? LimitValueCents { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Active { get; set; } = true;
    }
}
