using System.Globalization;
using System.Text;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public static class InsightText
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static string Money(long cents) => $"R$ {(cents / 100m).ToString("N2", PtBr)}";
        public static string Money(decimal cents) => Money((long)Math.Round(cents));

        public static string Percent(decimal value) => $"{Math.Round(value, 1).ToString("0.#", PtBr)}%";

        public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", PtBr);

        public static IEnumerable<string> StatementPaymentFacts(InsightStatement statement) =>
            statement.PaidCents > 0
                ? [$"já pago: {Money(statement.PaidCents)}", $"falta pagar: {Money(statement.RemainingCents)}"]
                : [];

        public static string Weekday(DayOfWeek day) => PtBr.DateTimeFormat.GetDayName(day);

        public static string NormalizeDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;

            var decomposed = description.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var letters = decomposed
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .Select(ch => char.IsLetter(ch) ? ch : ' ')
                .ToArray();

            return string.Join(' ', new string(letters).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public static string Title(string title) => title.Length > 100 ? title[..100] : title;
    }
}
