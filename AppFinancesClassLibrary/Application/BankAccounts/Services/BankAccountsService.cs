using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.BankAccounts.Services
{
    public class BankAccountsService(IBankAccountsRepository bankAccountsRepository, IUserRepository userRepository, ICurrentUser currentUser) : IBankAccountsService
    {
        public async Task<BankAccountResult> CreateAsync(CreateBankAccountDto dto)
        {
            Domain.Validate.NotNull(dto, nameof(CreateBankAccountDto));

            dto.UserId = currentUser.ResolveUserId(dto.UserId);

            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.String(dto.BankName, nameof(dto.BankName), minLength: 2, maxLength: 100);
            Domain.Validate.String(dto.Agency, nameof(dto.Agency), minLength: 1, maxLength: 20);
            Domain.Validate.String(dto.AccountNumber, nameof(dto.AccountNumber), minLength: 1, maxLength: 30);
            Domain.Validate.Int(dto.BalanceCents, nameof(dto.BalanceCents), min: 0);

            Domain.Entities.User userFound = await userRepository.GetByIdAsync(dto.UserId) ?? throw new DomainValidationException("Usuário não encontrado.");

            var created = await bankAccountsRepository.CreateAsync(dto);
            return created.Adapt<BankAccountResult>();
        }

        public async Task<BankAccountResult> UpdateAsync(UpdateBankAccountDto dto)
        {
            BankAccount updated;

            Domain.Validate.NotNull(dto, nameof(UpdateBankAccountDto));
            Domain.Validate.Id(dto.Id, nameof(dto.Id));

            await GetActiveOwnedAsync(dto.Id);

            updated = await bankAccountsRepository.UpdateAsync(dto);
            if (updated is null) throw new DomainValidationException("Erro na atualização da conta bancária.");

            return updated.Adapt<BankAccountResult>();
        }

        public async Task<BankAccountResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            var bankAccount = await GetActiveOwnedAsync(id);
            return bankAccount.Adapt<BankAccountResult>();
        }

        public async Task<IEnumerable<BankAccountResult>> GetAllByUserIdAsync(long userId)
        {
            Domain.Entities.User userFound;
            IEnumerable<BankAccount> bankAccounts;

            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);
            userFound = await userRepository.GetByIdAsync(userId) ?? throw new DomainValidationException("Usuário não encontrado.");

            bankAccounts = await bankAccountsRepository.GetAllByUserIdAsync(userId);
            if (bankAccounts is null) throw new NotFoundException("Contas bancárias não encontradas.");

            return bankAccounts.Adapt<IEnumerable<BankAccountResult>>();
        }

        public async Task<BankAccountResult> GetByIdIncludingInactiveAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            var bankAccount = await bankAccountsRepository.GetByIdIncludingInactiveAsync(id)
                ?? throw new NotFoundException("Conta bancária não encontrada.");
            currentUser.EnsureCanAccess(bankAccount.UserId);

            return bankAccount.Adapt<BankAccountResult>();
        }

        public async Task<IEnumerable<BankAccountResult>> GetAllInactiveByUserIdAsync(long userId)
        {
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);
            _ = await userRepository.GetByIdAsync(userId) ?? throw new DomainValidationException("Usuário não encontrado.");

            var bankAccounts = await bankAccountsRepository.GetAllInactiveByUserIdAsync(userId);
            return bankAccounts.Adapt<IEnumerable<BankAccountResult>>();
        }

        public async Task<BankAccountResult> ReactivateAsync(long id)
        {
            var existing = await GetByIdIncludingInactiveAsync(id);
            if (existing.Active) throw new DomainValidationException("A conta bancária já está ativa.");

            var reactivated = await bankAccountsRepository.ReactivateAsync(id)
                ?? throw new DomainValidationException("A conta bancária já está ativa.");

            return reactivated.Adapt<BankAccountResult>();
        }

        public async Task<BankAccountResult> DeleteAsync(long id)
        {
            BankAccount existing;
            BankAccount deactivated;

            Domain.Validate.Id(id, nameof(id));

            existing = await GetActiveOwnedAsync(id);

            if (existing.IsDefault)
                throw new DomainValidationException("A Carteira não pode ser excluída.");

            deactivated = await bankAccountsRepository.DeleteAsync(id)
                ?? throw new NotFoundException("Conta bancária não encontrada.");

            return deactivated.Adapt<BankAccountResult>();
        }

        public async Task<BankAccountResult> SetBalanceAsync(long id, int balanceCents)
        {
            BankAccount updated;

            Domain.Validate.Id(id, nameof(id));
            Domain.Validate.Int(balanceCents, nameof(balanceCents), min: 0);

            await GetActiveOwnedAsync(id);

            updated = await bankAccountsRepository.SetBalanceAsync(id, balanceCents);
            if (updated is null) throw new DomainValidationException("Erro na atualização do saldo da conta bancária.");

            return updated.Adapt<BankAccountResult>();
        }

        private async Task<BankAccount> GetActiveOwnedAsync(long id)
        {
            var bankAccount = await bankAccountsRepository.GetByIdAsync(id)
                ?? throw new NotFoundException("Conta bancária não encontrada.");
            currentUser.EnsureCanAccess(bankAccount.UserId);

            return bankAccount;
        }
    }
}
