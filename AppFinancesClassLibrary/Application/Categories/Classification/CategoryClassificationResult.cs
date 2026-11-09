using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Application.Categories.Classification
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CategoryClassificationReason
    {
        /// Uma única categoria do usuário bateu com a descrição
        correspondencia_unica,

        ///Só bateu um termo genérico ("posto", "loja"): não dá para decidir sozinho
        termo_ambiguo,

        /// Mais de uma categoria do usuário bateu com a descrição
        multiplas_categorias,

        /// Nada na descrição corresponde a uma categoria do usuário
        sem_correspondencia,

        /// Os itens do cupom apontam, quase todos, para a mesma categoria
        itens_do_documento,

        /// O usuário já confirmou essa categoria para o mesmo estabelecimento (CNPJ ou nome)
        historico_usuario,

        /// A atividade principal registrada do CNPJ (CNAE) pertence a uma categoria do usuário
        atividade_do_cnpj
    }

    public class CategoryClassificationResult
    {
        /// Categoria sugerida; null sempre que RequiresConfirmation for true
        public long? CategoryId { get; init; }

        /// O usuário precisa escolher a categoria: a sugestão não é confiável
        public bool RequiresConfirmation { get; init; }

        public CategoryClassificationReason Reason { get; init; }

        /// Termo da descrição que decidiu (nome da categoria, palavra-chave ou termo ambíguo)
        public string MatchedTerm { get; init; }

        /// Categorias que bateram, quando foi mais de uma
        public IReadOnlyList<long> CandidateCategoryIds { get; init; } = [];
    }
}
