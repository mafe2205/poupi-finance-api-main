using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Domain.Enums;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DocumentReviewReason
    {
        falha_no_ocr,
        texto_ilegivel,
        baixa_confianca,
        baixa_confianca_valor,
        valor_nao_encontrado,
        divergencia_itens_total,
        categoria_requer_confirmacao,
        estabelecimento_nao_encontrado
    }

    public static class DocumentReviewPolicy
    {
        public static IReadOnlyList<DocumentReviewReason> Evaluate(
            StatusOCRTypes status,
            string textOcr,
            decimal trustOcr,
            decimal confidenceThreshold,
            ParsedDocumentResult? parsed,
            decimal? valueTrustOcr = null,
            decimal valueConfidenceThreshold = 0m)
        {
            var reasons = new List<DocumentReviewReason>();

            if (status == StatusOCRTypes.erro)
            {
                reasons.Add(DocumentReviewReason.falha_no_ocr);
                return reasons;
            }

            if (status != StatusOCRTypes.processado) return reasons;

            if (string.IsNullOrWhiteSpace(textOcr))
            {
                reasons.Add(DocumentReviewReason.texto_ilegivel);
                reasons.Add(DocumentReviewReason.valor_nao_encontrado);
                return reasons;
            }

            if (trustOcr < confidenceThreshold)
                reasons.Add(DocumentReviewReason.baixa_confianca);

            if (parsed?.PaymentValueCents is null)
                reasons.Add(DocumentReviewReason.valor_nao_encontrado);
            else if (valueTrustOcr < valueConfidenceThreshold)
                reasons.Add(DocumentReviewReason.baixa_confianca_valor);

            if (parsed?.ItemsTotalDivergence == true)
                reasons.Add(DocumentReviewReason.divergencia_itens_total);

            if (parsed is null || parsed.CategoryRequiresConfirmation)
                reasons.Add(DocumentReviewReason.categoria_requer_confirmacao);

            if (string.IsNullOrWhiteSpace(parsed?.Description))
                reasons.Add(DocumentReviewReason.estabelecimento_nao_encontrado);

            return reasons;
        }
    }
}
