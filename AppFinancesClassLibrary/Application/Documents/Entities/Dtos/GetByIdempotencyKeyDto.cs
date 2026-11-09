using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record GetByIdempotencyKeyDto
    {
        [Required] public long UserId { get; set; }
        [Required] public string IdempotencyKey { get; set; }
    }
}
