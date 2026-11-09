using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Options;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;
using Microsoft.Extensions.Options;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public class DocumentsService(
        IDocumentsRepository documentsRepository,
        IDocumentItemsRepository documentItemsRepository,
        ICategoriesRepository categoriesRepository,
        IDocumentParsingService documentParsingService,
        IOptions<OcrOptions> ocrOptions,
        ITransactionsRepository transactionsRepository,
        IDocumentConfirmationsRepository documentConfirmationsRepository,
        ICurrentUser currentUser) : IDocumentsService
    {
        private readonly IDocumentsRepository documentsRepository = documentsRepository;
        private readonly IDocumentItemsRepository documentItemsRepository = documentItemsRepository;
        private readonly ICategoriesRepository categoriesRepository = categoriesRepository;
        private readonly IDocumentParsingService documentParsingService = documentParsingService;
        private readonly OcrOptions ocrOptions = ocrOptions.Value;
        private readonly ITransactionsRepository transactionsRepository = transactionsRepository;
        private readonly IDocumentConfirmationsRepository documentConfirmationsRepository = documentConfirmationsRepository;

        public async Task<long> CreateAsync(CreateDocumentDto dto)
        {
            Domain.Validate.NotNull(dto, nameof(CreateDocumentDto));

            dto.UserId = currentUser.ResolveUserId(dto.UserId);
            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));

            if (dto.TransactionId != null)
                Domain.Validate.Id(dto.TransactionId.Value, nameof(dto.TransactionId));

            Domain.Validate.String(dto.DocumentUrl, nameof(dto.DocumentUrl), minLength: 1, maxLength: 2000);
            Domain.Validate.Decimal(dto.TrustOCR, nameof(dto.TrustOCR), min: 0, max: 1);
            Domain.Validate.Enum(dto.Status, nameof(dto.Status));

            if (dto.TransactionId is long transactionId)
            {
                var transaction = await transactionsRepository.GetByIdAsync(transactionId);
                if (transaction is null || transaction.UserId != dto.UserId || transaction.DeletedAt is not null)
                    throw new DomainValidationException("Transação não encontrada.");
            }

            var created = await documentsRepository.CreateAsync(dto)
                ?? throw new DomainValidationException("IdempotencyKey já usada em outro documento deste usuário.");
            return created.Id;
        }

        public async Task<DocumentResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            var document = await GetOwnedAsync(id);

            return await MapWithReviewFlagAsync(document);
        }

        public async Task<IEnumerable<DocumentResult>> GetAllByUserIdAsync(long userId)
        {
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);

            var documents = (await documentsRepository.GetAllByUserIdAsync(userId)).ToList();
            var categories = (await categoriesRepository.GetAllByUserIdAsync(userId)).ToList();
            var itemsByDocument = (await documentItemsRepository.GetByDocumentIdsAsync(documents.Select(d => d.Id)))
                .ToLookup(item => item.DocumentId);
            var history = new CategoryHistory(await documentConfirmationsRepository.GetCategoryHistoryByUserAsync(userId));

            return documents.Select(document => MapWithReviewFlag(document, categories, itemsByDocument[document.Id], history)).ToList();
        }

        public async Task<DocumentResult> LinkTransactionAsync(long id, long transactionId)
        {
            Domain.Entities.Document document;
            Domain.Entities.Document updated;
            Transaction transaction;

            Domain.Validate.Id(id, nameof(id));
            Domain.Validate.Id(transactionId, nameof(transactionId));

            document = await GetOwnedAsync(id);
            transaction = await transactionsRepository.GetByIdAsync(transactionId);
            if (transaction is null || transaction.DeletedAt is not null)
                throw new DomainValidationException("Transação não encontrada.");

            if (document.TransactionId == transactionId)
                throw new DomainValidationException("Documento já vinculado a esta transação.");
            if (document.UserId != transaction.UserId)
                throw new DomainValidationException("Documento e transação pertencem a diferentes usuários.");

            LinkTransactionDto dto = new()
            {
                Id = id,
                TransactionId = transactionId
            };

            updated = await documentsRepository.UpdateTransactionAsync(dto);
            if (updated == null)
                throw new DomainValidationException("Erro ao vincular documento à transação.");

            return await MapWithReviewFlagAsync(updated);
        }

        public async Task<DocumentResult> UpdateStatusAsync(long id, StatusOCRTypes status)
        {
            Domain.Entities.Document document;
            Domain.Entities.Document updated;

            Domain.Validate.Id(id, nameof(id));
            Domain.Validate.Enum(status, nameof(status));

            document = await GetOwnedAsync(id);

            UpdateStatusDto dto = new()
            {
                Id = id,
                Status = status
            };

            updated = await documentsRepository.UpdateStatusAsync(dto);
            if (updated == null)
                throw new DomainValidationException("Erro ao atualizar status do documento.");

            return await MapWithReviewFlagAsync(updated);
        }

        public async Task<DocumentResult> ConfirmManuallyAsync(long id, bool confirmedManually)
        {
            Domain.Entities.Document document;
            Domain.Entities.Document updated;

            Domain.Validate.Id(id, nameof(id));
            Domain.Validate.Bool(confirmedManually, nameof(confirmedManually), expected: true);

            document = await GetOwnedAsync(id);

            ConfirmManuallyDto dto = new()
            {
                Id = id,
                ConfirmedManually = confirmedManually
            };

            updated = await documentsRepository.UpdateConfirmedManuallyAsync(dto);
            if (updated == null)
                throw new DomainValidationException("Erro ao atualizar confirmação manual do documento.");

            return await MapWithReviewFlagAsync(updated);
        }

        public async Task<DocumentResult> DeleteAsync(long id)
        {
            DocumentResult document = await GetByIdAsync(id);

            if (!await documentsRepository.DeleteAsync(id))
                throw new NotFoundException("Documento não encontrado.");

            return document;
        }

        private async Task<Domain.Entities.Document> GetOwnedAsync(long id)
        {
            var document = await documentsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Documento não encontrado.");
            currentUser.EnsureCanAccess(document.UserId);

            return document;
        }

        private async Task<DocumentResult> MapWithReviewFlagAsync(Domain.Entities.Document document)
        {
            IEnumerable<Category> categories;
            IEnumerable<DocumentItem> items;
            CategoryHistory history;

            document = await documentsRepository.GetByIdAsync(document.Id);
            if (document == null)
                throw new DomainValidationException("Documento não encontrado.");

            categories = await categoriesRepository.GetAllByUserIdAsync(document.UserId);
            items = await documentItemsRepository.GetByDocumentIdAsync(document.Id);
            history = new CategoryHistory(await documentConfirmationsRepository.GetCategoryHistoryByUserAsync(document.UserId));
            return MapWithReviewFlag(document, categories, items, history);
        }

        private DocumentResult MapWithReviewFlag(Domain.Entities.Document document,
            IEnumerable<Category> categories,
            IEnumerable<DocumentItem> items,
            CategoryHistory history)
        {
            DocumentResult result = document.Adapt<DocumentResult>();
            result.Items = items.Adapt<List<DocumentItemResult>>();

            if (!string.IsNullOrWhiteSpace(document.TextOCR))
            {
                result.Suggestion = documentParsingService.Parse(document.TextOCR, categories.Adapt<List<CategoryResult>>(), history,
                    document.EstablishmentCnae, document.EstablishmentRegistryName);
                result.Suggestion.Items = [];
            }

            result.ReviewReasons = DocumentReviewPolicy
                .Evaluate(document.Status, document.TextOCR, document.TrustOCR, ocrOptions.ConfidenceThreshold, result.Suggestion,
                    document.ValueTrustOCR, ocrOptions.ValueConfidenceThreshold)
                .ToList();
            result.NeedsManualReview = !document.ConfirmedManually && result.ReviewReasons.Count > 0;

            return result;
        }
    }
}