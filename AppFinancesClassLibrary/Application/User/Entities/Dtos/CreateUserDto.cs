using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.User.Entities.Dtos
{
    public record CreateUserDto
    {
        [Required] public string Name { get; set; }
        [Required] public string Surname { get; set; }
        [Required] public DateOnly BirthDate { get; set; } = default!;
        [Required] public string Email { get; set; }
        [Required] public string Phone { get; set; }
        [Required] public string User { get; set; }
        [Required] public string Password { get; set; }
    }
}
