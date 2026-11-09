using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Constants;

namespace AppFinancesClassLibrary.Tests.Categories
{
    public class CategoryClassifierTests
    {
        private static readonly IEnumerable<CategoryResult> Categories =
        [
            new() { Id = 1, Name = MERCADO, Type = CategoriesTypes.Saida },
            new() { Id = 2, Name = TRANSPORTE, Type = CategoriesTypes.Saida },
            new() { Id = 3, Name = FARMACIA, Type = CategoriesTypes.Saida },
            new() { Id = 4, Name = RESTAURANTES, Type = CategoriesTypes.Saida },
            new() { Id = 5, Name = SALARIO, Type = CategoriesTypes.Entrada },
            new() { Id = 6, Name = METAS, Type = CategoriesTypes.Saida, IsDefault = true },
        ];

        [Theory]
        [InlineData(SUPERMERCADO_TESTE, 1)]
        [InlineData(ATACADAO_TESTE, 1)]
        [InlineData(UBER_TESTE, 2)]
        [InlineData(POSTO_TESTE, 2)]
        [InlineData(FARMACIA_TESTE, 3)]
        [InlineData(FARMACIA_DOIS_TESTE, 3)]
        [InlineData(RESTAURANTE_TESTE, 4)]
        [InlineData(FARMACIA_TRES_TESTE, 3)]
        public void Descricao_conhecida_vai_para_a_categoria_esperada(string description, long expectedCategoryId)
        {
            var result = CategoryClassifier.Classify(description, Categories);

            Assert.False(result.RequiresConfirmation);
            Assert.Equal(expectedCategoryId, result.CategoryId);
        }

        [Theory]
        [InlineData(POSTO_DOIS_TESTE)]
        [InlineData(LOJA_CENTRAL_TESTE)]
        [InlineData(COMERCIO_DE_VARIEDADES)]
        public void Descricao_ambigua_pede_confirmacao(string description)
        {
            var result = CategoryClassifier.Classify(description, Categories);

            Assert.True(result.RequiresConfirmation);
            Assert.Null(result.CategoryId);
            Assert.Equal(CategoryClassificationReason.termo_ambiguo, result.Reason);
        }

        [Fact]
        public void Descricao_desconhecida_pede_confirmacao()
        {
            var result = CategoryClassifier.Classify("XYZ 123", Categories);

            Assert.True(result.RequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.sem_correspondencia, result.Reason);
        }

        [Fact]
        public void Varias_categorias_pede_confirmacao()
        {
            var result = CategoryClassifier.Classify(FARMACIA_E_MERCADO_TESTE, Categories);

            Assert.True(result.RequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.multiplas_categorias, result.Reason);
            Assert.Equal([1L, 3L], result.CandidateCategoryIds.Order());
        }

        [Theory]
        [InlineData(FARMACIA_TESTE, FARMACIA_TESTE_UM)]
        [InlineData(SUPERMERCADO_TESTE, SUPERMERCADO_TESTE_UM)]
        public void Mesma_despesa_via_ocr_e_manual_tem_a_mesma_categoria(string fromOcr, string typedByUser)
        {
            var ocr = CategoryClassifier.Classify(fromOcr, Categories);
            var manual = CategoryClassifier.Classify(typedByUser, Categories);

            Assert.Equal(ocr.CategoryId, manual.CategoryId);
            Assert.Equal(ocr.RequiresConfirmation, manual.RequiresConfirmation);
        }

        [Fact]
        public void Reclassificacao_preserva_categoria_corrigida_pelo_usuario()
        {
            var corrected = new Transaction { Id = 10, CategoryId = 4, Description = SUPERMERCADO_TESTE, CategoryConfirmedManually = true };
            Assert.Null(TransactionReclassifier.NewCategoryFor(corrected, Categories));

            var automatic = new Transaction { Id = 11, CategoryId = 4, Description = SUPERMERCADO_TESTE, CategoryConfirmedManually = false };
            Assert.Equal(1, TransactionReclassifier.NewCategoryFor(automatic, Categories));
        }

        [Fact]
        public void Reclassificacao_nao_troca_saida_por_entrada()
        {
            var transaction = new Transaction { Id = 12, CategoryId = 1, Description = SALARIO_TESTE, CategoryConfirmedManually = false };

            Assert.Null(TransactionReclassifier.NewCategoryFor(transaction, Categories));
        }
    }
}
