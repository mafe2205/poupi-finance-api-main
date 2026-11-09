using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Authentication.Entities.Dtos
{
    public record CreateAutheticationDto    {
        [Required] public long UserId { get; set; }
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string PasswordHash { get; set; } = string.Empty;
        [Required] public UserRole Role { get; set; } = UserRole.user;
        public DateTime LastLogin { get; set; }
    }
}
