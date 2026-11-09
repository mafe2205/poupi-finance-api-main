using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Application.Categories.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Login.Services;
using AppFinancesClassLibrary.Application.User.Entities.Dtos;
using AppFinancesClassLibrary.Application.User.Entities.Results;
using AppFinancesClassLibrary.Domain;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.User.Services
{
    public class UserService(
        IUserRepository userRepository,
        IAuthenticationRepository authenticationRepository,
        IBankAccountsRepository bankAccountsRepository,
        ICategoriesRepository categoriesRepository,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser) : IUserService
    {
        private readonly IUserRepository userRepository = userRepository;
        private readonly IAuthenticationRepository authenticationRepository = authenticationRepository;
        private readonly IBankAccountsRepository bankAccountsRepository = bankAccountsRepository;
        private readonly ICategoriesRepository categoriesRepository = categoriesRepository;
        private readonly IPasswordHasher passwordHasher = passwordHasher;

        public async Task<UserResult> CreateAsync(CreateUserDto dto)
        {
            Domain.Entities.User user;
            UserResult userResult;
            CreateAutheticationDto authenticationDto;
            Domain.Entities.Authentication existingAuthentication;
            CreateBankAccountDto walletDto;
            BankAccount existingBankAccount;
            CreateCategoryDto goalDto;
            Category existingCategories;

            Validate.NotNull(dto, nameof(CreateUserDto));

            Validate.String(dto.Name, nameof(dto.Name));
            Validate.String(dto.Surname, nameof(dto.Surname));
            Validate.DateOnly(dto.BirthDate, nameof(dto.BirthDate), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-120)), DateOnly.FromDateTime(DateTime.UtcNow));
            Validate.Email(dto.Email, nameof(dto.Email));
            Validate.Phone(dto.Phone, nameof(dto.Phone));
            Validate.String(dto.User, nameof(dto.User));
            Validate.String(dto.Password, nameof(dto.Password), 6);

            user = await userRepository.CreateAsync(dto)
                ?? throw new DomainValidationException("Erro ao criar usuário.");
            userResult = user.Adapt<UserResult>();

            authenticationDto = new()
            {
                UserId = userResult.Id,
                Username = dto.User,
                PasswordHash = passwordHasher.HashPassword(dto.Password)
            };
            existingAuthentication = await authenticationRepository.CreateAsync(authenticationDto) ?? throw new DomainValidationException("Erro ao criar autenticação do usuário.");

            walletDto = new()
            {
                UserId = userResult.Id,
                BankName = "Carteira",
                Agency = "-",
                AccountNumber = "-",
                IsDefault = true,
                BalanceCents = 0
            };
            existingBankAccount = await bankAccountsRepository.CreateAsync(walletDto) ?? throw new DomainValidationException("Erro ao criar conta bancária do usuário.");

            goalDto = new()
            {
                UserId = userResult.Id,
                Name = "Metas",
                Type = CategoriesTypes.Saida,
                IsDefault = true
            };
            existingCategories = await categoriesRepository.CreateAsync(goalDto) ?? throw new DomainValidationException("Erro ao criar categoria de metas do usuário.");

            foreach (var (name, type) in StarterCategories.All)
            {
                _ = await categoriesRepository.CreateAsync(new CreateCategoryDto { UserId = userResult.Id, Name = name, Type = type })
                    ?? throw new DomainValidationException($"Erro ao criar a categoria {name} do usuário.");
            }

            return userResult;
        }

        public async Task<UserResult> GetByIdAsync(long id)
        {
            Validate.Id(id, nameof(id));
            currentUser.EnsureCanAccess(id);

            Domain.Entities.User user = await userRepository.GetByIdAsync(id)
                ?? throw new UserNotFoundException();

            return user.Adapt<UserResult>();
        }

        public async Task<UserResult> UpdateAsync(long id, UpdateUserDto dto)
        {
            Domain.Entities.User user;
            Domain.Entities.User userFound;

            Validate.NotNull(dto, nameof(UpdateUserDto));
            Validate.Id(id, nameof(id));
            currentUser.EnsureCanAccess(id);

            if (dto.Name is not null)
                Validate.String(dto.Name, nameof(dto.Name));
            if (dto.Surname is not null)
                Validate.String(dto.Surname, nameof(dto.Surname));
            if (dto.BirthDate != default)
                Validate.DateOnly(dto.BirthDate, nameof(dto.BirthDate), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-120)), DateOnly.FromDateTime(DateTime.UtcNow));
            if (dto.Email is not null)
                Validate.Email(dto.Email, nameof(dto.Email));
            if (dto.Phone is not null)
                Validate.Phone(dto.Phone, nameof(dto.Phone));
            if (dto.User is not null)
                Validate.String(dto.User, nameof(dto.User));

            userFound = await userRepository.GetByIdAsync(id) ?? throw new UserNotFoundException();
            user = await userRepository.UpdateAsync(id, dto)
                ?? throw new UserNotFoundException();

            if (dto.User is not null)
                await authenticationRepository.UpdateUsernameByUserIdAsync(id, dto.User);

            return user.Adapt<UserResult>()
                ?? throw new DomainValidationException("Erro ao mapear entidade de usuário para resultado.");
        }

        public async Task<UserResult> DeleteAsync(long id)
        {
            UserResult userFound = await GetByIdAsync(id);

            await userRepository.DeleteAsync(id);
            return userFound;
        }
    }
}