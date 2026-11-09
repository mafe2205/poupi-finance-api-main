using System.Globalization;
using System.Text.RegularExpressions;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public static class BrazilianMoney
    {
        private static readonly Regex ValueRegex = new(
            @"^(?:R\$\s*)?(?<int>\d{1,3}(?:\.\d{3})+|\d+)(?:,(?<dec>\d{1,3}))?$", RegexOptions.Compiled);

        public static int? ParseCents(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var match = ValueRegex.Match(value.Trim());
            if (!match.Success) return null;

            var normalized = match.Groups["int"].Value.Replace(".", "") +
                (match.Groups["dec"].Success ? "." + match.Groups["dec"].Value : "");

            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                return null;

            var cents = Math.Round(parsed * 100, MidpointRounding.AwayFromZero);
            return cents > int.MaxValue ? null : (int)cents;
        }
    }
}
