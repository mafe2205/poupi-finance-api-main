using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Authentication.Entities.Dtos
{
    public record ChangePasswordDto
    {
        [Required] public string CurrentPassword { get; set; } = string.Empty;
        [Required] public string NewPassword { get; set; } = string.Empty;
    }
}
