using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Dtos;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.Categories.Services
{
    public class CategoriesService(ICategoriesRepository categoriesRepository, IUserRepository userRepository, ICurrentUser currentUser) : ICategoriesService
    {
        private readonly ICategoriesRepository categoriesRepository = categoriesRepository;
        private readonly IUserRepository userRepository = userRepository;

        public async Task<CategoryResult> CreateAsync(CreateCategoryDto dto)
        {
            Category category;

            Domain.Validate.NotNull(dto, nameof(CreateCategoryDto));
            dto.UserId = currentUser.ResolveUserId(dto.UserId);
            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.String(dto.Name, nameof(dto.Name), minLength: 2, maxLength: 100);
            Domain.Validate.Enum(dto.Type, nameof(dto.Type));

           Domain.Entities.User userResult = await userRepository.GetByIdAsync(dto.UserId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            if (dto.IsDefault)
                throw new DomainValidationException("A categoria padrão Metas é criada automaticamente no cadastro do usuário.");

            if (dto.LimitValueCents != 0)
                Domain.Validate.Int(dto.LimitValueCents, nameof(dto.LimitValueCents), min: 0);

            category = await categoriesRepository.CreateAsync(dto);
            if (category is null)
                throw new DomainValidationException("Erro ao criar a categoria.");

            return category.Adapt<CategoryResult>();
        }

        public async Task<CategoryResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            Category category = await GetOwnedAsync(id);
            return category.Adapt<CategoryResult>();
        }

        public async Task<IEnumerable<CategoryResult>> GetAllByUserIdAsync(long userId)
        {
            Domain.Entities.User userFound;
            IEnumerable<Category> categories;

            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);

            userFound = await userRepository.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            categories = await categoriesRepository.GetAllByUserIdAsync(userId) ?? [];
            return categories.Adapt<IEnumerable<CategoryResult>>();
        }

        public async Task<CategoryClassificationResult> SuggestAsync(string description)
        {
            Domain.Entities.User userFound;
            IEnumerable<Category> categories;
            IEnumerable<CategoryResult> categoriesResult;

            long userId = currentUser.UserId;

            userFound = await userRepository.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            categories = await categoriesRepository.GetAllByUserIdAsync(userId) ?? [];
            categoriesResult = categories.Adapt<IEnumerable<CategoryResult>>();

            return CategoryClassifier.Classify(description, categoriesResult);
        }

        public async Task<CategoryResult> UpdateAsync(long id, UpdateCategoryDto dto)
        {
            Category category;

            Domain.Validate.NotNull(dto, nameof(UpdateCategoryDto));
            if (id != dto.Id)
                throw new DomainValidationException("O id da rota é diferente do id enviado no corpo.");
            Domain.Validate.Id(dto.Id, nameof(dto.Id));

            await GetOwnedAsync(dto.Id);

            if (!string.IsNullOrWhiteSpace(dto.Name))
                Domain.Validate.String(dto.Name, nameof(dto.Name), minLength: 2, maxLength: 100);

            if (dto.LimitValueCents != 0)
                Domain.Validate.Int(dto.LimitValueCents, nameof(dto.LimitValueCents), min: 0);

            category = await categoriesRepository.UpdateAsync(dto);
            if (category is null)
                throw new DomainValidationException("Erro ao atualizar a categoria.");

            return category.Adapt<CategoryResult>();
        }

        public async Task<CategoryResult> DeleteAsync(long id)
        {
            Category category;
            Category deactivated;

            Domain.Validate.Id(id, nameof(id));

            category = await GetOwnedAsync(id);

            if (category.IsDefault)
                throw new DomainValidationException("A categoria padrão Metas não pode ser excluída.");

            deactivated = await categoriesRepository.DeleteAsync(id);
            if (deactivated is null)
                throw new DomainValidationException("Erro ao desativar a categoria.");

            return deactivated.Adapt<CategoryResult>();
        }

        public async Task<IEnumerable<CategoryResult>> GetAllInactiveByUserIdAsync(long userId)
        {
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);
            _ = await userRepository.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            var categories = await categoriesRepository.GetAllInactiveByUserIdAsync(userId) ?? [];
            return categories.Adapt<IEnumerable<CategoryResult>>();
        }

        public async Task<CategoryResult> ReactivateAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            Category existing = await categoriesRepository.GetByIdIncludingInactiveAsync(id)
                ?? throw new DomainValidationException("Categoria não encontrada.");
            currentUser.EnsureCanAccess(existing.UserId);
            if (existing.Active)
                throw new DomainValidationException("A categoria já está ativa.");

            Category reactivated = await categoriesRepository.ReactivateAsync(id)
                ?? throw new DomainValidationException("A categoria já está ativa.");

            return reactivated.Adapt<CategoryResult>();
        }

        private async Task<Category> GetOwnedAsync(long id)
        {
            Category category = await categoriesRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Categoria não encontrada.");
            currentUser.EnsureCanAccess(category.UserId);

            return category;
        }
    }
}
