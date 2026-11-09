using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.User.Services
{
    public static class StarterCategories
    {
        public static readonly IReadOnlyList<(string Name, CategoriesTypes Type)> All =
        [
            ("Salário", CategoriesTypes.Entrada),
            ("Outras receitas", CategoriesTypes.Entrada),

            ("Mercado", CategoriesTypes.Saida),
            ("Restaurante", CategoriesTypes.Saida),
            ("Moradia", CategoriesTypes.Saida),
            ("Transporte", CategoriesTypes.Saida),
            ("Combustível", CategoriesTypes.Saida),
            ("Saúde", CategoriesTypes.Saida),
            ("Farmácia", CategoriesTypes.Saida),
            ("Educação", CategoriesTypes.Saida),
            ("Lazer", CategoriesTypes.Saida),
            ("Vestuário", CategoriesTypes.Saida),
            ("Outros", CategoriesTypes.Saida),
        ];
    }
}
