using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public class DocumentParsingService : IDocumentParsingService
    {
        private static readonly Regex CurrencyRegex = new(
            @"(?<![\d.,*]\s?)(?:R\$\s*)?(\d{1,3}(?:\.\d{3})*,\d{2}|\d+,\d{2})(?!\d)", RegexOptions.Compiled);

        private static readonly Regex CnpjPrefixRegex = new(
            @"^\W*CNPJ\b\D*[\d\s.,/\-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WordRegex = new(@"\p{L}{3,}", RegexOptions.Compiled);

        private static readonly string[] BankLabels =
            ["instituicao", "nome", "cpf", "cnpj", "valor", "data", "descricao", "id", "dados", "chave", "agencia", "conta", "tipo", "identificador"];

        private static readonly Regex DateRegex = new(
            @"\b(\d{2})[\/\-.](\d{2})[\/\-.](\d{4}|\d{2})\b", RegexOptions.Compiled);

        private static readonly Regex ItemQuantityRegex = new(
            @"(?:^|\s)(?<qty>\d+(?:[.,]\d{1,3})?)\s*(?<unit>[A-Za-z]{1,5})?\s*[xX*]\s*(?:R\$\s*)?(?<price>\d{1,3}(?:\.\d{3})*,\d{2,3}|\d+,\d{2,3})(?<rest>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex ItemColumnsRegex = new(
            @"(?:^|\s)(?<qty>(?<decimalQty>\d+,\d{3})|\d{1,3})\s*(?<unit>UN|UM|KG|LT|ML|PCT|PC|CX|G|L)\b[\s—–]+(?:R\$\s*)?(?<price>\d{1,3}(?:\.\d{3})*[,:]\d{2}|\d+[,:]\d{2})(?<rest>\s.*)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ItemDiscountRegex = new(
            @"^DESCONTO\s+ITE\w*\b\D*?(?<value>\d+,\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ItemTotalRegex = new(
            @"(\d{1,3}(?:\.\d{3})*,\d{2}|\d+,\d{2})(?![\d%])", RegexOptions.Compiled);

        private static readonly Regex ItemPrefixRegex = new(
            @"^(?:\d{1,3}\s+)?(?:(?<code>\d{4,14})\s+)?", RegexOptions.Compiled);

        public ParsedDocumentResult Parse(string textOcr, IEnumerable<CategoryResult> userCategories, CategoryHistory history = null,
            int? establishmentCnae = null, string establishmentRegistryName = null)
        {
            var result = new ParsedDocumentResult();

            if (string.IsNullOrWhiteSpace(textOcr))
                return result;

            var lines = textOcr
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            var value = ExtractValue(lines);
            result.PaymentValueCents = value?.Cents;
            result.PaymentValueSpan = value?.Span;
            result.TransactionDate = ExtractDate(textOcr);
            result.Description = ExtractDescription(lines);
            (result.Cnpj, result.EstablishmentKey) = ExtractEstablishmentId(lines);
            FillMissingDescription(result, history, establishmentRegistryName);
            result.Items = ParseItems(textOcr).ToList();

            var evidence = new CategoryEvidence
            {
                Establishment = result.Description,
                EstablishmentKey = result.EstablishmentKey,
                Cnae = establishmentCnae,
                FullText = textOcr,
                ItemDescriptions = result.Items.Select(i => i.Description).ToList()
            };
            var classification = CategoryClassifier.ClassifyDocument(evidence, userCategories, history);
            result.CategoryId = classification.CategoryId;
            result.CategoryRequiresConfirmation = classification.RequiresConfirmation;
            result.CategoryReason = classification.Reason;

            if (result.Items.Count > 0)
            {
                result.ItemsTotalCents = result.Items.Sum(i => i.TotalCents);
                result.ItemsTotalDivergence = HasItemsTotalDivergence(result.ItemsTotalCents, result.PaymentValueCents, result.Items.Count(i => i.TotalComputed));
            }

            return result;
        }

        private static void FillMissingDescription(ParsedDocumentResult result, CategoryHistory history, string registryName)
        {
            if (result.Description is not null)
            {
                result.DescriptionSource = DocumentDescriptionSource.ocr;
                return;
            }

            if (result.EstablishmentKey is null) return;

            var confirmed = history?.ConfirmedName(result.EstablishmentKey);
            if (confirmed is not null)
            {
                result.Description = confirmed.Length > 200 ? confirmed[..200] : confirmed;
                result.DescriptionSource = DocumentDescriptionSource.historico_usuario;
            }
            else if (result.Cnpj is not null && !string.IsNullOrWhiteSpace(registryName))
            {
                var name = registryName.Trim();
                result.Description = name.Length > 200 ? name[..200] : name;
                result.DescriptionSource = DocumentDescriptionSource.cadastro_cnpj;
            }
        }

        public static bool HasItemsTotalDivergence(int? itemsTotalCents, int? printedTotalCents, int computedItemCount)
        {
            if (printedTotalCents is null) return false;
            return Math.Abs(itemsTotalCents.Value - printedTotalCents.Value) > computedItemCount;
        }

        public IReadOnlyList<ParsedDocumentItemResult> ParseItems(string textOcr)
        {
            var items = new List<ParsedDocumentItemResult>();

            if (string.IsNullOrWhiteSpace(textOcr))
                return items;

            var lines = textOcr
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            string previousLine = null;

            foreach (var line in lines)
            {
                var discount = ItemDiscountRegex.Match(line);
                if (discount.Success)
                {
                    var discountCents = BrazilianMoney.ParseCents(discount.Groups["value"].Value);
                    if (items.Count > 0 && discountCents is > 0 && discountCents < items[^1].TotalCents)
                        items[^1].TotalCents -= discountCents.Value;

                    previousLine = null;
                    continue;
                }

                var match = ItemQuantityRegex.Match(line);
                var columns = !match.Success;
                if (columns)
                    match = ItemColumnsRegex.Match(line);

                if (!match.Success || (columns && !match.Groups["decimalQty"].Success && !ItemTotalRegex.IsMatch(match.Groups["rest"].Value)))
                {
                    previousLine = line;
                    continue;
                }

                var quantity = ToQuantity(match.Groups["qty"].Value);
                var unitPrice = BrazilianMoney.ParseCents(match.Groups["price"].Value.Replace(':', ','));
                if (quantity is null or <= 0 || unitPrice is null)
                {
                    previousLine = line;
                    continue;
                }

                var (code, description) = SplitCodeAndDescription(line[..match.Index]);
                if (description.Count(char.IsLetter) < 2 && previousLine is not null)
                    (code, description) = SplitCodeAndDescription(previousLine);

                previousLine = null;

                if (description.Count(char.IsLetter) < 2)
                    continue;

                var totalMatch = ItemTotalRegex.Matches(match.Groups["rest"].Value).LastOrDefault();
                var total = totalMatch is not null ? BrazilianMoney.ParseCents(totalMatch.Groups[1].Value) : null;

                var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value.ToUpperInvariant() : null;
                if (unit == "UM") unit = "UN"; 

                items.Add(new ParsedDocumentItemResult
                {
                    LineNumber = items.Count + 1,
                    Code = code,
                    Description = description.Length > 200 ? description[..200] : description,
                    Quantity = quantity.Value,
                    Unit = unit,
                    UnitPriceCents = unitPrice.Value,
                    TotalCents = total ?? (int)Math.Round(quantity.Value * unitPrice.Value, MidpointRounding.AwayFromZero),
                    TotalComputed = total is null
                });
            }

            return items;
        }

        private static (string Code, string Description) SplitCodeAndDescription(string text)
        {
            var trimmed = text.Trim();
            var prefix = ItemPrefixRegex.Match(trimmed);
            var code = prefix.Groups["code"].Success ? prefix.Groups["code"].Value : null;
            return (code, trimmed[prefix.Length..].Trim().TrimEnd('—', '–', '-').TrimEnd());
        }

        private static decimal? ToQuantity(string value)
        {
            return decimal.TryParse(value.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static (int Cents, TextSpan Span)? ExtractValue(List<string> lines)
        {
            var amountDue = ValueFromLabeledLine(lines, IsAmountDueLine);
            if (amountDue is not null) return amountDue;

            if (lines.Any(IsAmountDueLine)) return null;

            return ValueFromLabeledLine(lines, IsTotalLine)
                ?? ValueFromLabeledLine(lines, IsValueLine);
        }

        private static bool IsAmountDueLine(string line) =>
            ContainsAny(line, "a pagar");

        private static (int Cents, TextSpan Span)? ValueFromLabeledLine(List<string> lines, Func<string, bool> isLabel)
        {
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                if (!isLabel(lines[i])) continue;

                var lineIndex = i;
                var match = CurrencyRegex.Match(lines[i]);

                if (!match.Success && i + 1 < lines.Count && lines[i + 1].Count(char.IsLetter) <= 2)
                {
                    lineIndex = i + 1;
                    match = CurrencyRegex.Match(lines[i + 1]);
                }

                if (match.Success && BrazilianMoney.ParseCents(match.Groups[1].Value) is > 0 and var cents)
                    return (cents, new TextSpan(lineIndex, match.Groups[1].Index, match.Groups[1].Length));
            }

            return null;
        }

        private static bool IsTotalLine(string line) =>
            ContainsAny(line, "total") &&
            !ContainsAny(line, "subtotal", "total de itens", "qtd", "qtde", "tributos", "aprox");

        private static bool IsValueLine(string line) =>
            ContainsAny(line, "valor") &&
            !ContainsAny(line, "tributos", "aprox", "troco", "desconto", "unit");

        private static bool ContainsAny(string line, params string[] terms)
        {
            var plain = CategoryClassifier.Normalize(line);
            return terms.Any(t => plain.Contains(t, StringComparison.Ordinal));
        }

        private static DateOnly? ExtractDate(string fullText)
        {
            var match = DateRegex.Match(fullText);
            if (!match.Success) return null;

            var day = int.Parse(match.Groups[1].Value);
            var month = int.Parse(match.Groups[2].Value);
            var yearPart = match.Groups[3].Value;
            var year = yearPart.Length == 2 ? 2000 + int.Parse(yearPart) : int.Parse(yearPart);

            try
            {
                return new DateOnly(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static string ExtractDescription(List<string> lines)
        {
            var description = TradeName(lines)
                ?? (lines.Any(l => IsBankLabel(l, "instituicao")) ? RecipientName(lines) : FirstNameLine(lines));
            if (description is null) return null;

            return description.Length > 200 ? description[..200] : description;
        }

        private const int CnpjHeaderLines = 8;

        private static readonly Regex CnpjRegex = new(
            @"(?<!\d)(\d{2})[.\s]?(\d{3})[.\s]?(\d{3})\s?/?\s?(\d{4})\s?-?\s?(\d{2})(?!\d)", RegexOptions.Compiled);

        private static (string Cnpj, string Key) ExtractEstablishmentId(List<string> lines)
        {
            if (lines.Any(l => IsBankLabel(l, "instituicao"))) return RecipientDocument(lines);

            foreach (var line in lines.Take(CnpjHeaderLines))
            {
                foreach (Match match in CnpjRegex.Matches(line))
                {
                    var digits = string.Concat(match.Groups.Values.Skip(1).Select(g => g.Value));
                    if (IsValidCnpj(digits)) return (digits, digits);
                }
            }

            return (null, null);
        }

        private static (string Cnpj, string Key) RecipientDocument(List<string> lines)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (!(IsBankLabel(lines[i], "cpf") || IsBankLabel(lines[i], "cnpj")) || InPayerSection(lines, i)) continue;

                var digits = string.Concat(lines[i].Where(char.IsAsciiDigit));
                if (digits.Length == 14) return IsValidCnpj(digits) ? (digits, digits) : (null, null);
                if (digits.Length == 6 && lines[i].Contains('*')) return (null, $"cpf:{digits}");
                if (digits.Length == 11) return (null, $"cpf:{digits[3..9]}");
                return (null, null);
            }

            return (null, null);
        }

        public static bool IsValidCnpj(string digits)
        {
            if (digits is null || digits.Length != 14 || !digits.All(char.IsAsciiDigit) || digits.Distinct().Count() == 1)
                return false;

            static int CheckDigit(string value, int[] weights)
            {
                var sum = weights.Select((w, i) => (value[i] - '0') * w).Sum();
                var rest = sum % 11;
                return rest < 2 ? 0 : 11 - rest;
            }

            int[] first = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
            int[] second = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
            return CheckDigit(digits, first) == digits[12] - '0' && CheckDigit(digits, second) == digits[13] - '0';
        }

        private static string TradeName(List<string> lines)
        {
            var line = lines.FirstOrDefault(l => CategoryClassifier.Normalize(l).StartsWith("nome fantasia"));
            var colon = line?.IndexOf(':') ?? -1;
            return colon >= 0 && HasWord(line[(colon + 1)..]) ? line[(colon + 1)..].Trim() : null;
        }

        private static string RecipientName(List<string> lines)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (!IsBankLabel(lines[i], "nome") || InPayerSection(lines, i)) continue;

                var after = lines.Skip(i + 1).Take(2).TakeWhile(l => !IsBankLabel(l)).ToList();
                var before = lines.Take(i).TakeLast(Math.Max(after.Count, 1)).ToList();
                while (before.Count > 0 && IsBankLabel(before[0])) before.RemoveAt(0);
                if (before.Any(IsBankLabel)) before.Clear();

                var onLabel = Regex.Replace(lines[i], @"^\W*\p{L}+\W*", "");
                var name = string.Join(' ', before.Append(onLabel).Concat(after).Select(l => l.Trim()).Where(l => l.Length > 0));
                return HasWord(name) ? name : null;
            }

            return null;
        }

        private static bool InPayerSection(List<string> lines, int index)
        {
            for (var i = index - 1; i >= 0; i--)
            {
                var plain = CategoryClassifier.Normalize(lines[i]);
                if (plain.Contains("pagador")) return true;
                if (plain.Contains("recebedor") || plain.Contains("favorecido") || plain.Contains("destinatario") || plain.Contains("beneficiario")) return false;
            }

            return false;
        }

        private static bool IsBankLabel(string line) => BankLabels.Any(label => IsBankLabel(line, label));

        private static bool IsBankLabel(string line, string label)
        {
            var plain = CategoryClassifier.Normalize(line);
            return plain == label || plain.StartsWith(label + " ");
        }

        private const int HeaderLines = 5;

        private static readonly string[] NotNameStarts = ["via", "valor", "total", "data", "composicao", "comprovante", "central"];

        private static readonly Regex AcquirerRegex = new(
            @"^\W*(?:sicoob|sicredi|stone|cielo|getnet|pagseguro|pagbank|safrapay|sumup|infinitepay)\b|\b(?:sicoob|sicredi|stone|cielo|getnet|pagseguro|pagbank|safrapay|sumup|infinitepay)\W*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string FirstNameLine(List<string> lines)
        {
            foreach (var original in lines.Take(HeaderLines))
            {
                var line = AcquirerRegex.Replace(original, "").Trim();
                var plain = CategoryClassifier.Normalize(line);
                if (plain.Length == 0 || NotNameStarts.Any(s => plain == s || plain.StartsWith(s + " ")) || line.Contains("R$") || CurrencyRegex.IsMatch(line)) continue;

                var name = CnpjPrefixRegex.Replace(line, "").Trim().TrimStart('-', '—', '–', '=', '*', '.', ':').Trim();
                if (LooksLikeText(name)) return name;
            }

            return null;
        }

        private static bool HasWord(string text) => text is not null && WordRegex.IsMatch(text);

        private static bool LooksLikeText(string text)
        {
            var visible = text.Where(c => !char.IsWhiteSpace(c)).ToList();
            return visible.Count > 0
                && Regex.IsMatch(text, @"\p{L}{4,}")
                && visible.Count(char.IsLetterOrDigit) >= visible.Count * 0.8
                && visible.Count(char.IsLetter) >= visible.Count * 0.6;
        }
    }
}
