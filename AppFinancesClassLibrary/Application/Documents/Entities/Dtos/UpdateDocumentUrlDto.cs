using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record UpdateDocumentUrlDto
    {
        [Required] public long Id { get; set; }
        [Required] public string DocumentUrl { get; set; }
    }
}
