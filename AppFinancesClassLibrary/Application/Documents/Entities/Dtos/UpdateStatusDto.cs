using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record UpdateStatusDto
    {
        [Required] public long Id { get; set; }
        [Required] public StatusOCRTypes Status { get; set; }
    }
}
