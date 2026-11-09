using AppFinances_API.IntegrationTests.Infrastructure;
using AppFinancesClassLibrary.Application.Categories.Classification;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace AppFinances_API.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class OcrEvaluationTests(ApiFactory factory, ITestOutputHelper output)
    {
        private static readonly decimal[] Thresholds = [0.6m, 0.7m, 0.8m, 0.9m];
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        [EvaluationFact]
        public async Task Avalia_o_ocr_contra_o_gabarito()
        {
            var (expected, excluded) = ReadAnswerKey(Path.Combine(AppContext.BaseDirectory, "gabarito.csv"));
            var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures"))
                .ToDictionary(f => int.Parse(Path.GetFileNameWithoutExtension(f)), Path.GetFileName);

            var user = await TestUser.CreateAsync(factory);
            var categoryNames = new Dictionary<long, string>();
            foreach (var name in expected.Select(e => e.Category).DistinctBy(CategoryClassifier.Normalize))
                categoryNames[await user.CreateCategoryAsync(name)] = name;

            var results = new List<ReceiptResult>();
            foreach (var receipt in expected)
            {
                if (!fixtures.TryGetValue(receipt.Number, out var fixture))
                {
                    excluded.Add($"{receipt.File}: imagem não encontrada em Fixtures");
                    continue;
                }

                var document = await user.UploadAndWaitForOcrAsync(fixture);
                results.Add(Compare(receipt, fixture, document, categoryNames));
            }

            var directory = Directory.CreateDirectory(Path.Combine(ProjectDirectory(), "Resultados"));

            var texts = Directory.CreateDirectory(Path.Combine(directory.FullName, "textos"));
            foreach (var r in results)
                await File.WriteAllTextAsync(Path.Combine(texts.FullName, Path.ChangeExtension(r.Fixture, ".txt")), r.TextOcr, new UTF8Encoding(false));

            var receiptsPath = Path.Combine(directory.FullName, "avaliacao_ocr_comprovantes.csv");
            var summaryPath = Path.Combine(directory.FullName, "avaliacao_ocr_resumo.md");
            await File.WriteAllTextAsync(receiptsPath, ReceiptsCsv(results), new UTF8Encoding(true));
            var summary = Summary(results, excluded);
            await File.WriteAllTextAsync(summaryPath, summary, new UTF8Encoding(false));

            output.WriteLine(summary);
            output.WriteLine($"Resultados em {directory.FullName}");

            Assert.NotEmpty(results);
        }

        private static ReceiptResult Compare(ExpectedReceipt expected, string fixture, JsonNode document, Dictionary<long, string> categoryNames)
        {
            var suggestion = document["suggestion"];
            var value = suggestion?["paymentValueCents"]?.GetValue<int?>();
            var date = suggestion?["transactionDate"]?.GetValue<string>() is { } d ? DateOnly.Parse(d, CultureInfo.InvariantCulture) : (DateOnly?)null;
            var establishment = suggestion?["description"]?.GetValue<string>();
            var categoryId = suggestion?["categoryId"]?.GetValue<long?>();
            var category = categoryId is { } id ? categoryNames.GetValueOrDefault(id) : null;

            var createdAt = document["createdAt"]!.GetValue<DateTime>();
            var finishedAt = document["ocrFinishedAt"]?.GetValue<DateTime?>();

            return new ReceiptResult
            {
                Expected = expected,
                Fixture = fixture,
                Value = value,
                Date = date,
                Establishment = establishment,
                Category = category,
                ValueOutcome = Outcome(value, v => v == expected.ValueCents),
                DateOutcome = Outcome(date, v => v == expected.Date),
                EstablishmentOutcome = Outcome(establishment, v => SameText(v, expected.Establishment)),
                EstablishmentTolerantOutcome = Outcome(establishment, v => SimilarText(v, expected.Establishment)),
                CategoryOutcome = Outcome(category, v => SameText(v, expected.Category)),
                TrustOcr = document["trustOCR"]!.GetValue<decimal>(),
                ValueTrustOcr = document["valueTrustOCR"]?.GetValue<decimal?>(),
                TextOcr = document["textOCR"]?.GetValue<string>() ?? string.Empty,
                Status = document["status"]!.GetValue<int>(),
                ReviewReasons = document["reviewReasons"]!.AsArray().Select(r => r!.GetValue<string>()).ToList(),
                ProcessingMs = finishedAt is { } f ? (f - createdAt).TotalMilliseconds : null,
                ItemCount = document["items"]!.AsArray().Count,
                ItemsTotalDivergence = suggestion?["itemsTotalDivergence"]?.GetValue<bool>() ?? false
            };
        }

        private static FieldOutcome Outcome<T>(T? value, Func<T, bool> isCorrect) =>
            value is null || (value is string s && string.IsNullOrWhiteSpace(s)) ? FieldOutcome.Vazio
            : isCorrect(value) ? FieldOutcome.Acerto
            : FieldOutcome.Erro;
        private static bool SameText(string a, string b) =>
            CategoryClassifier.Normalize(a) == CategoryClassifier.Normalize(b);

        private const double MinSimilarity = 0.8;

        private static bool SimilarText(string a, string b)
        {
            var (x, y) = (CategoryClassifier.Normalize(a), CategoryClassifier.Normalize(b));
            if (x.Length == 0 || y.Length == 0) return false;

            var (shorter, longer) = x.Length <= y.Length ? (x, y) : (y, x);
            if (shorter.Length >= 4 && $" {longer} ".Contains($" {shorter} ")) return true;

            return 1 - (double)Levenshtein(x, y) / longer.Length >= MinSimilarity;
        }

        private static int Levenshtein(string a, string b)
        {
            var previous = Enumerable.Range(0, b.Length + 1).ToArray();
            for (var i = 1; i <= a.Length; i++)
            {
                var current = new int[b.Length + 1];
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1, previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = current;
            }

            return previous[b.Length];
        }

        private static string Summary(List<ReceiptResult> results, List<string> excluded)
        {
            var md = new StringBuilder();
            md.AppendLine("# Avaliação do OCR (PaddleOCR)");
            md.AppendLine();
            md.AppendLine($"Gerado em {DateTime.Now.ToString("dd/MM/yyyy HH:mm", PtBr)}. Comprovantes avaliados: {results.Count}.");
            md.AppendLine();

            if (excluded.Count > 0)
            {
                md.AppendLine("Fora da avaliação:");
                foreach (var reason in excluded) md.AppendLine($"- {reason}");
                md.AppendLine();
            }

            var groups = new List<(string Name, List<ReceiptResult> Items)> { ("Todos", results) };
            groups.AddRange(results.GroupBy(r => r.Expected.Type).OrderBy(g => g.Key).Select(g => ($"Tipo: {g.Key}", g.ToList())));
            groups.AddRange(results.GroupBy(r => r.Expected.Condition).OrderBy(g => g.Key).Select(g => ($"Condição: {g.Key}", g.ToList())));

            md.AppendLine("## Acurácia por campo");
            md.AppendLine();
            md.AppendLine("Acerto / erro / vazio. Campos vazios não são acerto, mas ficam separados dos valores incorretos. " +
                          "Estabelecimento (estrito): texto igual ao gabarito, sem acentos, pontuação e caixa. " +
                          $"Estabelecimento (tolerante): um contém o outro como palavras inteiras ou similaridade de Levenshtein ≥ {Number(MinSimilarity, "0.0")}.");
            md.AppendLine();
            md.AppendLine("| Grupo | N | Valor | Data | Estabelecimento (estrito) | Estabelecimento (tolerante) |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var (name, items) in groups)
                md.AppendLine($"| {name} | {items.Count} | {Field(items, r => r.ValueOutcome)} | {Field(items, r => r.DateOutcome)} | " +
                              $"{Field(items, r => r.EstablishmentOutcome)} | {Field(items, r => r.EstablishmentTolerantOutcome)} |");
            md.AppendLine();

            md.AppendLine("## Classificação das despesas");
            md.AppendLine();
            md.AppendLine("| Grupo | N | Categoria correta | Categoria errada | Pediu confirmação |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var (name, items) in groups)
                md.AppendLine($"| {name} | {items.Count} | {Rate(items, r => r.CategoryOutcome == FieldOutcome.Acerto)} | {Rate(items, r => r.CategoryOutcome == FieldOutcome.Erro)} | {Rate(items, r => r.CategoryOutcome == FieldOutcome.Vazio)} |");
            md.AppendLine();

            md.AppendLine("## Correção, falhas e tempo");
            md.AppendLine();
            md.AppendLine("Exigiu correção: ao menos um campo (valor, data, estabelecimento ou categoria) diferente do gabarito ou vazio, " +
                          "com o estabelecimento comparado pelo critério estrito ou pelo tolerante. " +
                          "Falha de leitura: erro no OCR ou nenhum texto reconhecido. Tempo: do envio da imagem até o fim da leitura.");
            md.AppendLine();
            md.AppendLine("| Grupo | N | Exigiu correção (estrito) | Exigiu correção (tolerante) | Falha de leitura | TrustOCR médio | Tempo médio (s) | Tempo mediano (s) | Tempo máximo (s) |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var (name, items) in groups)
            {
                var times = items.Where(r => r.ProcessingMs is not null).Select(r => r.ProcessingMs!.Value / 1000).OrderBy(t => t).ToList();
                md.AppendLine($"| {name} | {items.Count} | {Rate(items, r => r.NeedsCorrectionStrict)} | {Rate(items, r => r.NeedsCorrection)} | {Rate(items, r => r.ReadFailure)} | " +
                              $"{Number(items.Average(r => (double)r.TrustOcr), "0.000")} | {Number(times.DefaultIfEmpty().Average(), "0.00")} | " +
                              $"{Number(Median(times), "0.00")} | {Number(times.DefaultIfEmpty().Max(), "0.00")} |");
            }
            md.AppendLine();

            md.AppendLine("## Calibração do limiar de confiança");
            md.AppendLine();
            md.AppendLine("Para cada limiar, o comprovante vai para revisão se o TrustOCR ficar abaixo dele ou se houver outro motivo de revisão. " +
                          "Erros sinalizados: comprovantes que exigiram correção (critério tolerante) e foram para revisão. " +
                          "Sinalização desnecessária: comprovantes corretos que foram para revisão. " +
                          "Escolhido: maior taxa de erros sinalizados; no empate, menor sinalização desnecessária.");
            md.AppendLine();
            var wrong = results.Where(r => r.NeedsCorrection).ToList();
            var right = results.Where(r => !r.NeedsCorrection).ToList();
            var calibration = Thresholds
                .Select(t => (Threshold: t, Caught: Share(wrong, r => r.FlaggedAt(t)), Unneeded: Share(right, r => r.FlaggedAt(t))))
                .ToList();
            var chosen = calibration.OrderByDescending(c => c.Caught ?? -1).ThenBy(c => c.Unneeded ?? 2).First().Threshold;
            md.AppendLine($"| Limiar | Erros sinalizados (de {wrong.Count}) | Sinalização desnecessária (de {right.Count}) | |");
            md.AppendLine("|---|---|---|---|");
            foreach (var c in calibration)
                md.AppendLine($"| {Number((double)c.Threshold, "0.0")} | {Count(wrong, r => r.FlaggedAt(c.Threshold))} | {Count(right, r => r.FlaggedAt(c.Threshold))} | {(c.Threshold == chosen ? "escolhido" : "")} |");
            md.AppendLine();

            md.AppendLine("## Motivos de revisão (limiar configurado na API)");
            md.AppendLine();
            md.AppendLine("| Motivo | Comprovantes |");
            md.AppendLine("|---|---|");
            foreach (var reason in results.SelectMany(r => r.ReviewReasons).GroupBy(r => r).OrderByDescending(g => g.Count()))
                md.AppendLine($"| {reason.Key} | {reason.Count()} |");

            return md.ToString();
        }

        private static string Field(List<ReceiptResult> items, Func<ReceiptResult, FieldOutcome> outcome) =>
            $"{Percent(Share(items, r => outcome(r) == FieldOutcome.Acerto))} ({items.Count(r => outcome(r) == FieldOutcome.Acerto)} / " +
            $"{items.Count(r => outcome(r) == FieldOutcome.Erro)} / {items.Count(r => outcome(r) == FieldOutcome.Vazio)})";

        private static string Rate(List<ReceiptResult> items, Func<ReceiptResult, bool> predicate) =>
            $"{Percent(Share(items, predicate))} ({items.Count(predicate)})";

        private static string Count(List<ReceiptResult> items, Func<ReceiptResult, bool> predicate) =>
            items.Count == 0 ? "sem casos" : $"{Percent(Share(items, predicate))} ({items.Count(predicate)})";

        private static double? Share(List<ReceiptResult> items, Func<ReceiptResult, bool> predicate) =>
            items.Count == 0 ? null : (double)items.Count(predicate) / items.Count;

        private static string Percent(double? share) => share is null ? "-" : (share.Value * 100).ToString("0.0", PtBr) + "%";

        private static string Number(double value, string format) => value.ToString(format, PtBr);

        private static double Median(List<double> sorted) =>
            sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

        private static string ReceiptsCsv(List<ReceiptResult> results)
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(';',
                "arquivo", "imagem", "tipo", "condicao",
                "valor_esperado", "valor_obtido", "valor_resultado",
                "data_esperada", "data_obtida", "data_resultado",
                "estabelecimento_esperado", "estabelecimento_obtido", "estabelecimento_resultado", "estabelecimento_resultado_tolerante",
                "categoria_esperada", "categoria_obtida", "categoria_resultado",
                "exigiu_correcao", "exigiu_correcao_tolerante", "trust_ocr", "status", "motivos_revisao", "tempo_ms", "itens", "divergencia_itens", "trust_valor"));

            foreach (var r in results)
            {
                csv.AppendLine(string.Join(';', new object?[]
                {
                    r.Expected.File, r.Fixture, r.Expected.Type, r.Expected.Condition,
                    r.Expected.ValueCents, r.Value, r.ValueOutcome,
                    r.Expected.Date.ToString("yyyy-MM-dd"), r.Date?.ToString("yyyy-MM-dd"), r.DateOutcome,
                    r.Expected.Establishment, r.Establishment, r.EstablishmentOutcome, r.EstablishmentTolerantOutcome,
                    r.Expected.Category, r.Category, r.CategoryOutcome == FieldOutcome.Vazio ? "Pediu confirmacao" : r.CategoryOutcome,
                    r.NeedsCorrectionStrict ? "sim" : "nao", r.NeedsCorrection ? "sim" : "nao", r.TrustOcr.ToString("0.0000", PtBr), r.Status, string.Join(", ", r.ReviewReasons),
                    r.ProcessingMs?.ToString("0", PtBr), r.ItemCount, r.ItemsTotalDivergence ? "sim" : "nao", r.ValueTrustOcr?.ToString("0.0000", PtBr)
                }.Select(v => CsvField(Convert.ToString(v, PtBr)))));
            }

            return csv.ToString();
        }

        private static string CsvField(string? value)
        {
            value = (value ?? string.Empty).ReplaceLineEndings(" ");
            return value.IndexOfAny([';', '"']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        private static (List<ExpectedReceipt> Receipts, List<string> Excluded) ReadAnswerKey(string path)
        {
            var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var header = SplitCsv(lines[0]);
            int Column(string name) => header.IndexOf(name) is var i and >= 0 ? i : throw new InvalidDataException($"Coluna '{name}' não encontrada no gabarito.");
            var (file, type, condition, value, date, establishment, category) =
                (Column("arquivo"), Column("tipo"), Column("condicao"), Column("valor_centavos"), Column("data"), Column("estabelecimento"), Column("categoria"));

            var receipts = new List<ExpectedReceipt>();
            var excluded = new List<string>();
            foreach (var line in lines.Skip(1))
            {
                var fields = SplitCsv(line);
                var name = fields[file].Trim();

                if (!int.TryParse(Path.GetFileNameWithoutExtension(name), out var number)
                    || !int.TryParse(fields[value].Trim(), out var cents)
                    || !DateOnly.TryParseExact(fields[date].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    excluded.Add($"{name}: linha do gabarito fora do formato ({fields[condition].Trim()})");
                    continue;
                }

                receipts.Add(new ExpectedReceipt(name, number, fields[type].Trim(), fields[condition].Trim(), cents, parsedDate,
                    fields[establishment].Trim(), fields[category].Trim()));
            }

            return (receipts, excluded);
        }

        private static List<string> SplitCsv(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (ch == '"') quoted = !quoted;
                else if (ch == ',' && !quoted) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(ch);
            }

            fields.Add(current.ToString());
            return fields;
        }

        private static string ProjectDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !directory.EnumerateFiles("*.csproj").Any())
                directory = directory.Parent;

            return directory?.FullName ?? AppContext.BaseDirectory;
        }

        private enum FieldOutcome { Acerto, Erro, Vazio }

        private sealed record ExpectedReceipt(string File, int Number, string Type, string Condition, int ValueCents, DateOnly Date, string Establishment, string Category);

        private sealed class ReceiptResult
        {
            public required ExpectedReceipt Expected { get; init; }
            public required string Fixture { get; init; }
            public int? Value { get; init; }
            public DateOnly? Date { get; init; }
            public string? Establishment { get; init; }
            public string? Category { get; init; }
            public FieldOutcome ValueOutcome { get; init; }
            public FieldOutcome DateOutcome { get; init; }
            public FieldOutcome EstablishmentOutcome { get; init; }
            public FieldOutcome EstablishmentTolerantOutcome { get; init; }
            public FieldOutcome CategoryOutcome { get; init; }
            public decimal TrustOcr { get; init; }
            public decimal? ValueTrustOcr { get; init; }
            public required string TextOcr { get; init; }
            public int Status { get; init; }
            public required List<string> ReviewReasons { get; init; }
            public double? ProcessingMs { get; init; }
            public int ItemCount { get; init; }
            public bool ItemsTotalDivergence { get; init; }

            public bool NeedsCorrectionStrict => NeedsCorrectionWith(EstablishmentOutcome);

            public bool NeedsCorrection => NeedsCorrectionWith(EstablishmentTolerantOutcome);

            private bool NeedsCorrectionWith(FieldOutcome establishment) =>
                ValueOutcome != FieldOutcome.Acerto || DateOutcome != FieldOutcome.Acerto
                || establishment != FieldOutcome.Acerto || CategoryOutcome != FieldOutcome.Acerto;

            public bool ReadFailure => ReviewReasons.Contains("falha_no_ocr") || ReviewReasons.Contains("texto_ilegivel");

            public bool FlaggedAt(decimal threshold) =>
                TrustOcr < threshold || ReviewReasons.Any(r => r != "baixa_confianca");
        }
    }
}
