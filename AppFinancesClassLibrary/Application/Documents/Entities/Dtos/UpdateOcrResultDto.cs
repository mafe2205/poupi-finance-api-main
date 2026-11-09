using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record UpdateOcrResultDto
    {
        [Required] public long Id { get; set; }
        [Required] public string TextOcr { get; set; }
        [Required] public decimal TrustOcr { get; set; }
        public decimal? ValueTrustOcr { get; set; }
        [Required] public DateTime OcrFinishedAt { get; set; }
        public int? EstablishmentCnae { get; set; }
        public string EstablishmentRegistryName { get; set; }
    }
}
