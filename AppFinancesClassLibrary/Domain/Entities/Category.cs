using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Domain.Entities
{
    public class Category
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Name { get; set; }
        public CategoriesTypes Type { get; set; }
        public bool IsDefault { get; set; }
        public int LimitValueCents { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Active { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
    }
}
