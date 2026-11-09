using AppFinancesClassLibrary.Application.Categories.Entities.Results;

namespace AppFinancesClassLibrary.Application.Categories.Classification
{
    public sealed class CategoryHistoryEntry
    {
        public string EstablishmentKey { get; init; }
        public string SuggestedDescription { get; init; }
        public string ConfirmedDescription { get; init; }
        public long CategoryId { get; init; }
    }

    public sealed class CategoryHistory
    {
        public const int MinNameConfirmations = 2;
        private const int MinNameLength = 4;

        public static readonly CategoryHistory Empty = new([]);

        private readonly IReadOnlyList<CategoryHistoryEntry> entries;

        public CategoryHistory(IEnumerable<CategoryHistoryEntry> entries)
        {
            this.entries = entries?.ToList() ?? [];
        }

        public string ConfirmedName(string establishmentKey)
        {
            if (string.IsNullOrEmpty(establishmentKey)) return null;

            return entries
                .LastOrDefault(e => e.EstablishmentKey == establishmentKey && !string.IsNullOrWhiteSpace(e.ConfirmedDescription))
                ?.ConfirmedDescription.Trim();
        }

        internal CategoryClassificationResult Classify(string establishmentKey, string establishment, IReadOnlyList<CategoryResult> categories)
        {
            if (entries.Count == 0) return null;

            var eligible = categories.Select(c => c.Id).ToHashSet();
            var known = entries.Where(e => eligible.Contains(e.CategoryId)).ToList();

            if (!string.IsNullOrEmpty(establishmentKey))
            {
                var byKey = known.Where(e => e.EstablishmentKey == establishmentKey).ToList();
                if (byKey.Count > 0) return Decide(byKey, minConfirmations: 1, term: establishmentKey);
            }

            var key = Key(establishment);
            if (key.Length < MinNameLength) return null;

            var byName = known.Where(e => Key(e.SuggestedDescription) == key || Key(e.ConfirmedDescription) == key).ToList();
            return byName.Count > 0 ? Decide(byName, MinNameConfirmations, establishment) : null;
        }

        private static CategoryClassificationResult Decide(List<CategoryHistoryEntry> matches, int minConfirmations, string term)
        {
            var categoryIds = matches.Select(e => e.CategoryId).Distinct().ToList();

            if (categoryIds.Count > 1)
            {
                return new CategoryClassificationResult
                {
                    RequiresConfirmation = true,
                    Reason = CategoryClassificationReason.multiplas_categorias,
                    MatchedTerm = term,
                    CandidateCategoryIds = categoryIds
                };
            }

            if (matches.Count < minConfirmations) return null;

            return new CategoryClassificationResult
            {
                CategoryId = categoryIds[0],
                RequiresConfirmation = false,
                Reason = CategoryClassificationReason.historico_usuario,
                MatchedTerm = term
            };
        }

        private static string Key(string description) => CategoryClassifier.Canonical(CategoryClassifier.Normalize(description));
    }
}
