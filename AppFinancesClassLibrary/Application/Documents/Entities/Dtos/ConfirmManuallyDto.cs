using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record ConfirmManuallyDto
    {
        [Required] public long Id { get; set; }
        [Required] public bool ConfirmedManually { get; set; }
    }
}
