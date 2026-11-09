using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Authentication.Entities.Dtos
{
    public record AuthenticationResult
    {
        public long UserId { get; set; }
        public string Username { get; set; }
        public UserRole Role { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
