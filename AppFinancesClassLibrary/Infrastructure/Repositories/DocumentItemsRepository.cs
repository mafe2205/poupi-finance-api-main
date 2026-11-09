using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class DocumentItemsRepository(IUnitOfWork unitOfWork) : IDocumentItemsRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetByDocumentId = @"
            SELECT ID, DOCUMENT_ID, LINE_NUMBER, CODE, DESCRIPTION, QUANTITY, UNIT, UNIT_PRICE_CENTS, TOTAL_CENTS, CREATED_AT, UPDATED_AT
            FROM DOCUMENT_ITEMS
            WHERE DOCUMENT_ID = :DocumentId
            ORDER BY LINE_NUMBER";

        private const string sqlGetByDocumentIds = @"
            SELECT ID, DOCUMENT_ID, LINE_NUMBER, CODE, DESCRIPTION, QUANTITY, UNIT, UNIT_PRICE_CENTS, TOTAL_CENTS, CREATED_AT, UPDATED_AT
            FROM DOCUMENT_ITEMS
            WHERE DOCUMENT_ID = ANY(:DocumentIds)
            ORDER BY DOCUMENT_ID, LINE_NUMBER";

        private const string sqlDeleteByDocumentId = "DELETE FROM DOCUMENT_ITEMS WHERE DOCUMENT_ID = :DocumentId";

        private const string sqlInsert = @"
            INSERT INTO DOCUMENT_ITEMS (DOCUMENT_ID, LINE_NUMBER, CODE, DESCRIPTION, QUANTITY, UNIT, UNIT_PRICE_CENTS, TOTAL_CENTS, CREATED_AT, UPDATED_AT)
            VALUES (:DocumentId, :LineNumber, :Code, :Description, :Quantity, :Unit, :UnitPriceCents, :TotalCents, :CreatedAt, :UpdatedAt)";
        #endregion

        public Task<IEnumerable<DocumentItem>> GetByDocumentIdAsync(long documentId)
        {
            return unitOfWork.Connection.QueryAsync<DocumentItem>(sqlGetByDocumentId, new { DocumentId = documentId }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<DocumentItem>> GetByDocumentIdsAsync(IEnumerable<long> documentIds)
        {
            var ids = documentIds.Distinct().ToArray();
            if (ids.Length == 0)
                return Task.FromResult(Enumerable.Empty<DocumentItem>());

            return unitOfWork.Connection.QueryAsync<DocumentItem>(sqlGetByDocumentIds, new { DocumentIds = ids }, unitOfWork.Transaction);
        }

        public async Task ReplaceForDocumentAsync(long documentId, IEnumerable<ParsedDocumentItemResult> items)
        {
            await unitOfWork.Connection.ExecuteAsync(sqlDeleteByDocumentId, new { DocumentId = documentId }, unitOfWork.Transaction);

            var now = DateTime.UtcNow;
            var param = items.Select(item => new
            {
                DocumentId = documentId,
                item.LineNumber,
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
                await unitOfWork.Connection.ExecuteAsync(sqlInsert, param, unitOfWork.Transaction);
        }
    }
}
