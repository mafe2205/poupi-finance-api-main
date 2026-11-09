using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankAccounts.Services;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos;
using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Cards.Entities.Results;
using AppFinancesClassLibrary.Application.Cards.Services;
using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Categories.Services;
using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Application.Goals.Entities.Results;
using AppFinancesClassLibrary.Application.Goals.Services;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Results;
using AppFinancesClassLibrary.Application.User.Entities.Results;
using AppFinancesClassLibrary.Application.User.Services;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace AppFinancesClassLibrary.Application.Transactions.Services
{
    public class TransactionsService(
        IUnitOfWork unitOfWork,
        ITransactionsRepository transactionsRepository,
        ICategoriesRepository categoriesRepository,
        ICategoriesService categoriesService,
        IGoalsService goalsService,
        ICardsRepository cardsRepository,
        ICardsService cardsService,
        IBankAccountsRepository bankAccountsRepository,
        IBankAccountsService bankAccountsService,
        IBankStatementsRepository bankStatementsRepository,
        IDocumentsRepository documentsRepository,
        IDocumentConfirmationsRepository documentConfirmationsRepository,
        IDocumentsService documentsService,
        IDocumentParsingService documentParsingService,
        IUserService userService,
        ICurrentUser currentUser,
        IBackgroundTaskQueue backgroundTaskQueue) : ITransactionsService
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;
        private readonly ITransactionsRepository transactionsRepository = transactionsRepository;
        private readonly ICategoriesRepository categoriesRepository = categoriesRepository;
        private readonly ICategoriesService categoriesService = categoriesService;
        private readonly IGoalsService goalsService = goalsService;
        private readonly ICardsRepository cardsRepository = cardsRepository;
        private readonly ICardsService cardsService = cardsService;
        private readonly IBankAccountsRepository bankAccountsRepository = bankAccountsRepository;
        private readonly IBankAccountsService bankAccountsService = bankAccountsService;
        private readonly IBankStatementsRepository bankStatementsRepository = bankStatementsRepository;
        private readonly IDocumentsRepository documentsRepository = documentsRepository;
        private readonly IDocumentConfirmationsRepository documentConfirmationsRepository = documentConfirmationsRepository;
        private readonly IDocumentsService documentsService = documentsService;
        private readonly IDocumentParsingService documentParsingService = documentParsingService;
        private readonly IUserService userService = userService;

        public Task<Transaction> GetByIdAsync(long id)
            => GetByIdCoreAsync(currentUser.UserId, id);

        public Task<IEnumerable<Transaction>> GetByUserAsync(TransactionFilterDto filter)
            => GetByUserCoreAsync(currentUser.UserId, filter);

        public Task<Transaction> CreateAsync(CreateTransactionDto dto)
            => ChangeDataAsync(async userId => await GetByIdCoreAsync(userId, await CreateCoreAsync(userId, dto)));

        public Task<Transaction> CreateFromDocumentAsync(long documentId, ConfirmDocumentDto dto)
            => ChangeDataAsync(async userId => await GetByIdCoreAsync(userId, await CreateFromDocumentCoreAsync(userId, documentId, dto)));

        public Task<Transaction> UpdateAsync(long id, UpdateTransactionDto dto)
            => ChangeDataAsync(async userId =>
            {
                await UpdateCoreAsync(userId, id, dto);
                return await GetByIdCoreAsync(userId, id);
            });

        public Task<Transaction> DeleteAsync(long id)
            => ChangeDataAsync(userId => DeleteCoreAsync(userId, id));

        public Task<ReclassificationResult> ReclassifyAsync()
            => ChangeDataAsync(ReclassifyCoreAsync);

        public Task CancelSeriesAsync(long transactionId)
            => ChangeDataAsync(async userId =>
            {
                await CancelSeriesCoreAsync(userId, transactionId);
                return true;
            });
        private async Task<T> ChangeDataAsync<T>(Func<long, Task<T>> action)
        {
            long userId = currentUser.UserId;
            T result;

            unitOfWork.Begin();
            try
            {
                result = await action(userId);
                await unitOfWork.CommitAsync();
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }

            backgroundTaskQueue.Enqueue((scopedProvider, ct) =>
                scopedProvider.GetRequiredService<IInsightRefreshService>()
                    .RefreshAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), ct));

            return result;
        }

        private async Task<long?> ResolveBankAccountIdForBalanceAsync(Transaction transaction)
        {
            if (transaction.PaymentMethod == PaymentMethod.cartao_credito)
                return null;

            if (transaction.BankAccountId is not null)
                return transaction.BankAccountId;

            if (transaction.CardId is not null)
            {
                Card card = await cardsRepository.GetByIdIncludingInactiveAsync(transaction.CardId.Value) ?? throw new DomainValidationException("Cartão não encontrado.");
                return card?.BankAccountId;
            }

            return null;
        }

        private static void ValidateMetaLinkage(CategoryResult category, long? metaId, GoalContributionDirection? metaDirection)
        {
            if (category.IsDefault && metaId is null)
                throw new DomainValidationException("MetaId é obrigatório quando CategoryId aponta para a categoria padrão Metas.");

            if (!category.IsDefault && metaId is not null)
                throw new DomainValidationException("MetaId só pode ser preenchido quando CategoryId aponta para a categoria padrão Metas.");

            if (metaId is not null && metaDirection is null)
                throw new DomainValidationException("MetaDirection é obrigatório quando MetaId está preenchido.");

            if (metaId is null && metaDirection is not null)
                throw new DomainValidationException("MetaDirection só pode ser preenchido quando MetaId também está.");
        }

        private static void ValidateConfirmedItems(List<ConfirmDocumentItemDto> items, DocumentResult document)
        {
            var suggestedItemIds = document.Items.Select(item => item.Id).ToHashSet();
            var usedSuggestedItemIds = new HashSet<long>();

            foreach (var item in items)
            {
                Domain.Validate.NotNull(item, nameof(ConfirmDocumentDto.Items));
                Domain.Validate.String(item.Description, nameof(item.Description));
                Domain.Validate.Decimal(item.Quantity, nameof(item.Quantity), min: 0.001m);
                Domain.Validate.Int(item.UnitPriceCents, nameof(item.UnitPriceCents), min: 0);
                Domain.Validate.Int(item.TotalCents, nameof(item.TotalCents), min: 0);

                if (item.SuggestedItemId is null)
                    continue;

                if (!suggestedItemIds.Contains(item.SuggestedItemId.Value))
                    throw new DomainValidationException("SuggestedItemId não pertence aos itens lidos deste documento.");

                if (!usedSuggestedItemIds.Add(item.SuggestedItemId.Value))
                    throw new DomainValidationException("SuggestedItemId repetido nos itens confirmados.");
            }
        }

        private static int ComputeSignedDelta(CategoriesTypes categoryType, GoalContributionDirection? metaDirection, int amountCents)
            => BalanceDelta.Compute(categoryType, metaDirection, amountCents);


        private async Task<Transaction> GetByIdCoreAsync(long userId, long id)
        {
            UserResult userFound;
            Transaction transaction;

            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.Id(id, nameof(id));

            userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            transaction = await transactionsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Transação não encontrada.");

            if (transaction.UserId != userId || transaction.DeletedAt is not null)
                throw new DomainValidationException("Transação não encontrada.");

            return transaction;
        }

        private async Task<IEnumerable<Transaction>> GetByUserCoreAsync(long userId, TransactionFilterDto filter)
        {
            UserResult userFound;
            IEnumerable<Transaction> transactions;

            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.NotNull(filter, nameof(filter));

            userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            transactions =  await transactionsRepository.GetByUserAsync(userId, filter) ?? [];
            return transactions;
        }


        private async Task<long> CreateCoreAsync(long userId, CreateTransactionDto dto)
        {
            UserResult userFound;
            CategoryResult category;
            GoalResult goals;
            CardResult card = null;
            BankAccountResult bankAccount = null;
            BankStatement statement = null;
            DateOnly? expiringDate = dto.ExpiringDate;
            CreateTransactionInternalDto internalDto;
            Transaction transaction;

            Domain.Validate.NotNull(dto, nameof(dto));
            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.Id(dto.CategoryId, nameof(dto.CategoryId));
            Domain.Validate.Int(dto.PaymentValueCents, nameof(dto.PaymentValueCents), min: 1);

            userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            category = await categoriesService.GetByIdAsync(dto.CategoryId)
                ?? throw new DomainValidationException("Categoria não encontrada.");

            ValidateMetaLinkage(category, dto.MetaId, dto.MetaDirection);

            if (dto.MetaId is not null)
            {
                Domain.Validate.Id(dto.MetaId.Value, nameof(dto.MetaId));
                goals = await goalsService.GetByIdAsync(dto.MetaId.Value)
                    ?? throw new DomainValidationException("Meta não encontrada.");
            }

            var involvesCard = dto.PaymentMethod is PaymentMethod.cartao_credito or PaymentMethod.cartao_debito;

            if (involvesCard)
            {
                if (dto.CardId is null)
                    throw new DomainValidationException("CardId é obrigatório quando a forma de pagamento envolve cartão.");

                Domain.Validate.Id(dto.CardId.Value, nameof(dto.CardId));
                card = await cardsService.GetByIdAsync(dto.CardId.Value)
                    ?? throw new DomainValidationException("Cartão não encontrado.");

                if (dto.PaymentMethod == PaymentMethod.cartao_debito)
                {
                    bankAccount = await bankAccountsService.GetByIdAsync(card.BankAccountId)
                        ?? throw new DomainValidationException("Conta bancária do cartão não encontrada.");
                }
            }
            else
            {
                if (dto.BankAccountId is null)
                    throw new DomainValidationException("BankAccountId é obrigatório quando a forma de pagamento é dinheiro, pix ou boleto.");

                Domain.Validate.Id(dto.BankAccountId.Value, nameof(dto.BankAccountId));
                bankAccount = await bankAccountsService.GetByIdAsync(dto.BankAccountId.Value)
                    ?? throw new DomainValidationException("Conta bancária não encontrada.");
            }

            var parcels = dto.PaymentMethod == PaymentMethod.cartao_credito ? Math.Max(dto.PaymentParcels, 1) : 1;

            if (dto.Recorrent)
                return await CreateRecurringSeriesAsync(dto, userId, card, bankAccount, category);

            if (parcels > 1)
                return await CreateInstallmentPlanAsync(dto, userId, card!, parcels);

            if (dto.PaymentMethod == PaymentMethod.cartao_credito)
            {
                statement = await GetOrCreateBankStatementAsync(card!, dto.TransactionDate);
                expiringDate = statement.DueDate;
            }

            internalDto = new()
            {
                UserId = userId,
                CategoryId = dto.CategoryId,
                MetaId = dto.MetaId,
                MetaDirection = dto.MetaDirection,
                CardId = involvesCard ? card!.Id : null,
                BankAccountId = involvesCard ? null : bankAccount?.Id,
                BankStatementId = statement?.Id,
                Description = dto.Description,
                PaymentMethod = dto.PaymentMethod,
                PaymentParcels = parcels,
                PaymentValueCents = dto.PaymentValueCents,
                Recorrent = false,
                TransactionDate = dto.TransactionDate,
                PaymentDate = dto.PaymentDate,
                ExpiringDate = expiringDate,
                Source = TransactionSource.manual,
            };

            unitOfWork.Begin();
            try
            {
                transaction = await transactionsRepository.CreateAsync(internalDto);

                if (dto.PaymentMethod != PaymentMethod.cartao_credito && bankAccount is not null)
                {
                    var delta = ComputeSignedDelta(category.Type, dto.MetaDirection, dto.PaymentValueCents);
                    await bankAccountsRepository.AdjustBalanceAsync(bankAccount.Id, delta);
                }

                await unitOfWork.CommitAsync();
                return transaction.Id;
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task<long> CreateFromDocumentCoreAsync(long userId, long documentId, ConfirmDocumentDto dto)
        {
            DocumentResult document;
            CategoryResult category;
            GoalResult goal;
            CardResult card = null;
            BankAccountResult bankAccount = null;
            BankStatement statement = null;
            DateOnly? expiringDate = dto.ExpiringDate;
            CreateTransactionInternalDto internalDto;
            Transaction transaction;
            LinkTransactionDto linkTransactionDto;
            UpdateStatusDto updateStatusDto;

            Domain.Validate.NotNull(dto, nameof(dto));
            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.Id(documentId, nameof(documentId));
            Domain.Validate.Id(dto.CategoryId, nameof(dto.CategoryId));
            Domain.Validate.Int(dto.PaymentValueCents, nameof(dto.PaymentValueCents), min: 1);

            document = await documentsService.GetByIdAsync(documentId)
                ?? throw new DomainValidationException("Documento não encontrado.");

            if (document.UserId != userId)
                throw new DomainValidationException("Documento não encontrado.");

            if (document.TransactionId is not null)
                throw new DomainValidationException("Documento já está vinculado a uma transação.");

            category = await categoriesService.GetByIdAsync(dto.CategoryId)
                ?? throw new DomainValidationException("Categoria não encontrada.");

            ValidateMetaLinkage(category, dto.MetaId, dto.MetaDirection);

            if (dto.Items is not null)
                ValidateConfirmedItems(dto.Items, document);

            if (dto.MetaId is not null)
            {
                Domain.Validate.Id(dto.MetaId.Value, nameof(dto.MetaId));
                goal = await goalsService.GetByIdAsync(dto.MetaId.Value)
                    ?? throw new DomainValidationException("Meta não encontrada.");
            }

            var involvesCard = dto.PaymentMethod is PaymentMethod.cartao_credito or PaymentMethod.cartao_debito;

            if (involvesCard)
            {
                if (dto.CardId is null)
                    throw new DomainValidationException("CardId é obrigatório quando a forma de pagamento envolve cartão.");

                Domain.Validate.Id(dto.CardId.Value, nameof(dto.CardId));
                card = await cardsService.GetByIdAsync(dto.CardId.Value)
                    ?? throw new DomainValidationException("Cartão não encontrado.");

                if (dto.PaymentMethod == PaymentMethod.cartao_debito)
                {
                    bankAccount = await bankAccountsService.GetByIdAsync(card.BankAccountId)
                        ?? throw new DomainValidationException("Conta bancária do cartão não encontrada.");
                }
            }
            else
            {
                if (dto.BankAccountId is null)
                    throw new DomainValidationException("BankAccountId é obrigatório quando a forma de pagamento é dinheiro, pix ou boleto.");

                Domain.Validate.Id(dto.BankAccountId.Value, nameof(dto.BankAccountId));
                bankAccount = await bankAccountsService.GetByIdAsync(dto.BankAccountId.Value)
                    ?? throw new DomainValidationException("Conta bancária não encontrada.");
            }

            if (dto.PaymentMethod == PaymentMethod.cartao_credito)
            {
                statement = await GetOrCreateBankStatementAsync(card!, dto.TransactionDate);
                expiringDate = statement.DueDate;
            }

            var history = new CategoryHistory(await documentConfirmationsRepository.GetCategoryHistoryByUserAsync(userId));
            var ocrProposal = documentParsingService.Parse(document.TextOCR, await categoriesService.GetAllByUserIdAsync(userId), history,
                document.EstablishmentCnae, document.EstablishmentRegistryName);

            internalDto = new()
            {
                UserId = userId,
                CategoryId = dto.CategoryId,
                MetaId = dto.MetaId,
                MetaDirection = dto.MetaDirection,
                CardId = involvesCard ? card!.Id : null,
                BankAccountId = involvesCard ? null : bankAccount?.Id,
                BankStatementId = statement?.Id,
                Description = dto.Description,
                PaymentMethod = dto.PaymentMethod,
                PaymentParcels = 1,
                PaymentValueCents = dto.PaymentValueCents,
                PaymentValueOCR = ocrProposal.PaymentValueCents,
                DescriptionOCR = ocrProposal.Description,
                Recorrent = false,
                TransactionDate = dto.TransactionDate,
                ExpiringDate = expiringDate,
                Source = TransactionSource.ocr,
                CategoryConfirmedManually = ocrProposal.CategoryRequiresConfirmation || ocrProposal.CategoryId != dto.CategoryId,
            };

            unitOfWork.Begin();
            try
            {
                transaction = await transactionsRepository.CreateAsync(internalDto);

                if (dto.PaymentMethod != PaymentMethod.cartao_credito && bankAccount is not null)
                {
                    var delta = ComputeSignedDelta(category.Type, dto.MetaDirection, dto.PaymentValueCents);
                    await bankAccountsRepository.AdjustBalanceAsync(bankAccount.Id, delta);
                }

                linkTransactionDto = new()
                {
                    Id = documentId,
                    TransactionId = transaction.Id,
                };

                if (!await documentsRepository.TryLinkTransactionAsync(linkTransactionDto))
                    throw new DomainValidationException("Documento já está vinculado a uma transação.");

                CreateDocumentConfirmationDto confirmationDto = new()
                {
                    DocumentId = documentId,
                    TransactionId = transaction.Id,
                    SuggestedDescription = ocrProposal.Description,
                    ConfirmedDescription = dto.Description,
                    SuggestedPaymentValueCents = ocrProposal.PaymentValueCents,
                    ConfirmedPaymentValueCents = dto.PaymentValueCents,
                    SuggestedTransactionDate = ocrProposal.TransactionDate,
                    ConfirmedTransactionDate = dto.TransactionDate,
                    SuggestedCategoryId = ocrProposal.CategoryId,
                    SuggestedCategoryReason = ocrProposal.CategoryReason,
                    ConfirmedCategoryId = dto.CategoryId,
                    SuggestedItemsTotalCents = ocrProposal.ItemsTotalCents,
                    ItemsConfirmed = dto.Items is not null,
                    EstablishmentKey = ocrProposal.EstablishmentKey,
                    SuggestedDescriptionSource = ocrProposal.DescriptionSource
                };
                await documentConfirmationsRepository.CreateAsync(confirmationDto);

                if (dto.Items is not null)
                    await documentConfirmationsRepository.CreateItemsAsync(documentId, dto.Items);

                updateStatusDto = new()
                {
                    Id = documentId,
                    Status = StatusOCRTypes.processado,
                };

                await documentsRepository.UpdateStatusAsync(updateStatusDto);

                await unitOfWork.CommitAsync();
                return transaction.Id;
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task<long> CreateInstallmentPlanAsync(CreateTransactionDto dto, long userId, CardResult card, int parcels)
        {
            var baseValue = dto.PaymentValueCents / parcels;
            var remainder = dto.PaymentValueCents - baseValue * parcels;
            var serialId = await transactionsRepository.NextRecorrentSerialIdAsync();
            var firstId = 0L;

            unitOfWork.Begin();
            try
            {
                for (var i = 0; i < parcels; i++)
                {
                    CreateTransactionInternalDto internalDto;
                    Transaction created;

                    var installmentDate = dto.TransactionDate.AddMonths(i);
                    var statement = await GetOrCreateBankStatementAsync(card, installmentDate);
                    var value = baseValue + (i == parcels - 1 ? remainder : 0);

                    internalDto = new() 
                    {
                        UserId = userId,
                        CategoryId = dto.CategoryId,
                        MetaId = dto.MetaId,
                        MetaDirection = dto.MetaDirection,
                        CardId = card.Id,
                        BankAccountId = null,
                        BankStatementId = statement.Id,
                        Description = $"{dto.Description} ({i + 1}/{parcels})",
                        PaymentMethod = dto.PaymentMethod,
                        PaymentParcels = parcels,
                        PaymentValueCents = value,
                        Recorrent = false,
                        RecorrentSerialId = serialId,
                        TransactionDate = dto.TransactionDate,
                        ExpiringDate = statement.DueDate,
                        Source = TransactionSource.manual,
                    };

                    created = await transactionsRepository.CreateAsync(internalDto);
                    if (i == 0) firstId = created.Id;
                }

                await unitOfWork.CommitAsync();
                return firstId;
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task<long> CreateRecurringSeriesAsync(
            CreateTransactionDto dto, long userId, CardResult card, BankAccountResult bankAccount, CategoryResult category)
        {
            var months = dto.RecorrentValue.GetValueOrDefault(1);
            if (months < 1) months = 1;

            var involvesCard = card is not null;
            var serialId = await transactionsRepository.NextRecorrentSerialIdAsync();
            var firstId = 0L;

            unitOfWork.Begin();
            try
            {
                for (var i = 0; i < months; i++)
                {
                    var occurrenceDate = dto.TransactionDate.AddMonths(i);
                    BankStatement statement = null;
                    DateOnly? expiringDate = dto.ExpiringDate;

                    if (dto.PaymentMethod == PaymentMethod.cartao_credito)
                    {
                        statement = await GetOrCreateBankStatementAsync(card!, occurrenceDate);
                        expiringDate = statement.DueDate;
                    }

                    var internalDto = new CreateTransactionInternalDto
                    {
                        UserId = userId,
                        CategoryId = dto.CategoryId,
                        MetaId = dto.MetaId,
                        MetaDirection = dto.MetaDirection,
                        CardId = involvesCard ? card!.Id : null,
                        BankAccountId = involvesCard ? null : bankAccount?.Id,
                        BankStatementId = statement?.Id,
                        Description = dto.Description,
                        PaymentMethod = dto.PaymentMethod,
                        PaymentParcels = 1,
                        PaymentValueCents = dto.PaymentValueCents,
                        Recorrent = true,
                        RecorrentValue = months,
                        RecorrentSerialId = serialId,
                        TransactionDate = occurrenceDate,
                        ExpiringDate = expiringDate,
                        Source = TransactionSource.manual,
                    };

                    var created = await transactionsRepository.CreateAsync(internalDto);
                    if (i == 0) firstId = created.Id;

                    if (dto.PaymentMethod != PaymentMethod.cartao_credito)
                    {
                        var accountId = await ResolveBankAccountIdForBalanceAsync(created);
                        if (accountId is not null)
                        {
                            var delta = ComputeSignedDelta(category.Type, dto.MetaDirection, dto.PaymentValueCents);
                            await bankAccountsRepository.AdjustBalanceAsync(accountId.Value, delta);
                        }
                    }
                }

                await unitOfWork.CommitAsync();
                return firstId;
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task<BankStatement> GetOrCreateBankStatementAsync(CardResult card, DateOnly referenceDate)
        {
            var (monthReference, closingDate, dueDate) = ResolveBillingCycle(card, referenceDate);

            var existing = await bankStatementsRepository.GetByCardIdAndMonthReferenceAsync(card.Id, monthReference);
            if (existing is not null) return existing;

            return await bankStatementsRepository.CreateAsync(new CreateBankStatementInternalDto
            {
                CardId = card.Id,
                MonthReference = monthReference,
                ClosingDate = closingDate,
                DueDate = dueDate,
                Status = BankStatementsStatus.aberta
            });
        }

        private static (string MonthReference, DateOnly ClosingDate, DateOnly DueDate) ResolveBillingCycle(CardResult card, DateOnly referenceDate)
        {
            var cycleMonth = referenceDate.Day <= card.ClosingDate
                ? new DateOnly(referenceDate.Year, referenceDate.Month, 1)
                : new DateOnly(referenceDate.Year, referenceDate.Month, 1).AddMonths(1);

            var closingDate = ClampToMonth(cycleMonth, card.ClosingDate);

            var dueMonth = card.DueDate <= card.ClosingDate ? cycleMonth.AddMonths(1) : cycleMonth;
            var dueDate = ClampToMonth(dueMonth, card.DueDate);

            var monthReference = $"{cycleMonth.Year:D4}-{cycleMonth.Month:D2}";
            return (monthReference, closingDate, dueDate);
        }

        private static DateOnly ClampToMonth(DateOnly month, int day)
        {
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            return new DateOnly(month.Year, month.Month, Math.Min(Math.Max(day, 1), daysInMonth));
        }

        private async Task UpdateCoreAsync(long userId, long id, UpdateTransactionDto dto)
        {
            Transaction transaction;
            Category categoriesEntity;
            CategoryResult categoryResult;
            UpdateTransactionInternalDto internalDto;
            Category oldCategory;

            Domain.Validate.NotNull(dto, nameof(dto));
            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.Id(id, nameof(id));
            Domain.Validate.Int(dto.PaymentValueCents, nameof(dto.PaymentValueCents), min: 1);

            transaction = await transactionsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Transação não encontrada.");

            if (transaction.UserId != userId || transaction.DeletedAt is not null)
                throw new DomainValidationException("Transação não encontrada.");

            categoriesEntity = (dto.CategoryId == transaction.CategoryId
                    ? await categoriesRepository.GetByIdIncludingInactiveAsync(dto.CategoryId)
                    : await categoriesRepository.GetByIdAsync(dto.CategoryId))
                ?? throw new DomainValidationException("Categoria não encontrada.");

            categoryResult = categoriesEntity.Adapt<CategoryResult>();

            ValidateMetaLinkage(categoryResult, dto.MetaId, dto.MetaDirection);

            internalDto = new()
            {
                Id = id,
                CategoryId = dto.CategoryId,
                MetaId = dto.MetaId,
                MetaDirection = dto.MetaDirection,
                Description = dto.Description,
                PaymentValueCents = dto.PaymentValueCents,
                TransactionDate = dto.TransactionDate,
                PaymentDate = dto.PaymentDate,
                CategoryConfirmedManually = transaction.CategoryConfirmedManually || dto.CategoryId != transaction.CategoryId,
            };

            unitOfWork.Begin();
            try
            {
                await transactionsRepository.UpdateAsync(internalDto);

                var accountId = await ResolveBankAccountIdForBalanceAsync(transaction);
                if (accountId is not null)
                {
                    oldCategory = await categoriesRepository.GetByIdIncludingInactiveAsync(transaction.CategoryId)
                        ?? throw new DomainValidationException("Categoria original não encontrada.");

                    var oldDelta = ComputeSignedDelta(oldCategory.Type, transaction.MetaDirection, transaction.PaymentValueCents);
                    var newDelta = ComputeSignedDelta(categoryResult.Type, dto.MetaDirection, dto.PaymentValueCents);
                    await bankAccountsRepository.AdjustBalanceAsync(accountId.Value, newDelta - oldDelta);
                }

                await unitOfWork.CommitAsync();
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task<ReclassificationResult> ReclassifyCoreAsync(long userId)
        {
            UserResult userFound;
            IReadOnlyList<CategoryResult> categories;
            IReadOnlyList<Transaction> candidates;
            ReclassificationResult result;

            Domain.Validate.Id(userId, nameof(userId));

            userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            categories = (await categoriesService.GetAllByUserIdAsync(userId)).ToList();
            candidates = (await transactionsRepository.GetAutoClassifiedByUserAsync(userId)).ToList();
            var history = new CategoryHistory(await documentConfirmationsRepository.GetCategoryHistoryByUserAsync(userId));
            result = new()
            { 
                Evaluated = candidates.Count
            };

            foreach (var transaction in candidates)
            {
                if (TransactionReclassifier.NewCategoryFor(transaction, categories, history) is not { } newCategoryId) continue;

                if (await transactionsRepository.UpdateAutoCategoryAsync(transaction.Id, newCategoryId))
                    result.Changed.Add(new ReclassifiedTransaction(transaction.Id, transaction.CategoryId, newCategoryId));
            }

            return result;
        }

        private async Task<Transaction> DeleteCoreAsync(long userId, long id)
        {
            Transaction transaction;
            SoftDeleteDto softDeleteDto;
            Category category;

            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.Id(id, nameof(id));

            transaction = await transactionsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Transação não encontrada.");

            if (transaction.UserId != userId || transaction.DeletedAt is not null)
                throw new DomainValidationException("Transação não encontrada.");

            try
            {
                softDeleteDto = new()
                {
                    Id = id,
                };
                DateTime deletedAt = await transactionsRepository.SoftDeleteAsync(softDeleteDto);

                var accountId = await ResolveBankAccountIdForBalanceAsync(transaction);
                if (accountId is not null)
                {
                    category = await categoriesRepository.GetByIdIncludingInactiveAsync(transaction.CategoryId)
                        ?? throw new DomainValidationException("Categoria não encontrada.");

                    var reversedDelta = -ComputeSignedDelta(category.Type, transaction.MetaDirection, transaction.PaymentValueCents);
                    await bankAccountsRepository.AdjustBalanceAsync(accountId.Value, reversedDelta);
                }

                transaction.Active = false;
                transaction.DeletedAt = deletedAt;
                transaction.UpdatedAt = deletedAt;
                return transaction;
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task CancelSeriesCoreAsync(long userId, long transactionId)
        {
            Transaction anchor;
            IEnumerable<Transaction> series;
            List<Transaction> toCancel;
            SoftDeleteDto softDeleteDto;
            Category category;

            Domain.Validate.Id(userId, nameof(userId));
            Domain.Validate.Id(transactionId, nameof(transactionId));

            anchor = await transactionsRepository.GetByIdAsync(transactionId)
                ?? throw new DomainValidationException("Transação não encontrada.");

            if (anchor.UserId != userId || anchor.DeletedAt is not null)
                throw new DomainValidationException("Transação não encontrada.");

            if (anchor.RecorrentSerialId is null)
                throw new DomainValidationException("Transação não pertence a uma série recorrente ou parcelada.");

            series = await transactionsRepository.GetBySerialIdAsync(anchor.RecorrentSerialId.Value);
            toCancel = series.Where(t => t.UserId == userId && t.DeletedAt is null).ToList();

            unitOfWork.Begin();
            try
            {
                foreach (var transaction in toCancel)
                {
                    softDeleteDto = new()
                    {
                        Id = transaction.Id,
                    };

                    await transactionsRepository.SoftDeleteAsync(softDeleteDto);

                    var accountId = await ResolveBankAccountIdForBalanceAsync(transaction);
                    if (accountId is not null)
                    {
                        category = await categoriesRepository.GetByIdIncludingInactiveAsync(transaction.CategoryId)
                            ?? throw new DomainValidationException("Categoria não encontrada.");

                        var reversedDelta = -ComputeSignedDelta(category.Type, transaction.MetaDirection, transaction.PaymentValueCents);
                        await bankAccountsRepository.AdjustBalanceAsync(accountId.Value, reversedDelta);
                    }
                }

                await unitOfWork.CommitAsync();
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}