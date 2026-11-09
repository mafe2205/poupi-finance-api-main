using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class DocumentConfirmationsRepository(IUnitOfWork unitOfWork) : IDocumentConfirmationsRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlInsert = @"
            INSERT INTO DOCUMENT_CONFIRMATIONS (DOCUMENT_ID, TRANSACTION_ID,
                SUGGESTED_DESCRIPTION, CONFIRMED_DESCRIPTION,
                SUGGESTED_PAYMENT_VALUE_CENTS, CONFIRMED_PAYMENT_VALUE_CENTS,
                SUGGESTED_TRANSACTION_DATE, CONFIRMED_TRANSACTION_DATE,
                SUGGESTED_CATEGORY_ID, SUGGESTED_CATEGORY_REASON, CONFIRMED_CATEGORY_ID,
                SUGGESTED_ITEMS_TOTAL_CENTS, ITEMS_CONFIRMED, ESTABLISHMENT_KEY, SUGGESTED_DESCRIPTION_SOURCE, CREATED_AT, UPDATED_AT)
            VALUES (:DocumentId, :TransactionId,
                :SuggestedDescription, :ConfirmedDescription,
                :SuggestedPaymentValueCents, :ConfirmedPaymentValueCents,
                :SuggestedTransactionDate, :ConfirmedTransactionDate,
                :SuggestedCategoryId, :SuggestedCategoryReason, :ConfirmedCategoryId,
                :SuggestedItemsTotalCents, :ItemsConfirmed, :EstablishmentKey, :SuggestedDescriptionSource, :CreatedAt, :UpdatedAt)";

        private const string sqlInsertItem = @"
            INSERT INTO DOCUMENT_CONFIRMED_ITEMS (DOCUMENT_ID, SUGGESTED_ITEM_ID, LINE_NUMBER, CODE, DESCRIPTION, QUANTITY, UNIT, UNIT_PRICE_CENTS, TOTAL_CENTS, CREATED_AT, UPDATED_AT)
            VALUES (:DocumentId, :SuggestedItemId, :LineNumber, :Code, :Description, :Quantity, :Unit, :UnitPriceCents, :TotalCents, :CreatedAt, :UpdatedAt)";

        // A categoria vem da transação, não da confirmação: se o usuário corrigiu depois, vale a correção.
        // Transações apagadas e aportes em meta ficam de fora.
        private const string sqlGetCategoryHistoryByUser = @"
            SELECT DC.ESTABLISHMENT_KEY AS EstablishmentKey, DC.SUGGESTED_DESCRIPTION AS SuggestedDescription,
                DC.CONFIRMED_DESCRIPTION AS ConfirmedDescription, T.CATEGORY_ID AS CategoryId
            FROM DOCUMENT_CONFIRMATIONS DC
            JOIN DOCUMENTS D ON D.ID = DC.DOCUMENT_ID
            JOIN TRANSACTIONS T ON T.ID = DC.TRANSACTION_ID
            WHERE D.USER_ID = :UserId AND T.USER_ID = :UserId AND T.DELETED_AT IS NULL AND T.META_ID IS NULL
            ORDER BY DC.CREATED_AT, DC.ID";
        #endregion

        public Task CreateAsync(CreateDocumentConfirmationDto dto)
        {
            var now = DateTime.UtcNow;
            var param = new
            {
                dto.DocumentId,
                dto.TransactionId,
                dto.SuggestedDescription,
                dto.ConfirmedDescription,
                dto.SuggestedPaymentValueCents,
                dto.ConfirmedPaymentValueCents,
                dto.SuggestedTransactionDate,
                dto.ConfirmedTransactionDate,
                dto.SuggestedCategoryId,
                dto.SuggestedCategoryReason,
                dto.ConfirmedCategoryId,
                dto.SuggestedItemsTotalCents,
                dto.ItemsConfirmed,
                dto.EstablishmentKey,
                dto.SuggestedDescriptionSource,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.ExecuteAsync(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<CategoryHistoryEntry>> GetCategoryHistoryByUserAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<CategoryHistoryEntry>(sqlGetCategoryHistoryByUser, new { UserId = userId }, unitOfWork.Transaction);
        }

        public async Task CreateItemsAsync(long documentId, IEnumerable<ConfirmDocumentItemDto> items)
        {
            var now = DateTime.UtcNow;
            var param = items.Select((item, index) => new
            {
                DocumentId = documentId,
                item.SuggestedItemId,
                LineNumber = index + 1,
                item.Code,
                item.Description,
                item.Quantity,
                item.Unit,
                item.UnitPriceCents,
                item.TotalCents,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            if (param.Count > 0)
                await unitOfWork.Connection.ExecuteAsync(sqlInsertItem, param, unitOfWork.Transaction);
        }
    }
}
