using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Categories.Classification
{
    public static class TransactionReclassifier
    {
        public static long? NewCategoryFor(Transaction transaction, IEnumerable<CategoryResult> userCategories, CategoryHistory history = null)
        {
            if (transaction.CategoryConfirmedManually || transaction.MetaId is not null) return null;

            var current = userCategories.FirstOrDefault(c => c.Id == transaction.CategoryId);
            if (current is null) return null;

            var byHistory = (history ?? CategoryHistory.Empty).Classify(null, transaction.DescriptionOCR,
                userCategories.Where(c => c.Active && !c.IsDefault).ToList());
            if (byHistory is not null)
                return byHistory.CategoryId is { } historyId && historyId != transaction.CategoryId
                    && userCategories.First(c => c.Id == historyId).Type == current.Type ? historyId : null;

            var classification = CategoryClassifier.Classify(transaction.Description, userCategories);
            if (classification.RequiresConfirmation && !string.IsNullOrWhiteSpace(transaction.DescriptionOCR))
                classification = CategoryClassifier.Classify(transaction.DescriptionOCR, userCategories);

            if (classification.RequiresConfirmation || classification.CategoryId is not { } suggestedId) return null;
            if (suggestedId == transaction.CategoryId) return null;

            var suggested = userCategories.First(c => c.Id == suggestedId);
            return suggested.Type == current.Type ? suggestedId : null;
        }
    }
}
