namespace AppFinancesClassLibrary.Application.Documents.Entities.Results
{
    public record OcrExtractionResult
    {
        public string Text { get; set; }
        public decimal TrustOcr { get; set; }
        public IReadOnlyList<float[]> LineConfidences { get; set; }

        public decimal? DigitConfidence(TextSpan span)
        {
            if (span is null || LineConfidences is null) return null;

            var lines = Text.Split('\n');
            if (lines.Length != LineConfidences.Count || span.Line >= lines.Length) return null;

            var line = lines[span.Line];
            var scores = LineConfidences[span.Line];
            if (scores.Length != line.Length || span.Start + span.Length > line.Length) return null;

            var digits = Enumerable.Range(span.Start, span.Length).Where(i => char.IsDigit(line[i])).Select(i => scores[i]).ToList();
            return digits.Count == 0 ? null : (decimal)digits.Min();
        }
    }
}
