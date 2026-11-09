using AppFinancesClassLibrary.Application.Categories.Classification;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Results
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DocumentDescriptionSource
    {
        /// Lido no comprovante pelo OCR
        ocr,

        /// O OCR não leu; é o nome que o usuário já confirmou para o mesmo CNPJ ou CPF do recebedor
        historico_usuario,

        /// O OCR não leu; é o nome fantasia (ou a razão social) do CNPJ no cadastro da Receita Federal
        cadastro_cnpj
    }

    public class ParsedDocumentResult
    {
        public string Description { get; set; }
        public DocumentDescriptionSource? DescriptionSource { get; set; }
        [JsonIgnore]
        public string Cnpj { get; set; }
        [JsonIgnore]
        public string EstablishmentKey { get; set; }
        public long? CategoryId { get; set; }
        public bool CategoryRequiresConfirmation { get; set; } = true;
        public CategoryClassificationReason CategoryReason { get; set; } = CategoryClassificationReason.sem_correspondencia;
        public int? PaymentValueCents { get; set; }
        [JsonIgnore]
        public TextSpan PaymentValueSpan { get; set; }
        public DateOnly? TransactionDate { get; set; }
        public List<ParsedDocumentItemResult> Items { get; set; } = [];
        public int? ItemsTotalCents { get; set; }
        public bool ItemsTotalDivergence { get; set; }
    }
}
