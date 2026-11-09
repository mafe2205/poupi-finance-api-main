using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface IDocumentParsingService
    {
        ParsedDocumentResult Parse(string textOcr, IEnumerable<CategoryResult> userCategories, CategoryHistory history = null, int? establishmentCnae = null, string establishmentRegistryName = null);
        IReadOnlyList<ParsedDocumentItemResult> ParseItems(string textOcr);
    }
}
