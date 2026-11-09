using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Login.Entities.Dtos
{
    public record LoginDto
    {
        [Required] public string Identifier { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }
}
