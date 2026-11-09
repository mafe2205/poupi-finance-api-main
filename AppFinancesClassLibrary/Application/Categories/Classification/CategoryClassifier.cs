using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AppFinancesClassLibrary.Application.Categories.Classification
{
    public static class CategoryClassifier
    {
        private static readonly CategoryKeywordGroup[] Groups =
        [
            new(aliases: ["mercado", "supermercado", "supermercados", "mercearia", "feira", "compras", "alimentacao", "comida"],
                strong: ["supermercado", "supermercados", "mercearia", "atacadao", "atacarejo", "assai", "carrefour",
                         "pao de acucar", "hortifruti", "sacolao", "acougue", "hipermercado", "minimercado", "dia supermercado",
                         "big bompreco", "sams club", "tenda atacado"],
                establishmentOnly: ["mercado", "makro"],
                items: ["arroz", "feijao", "acucar", "leite", "macarrao", "farinha", "fuba", "queijo", "presunto", "mortadela",
                        "salsicha", "linguica", "frango", "carne moida", "alcatra", "patinho", "iogurte", "manteiga", "margarina",
                        "requeijao", "biscoito", "bolacha", "achocolatado", "leite condensado", "creme de leite", "sardinha",
                        "atum", "extrato de tomate", "molho de tomate", "detergente", "sabao", "amaciante", "desinfetante",
                        "papel higienico", "creme dental", "banana", "tomate", "batata", "cebola", "alho", "ovos"],
                cnae: ["4711", "4712", "4722", "4724", "4729", "4639"]),

            new(aliases: ["combustivel", "combustiveis", "gasolina", "abastecimento", "carro", "veiculo", "transporte", "transportes"],
                strong: ["combustivel", "combustiveis", "gasolina", "etanol", "diesel", "auto posto", "posto de combustivel",
                         "petrobras", "br mania"],
                establishmentOnly: ["ipiranga", "shell"],
                items: ["gasolina", "etanol", "diesel", "gnv", "arla", "aditivada"],
                strongItems: ["gasolina", "etanol", "diesel", "gnv"],
                cnae: ["4731", "4732"]),

            new(aliases: ["transporte", "transportes", "mobilidade", "carro", "veiculo"],
                strong: ["uber", "99app", "99pop", "cabify", "bilhete unico", "estacionamento", "pedagio", "sem parar"],
                establishmentOnly: ["99 pop", "taxi", "metro", "onibus", "passagem"],
                items: [],
                cnae: ["4921", "4922", "4923", "4929", "5221", "5223"]),

            new(aliases: ["farmacia", "farmacias", "remedios", "medicamentos", "saude"],
                strong: ["farmacia", "drogaria", "drogarias", "drogasil", "droga raia", "drogaraia", "pague menos", "panvel",
                         "sao joao farmacias", "medicamento", "medicamentos", "remedio", "remedios"],
                establishmentOnly: ["pacheco", "nissei"],
                items: ["dipirona", "paracetamol", "ibuprofeno", "amoxicilina", "azitromicina", "losartana", "omeprazol",
                        "dorflex", "neosaldina", "buscopan", "nimesulida", "cetoprofeno", "loratadina", "dexametasona",
                        "prednisolona", "sinvastatina", "metformina", "comprimido", "comprimidos", "capsula", "capsulas",
                        "xarope", "pomada", "colirio", "soro fisiologico", "generico"],
                cnae: ["4771"]),

            new(aliases: ["saude", "medico", "medicos", "consultas", "plano de saude"],
                strong: ["clinica", "laboratorio", "hospital", "odontologia", "consulta medica"],
                establishmentOnly: [],
                items: [],
                cnae: ["8610", "8630", "8640", "8650", "8660", "8690"]),

            new(aliases: ["restaurante", "restaurantes", "lanche", "lanches", "delivery", "alimentacao", "comida"],
                strong: ["restaurante", "ifood", "rappi", "hamburgueria", "churrascaria", "mcdonalds", "mc donalds",
                         "burger king", "bar e restaurante"],
                establishmentOnly: ["lanchonete", "padaria", "pizzaria", "sorveteria", "cafeteria", "subway", "self service", "pastelaria",
                                    "grill", "bistro", "cantina", "trattoria", "sushi", "temakeria", "espetinho", "petiscaria",
                                    "galeteria", "steakhouse"],
                items: ["refeicao", "buffet", "rodizio", "espeto", "porcao", "prato executivo", "prato feito", "marmita",
                        "marmitex", "a la carte", "couvert", "sobremesa", "x salada", "x bacon", "hamburguer", "pizza",
                        "calzone", "esfiha", "chopp", "caipirinha"],
                strongItems: ["refeicao", "buffet", "rodizio", "marmitex", "prato executivo", "prato feito", "a la carte"],
                cnae: ["5611", "5612", "5620"]),

            new(aliases: ["moradia", "casa", "contas", "contas da casa", "servicos"],
                strong: ["aluguel", "condominio", "conta de luz", "energia eletrica", "cemig", "copel", "coelba", "sabesp",
                         "conta de agua"],
                establishmentOnly: ["enel", "internet", "vivo", "claro", "tim", "net servicos"],
                items: [],
                cnae: ["3511", "3514", "3600", "6110", "6120", "6130", "6190"]),

            new(aliases: ["lazer", "entretenimento", "diversao", "assinaturas"],
                strong: ["netflix", "spotify", "prime video"],
                establishmentOnly: ["cinema", "ingresso", "ingressos", "disney", "hbo", "teatro", "steam", "playstation", "xbox"],
                items: [],
                cnae: ["5914", "9001", "9002", "9003", "9101", "9102", "9311", "9312", "9319", "9321", "9329"]),

            new(aliases: ["educacao", "estudos", "escola", "faculdade", "cursos"],
                strong: ["faculdade", "universidade", "livraria", "material escolar", "papelaria"],
                establishmentOnly: ["escola", "curso"],
                items: [],
                cnae: ["85"]),

            new(aliases: ["vestuario", "roupas", "roupa", "moda"],
                strong: ["renner", "riachuelo", "calcados", "sapataria"],
                establishmentOnly: ["zara", "hering", "marisa", "centauro"],
                items: [],
                cnae: ["4781", "4782"]),

            new(aliases: ["hotel", "hoteis", "hospedagem", "viagem", "viagens"],
                strong: ["hotel", "pousada", "hostel"],
                establishmentOnly: [],
                items: [],
                cnae: ["5510", "5590"]),
        ];

        private static readonly string[] FalseFriends = ["mercado pago", "mercadopago", "mercado livre", "mercadolivre"];

        private static readonly string[] AmbiguousTerms =
        [
            "posto", "loja", "lojas", "comercio", "comercial", "servicos", "servico", "store", "shop", "center",
            "magazine", "bazar", "distribuidora", "conveniencia", "emporio", "variedades", "utilidades"
        ];

        private const int HeaderLines = 8;

        private const int MinItemVotes = 2;
        private const double MinItemShare = 0.75;

        private const int MinCanonicalLength = 4;
        private const int MinFuzzyLength = 7;

        private static readonly Regex AddressRegex = new(
            @"^(?:rua|r|av|avenida|rod|rodovia|estrada|travessa|tv|alameda|praca|endereco|end|logradouro)\b|\bcep\b|(?<!\d)\d{5}\s?\d{3}(?!\d)",
            RegexOptions.Compiled);

        public static CategoryClassificationResult Classify(string description, IEnumerable<CategoryResult> userCategories)
        {
            var categories = Eligible(userCategories);
            var text = WithoutFalseFriends(Normalize(description));
            if (text.Length == 0)
                return NeedsConfirmation(CategoryClassificationReason.sem_correspondencia);

            var candidates = new Candidates();
            MatchNames(text, categories, candidates, fuzzy: true);
            MatchGroups(text, categories, candidates, g => g.Strong.Concat(g.EstablishmentOnly), fuzzy: true);

            var result = candidates.ToResult();
            if (result is not null) return result;

            var ambiguous = AmbiguousTerms.FirstOrDefault(t => ContainsTerm(text, t, fuzzy: true));
            return ambiguous is not null
                ? NeedsConfirmation(CategoryClassificationReason.termo_ambiguo, ambiguous)
                : NeedsConfirmation(CategoryClassificationReason.sem_correspondencia);
        }

        public static CategoryClassificationResult ClassifyDocument(CategoryEvidence evidence, IEnumerable<CategoryResult> userCategories,
            CategoryHistory history = null)
        {
            var categories = Eligible(userCategories);

            var byHistory = (history ?? CategoryHistory.Empty).Classify(evidence.EstablishmentKey, evidence.Establishment, categories);
            if (byHistory is not null) return byHistory;

            var byEstablishment = Classify(evidence.Establishment ?? string.Empty, categories);
            var byCnae = ClassifyCnae(evidence.Cnae, categories);

            if (!byEstablishment.RequiresConfirmation)
                return byCnae is null || byCnae.CategoryId == byEstablishment.CategoryId ? byEstablishment : Conflict(byEstablishment, byCnae);

            if (byCnae is not null)
            {
                if (byEstablishment.Reason != CategoryClassificationReason.multiplas_categorias
                    || byEstablishment.CandidateCategoryIds.Contains(byCnae.CategoryId!.Value))
                    return byCnae;
            }

            var byText = ClassifyText(evidence.FullText, categories);
            var byItems = ClassifyItems(evidence.ItemDescriptions, categories);
            var fromText = Combine(byText, byItems);

            if (byEstablishment.Reason == CategoryClassificationReason.multiplas_categorias)
                return fromText is { RequiresConfirmation: false } && byEstablishment.CandidateCategoryIds.Contains(fromText.CategoryId!.Value)
                    ? fromText
                    : byEstablishment;

            if (fromText is not null) return fromText;

            return byEstablishment.Reason == CategoryClassificationReason.sem_correspondencia ? byText : byEstablishment;
        }

        private static CategoryClassificationResult ClassifyCnae(int? cnae, IReadOnlyList<CategoryResult> categories)
        {
            if (cnae is not { } code) return null;

            var digits = code.ToString("D7", CultureInfo.InvariantCulture);
            var group = Groups
                .Select(g => (Group: g, Prefix: g.Cnae.Where(p => digits.StartsWith(p, StringComparison.Ordinal)).MaxBy(p => p.Length)))
                .Where(m => m.Prefix is not null)
                .OrderByDescending(m => m.Prefix.Length)
                .Select(m => m.Group)
                .FirstOrDefault();

            var category = group?.ResolveCategory(categories);
            if (category is null) return null;

            return new CategoryClassificationResult
            {
                CategoryId = category.Id,
                RequiresConfirmation = false,
                Reason = CategoryClassificationReason.atividade_do_cnpj,
                MatchedTerm = $"CNAE {digits}"
            };
        }

        private static CategoryClassificationResult Conflict(CategoryClassificationResult first, CategoryClassificationResult second) => new()
        {
            RequiresConfirmation = true,
            Reason = CategoryClassificationReason.multiplas_categorias,
            MatchedTerm = $"{first.MatchedTerm}, {second.MatchedTerm}",
            CandidateCategoryIds = [first.CategoryId!.Value, second.CategoryId!.Value]
        };

        private static CategoryClassificationResult Combine(CategoryClassificationResult byText, CategoryClassificationResult byItems)
        {
            if (!byText.RequiresConfirmation)
                return byItems is null || byItems.CategoryId == byText.CategoryId ? byText : Conflict(byText, byItems);

            if (byText.Reason == CategoryClassificationReason.multiplas_categorias)
                return byItems is not null && byText.CandidateCategoryIds.Contains(byItems.CategoryId!.Value) ? byItems : byText;

            return byItems;
        }

        private static CategoryClassificationResult ClassifyText(string fullText, IReadOnlyList<CategoryResult> categories)
        {
            var lines = (fullText ?? string.Empty)
                .Split('\n')
                .Select(l => WithoutFalseFriends(Normalize(l)))
                .Where(l => l.Length > 0 && !AddressRegex.IsMatch(l) && !l.StartsWith("instituicao"))
                .ToList();
            if (lines.Count == 0) return NeedsConfirmation(CategoryClassificationReason.sem_correspondencia);

            var header = string.Join(' ', lines.Take(HeaderLines));
            var all = string.Join(' ', lines);

            var candidates = new Candidates();
            MatchNames(header, categories, candidates, fuzzy: false);
            MatchGroups(all, categories, candidates, g => g.Strong, fuzzy: false);

            var result = candidates.ToResult();
            if (result is not null) return result;

            var ambiguous = AmbiguousTerms.FirstOrDefault(t => ContainsWord(header, t));
            return ambiguous is not null
                ? NeedsConfirmation(CategoryClassificationReason.termo_ambiguo, ambiguous)
                : NeedsConfirmation(CategoryClassificationReason.sem_correspondencia);
        }

        private static CategoryClassificationResult ClassifyItems(IEnumerable<string> itemDescriptions, IReadOnlyList<CategoryResult> categories)
        {
            var votes = new Dictionary<CategoryKeywordGroup, int>();
            var strongVotes = new HashSet<CategoryKeywordGroup>();
            var total = 0;

            foreach (var description in itemDescriptions ?? [])
            {
                var text = Normalize(description);
                var groups = Groups.Where(g => g.Items.Concat(g.StrongItems).Any(k => ContainsTerm(text, k, fuzzy: true))).ToList();
                if (groups.Count != 1) continue;

                total++;
                votes[groups[0]] = votes.GetValueOrDefault(groups[0]) + 1;
                if (groups[0].StrongItems.Any(k => ContainsTerm(text, k, fuzzy: true))) strongVotes.Add(groups[0]);
            }

            if (total == 0) return null;

            var (group, count) = votes.MaxBy(v => v.Value);
            var unanimousStrong = count == total && strongVotes.Contains(group);
            if (!unanimousStrong && (count < MinItemVotes || count < total * MinItemShare)) return null;

            var category = group.ResolveCategory(categories);
            if (category is null) return null;

            return new CategoryClassificationResult
            {
                CategoryId = category.Id,
                RequiresConfirmation = false,
                Reason = CategoryClassificationReason.itens_do_documento,
                MatchedTerm = $"{count} de {total} itens"
            };
        }

        private static void MatchNames(string text, IReadOnlyList<CategoryResult> categories, Candidates candidates, bool fuzzy)
        {
            foreach (var category in categories)
            {
                var name = Normalize(category.Name);
                if (name.Length >= 3 && !AmbiguousTerms.Contains(name) && ContainsTerm(text, name, fuzzy))
                    candidates.Add(category.Id, category.Name);
            }
        }

        private static void MatchGroups(string text, IReadOnlyList<CategoryResult> categories, Candidates candidates,
            Func<CategoryKeywordGroup, IEnumerable<string>> keywords, bool fuzzy)
        {
            foreach (var group in Groups)
            {
                var keyword = keywords(group).FirstOrDefault(k => ContainsTerm(text, k, fuzzy));
                if (keyword is null) continue;

                var category = group.ResolveCategory(categories);
                if (category is not null) candidates.Add(category.Id, keyword);
            }
        }

        private static IReadOnlyList<CategoryResult> Eligible(IEnumerable<CategoryResult> userCategories) =>
            (userCategories ?? [])
                .Where(c => c.Active && !c.IsDefault && !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Id)
                .ToList();

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var lastWasSpace = true;

            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        public static string Canonical(string normalized)
        {
            var chars = normalized.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = chars[i] switch
                {
                    '0' => 'o',
                    '1' or 'l' => 'i',
                    '5' => 's',
                    '8' => 'b',
                    var c => c
                };
            }

            return new string(chars);
        }

        private static string WithoutFalseFriends(string normalized)
        {
            var padded = $" {normalized} ";
            foreach (var term in FalseFriends)
                padded = padded.Replace($" {term} ", " ", StringComparison.Ordinal);

            return padded.Trim();
        }

        private static bool ContainsWord(string normalizedText, string normalizedTerm) =>
            $" {normalizedText} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);

        private static bool ContainsTerm(string normalizedText, string normalizedTerm, bool fuzzy)
        {
            if (ContainsWord(normalizedText, normalizedTerm)) return true;
            if (!fuzzy || normalizedTerm.Length < MinCanonicalLength) return false;

            var term = Canonical(normalizedTerm);
            var words = Canonical(normalizedText).Split(' ');
            var size = term.Count(c => c == ' ') + 1;

            for (var i = 0; i + size <= words.Length; i++)
            {
                var window = string.Join(' ', words, i, size);
                if (window == term) return true;
                if (term.Length >= MinFuzzyLength && Math.Abs(window.Length - term.Length) <= 1 && WithinOneEdit(window, term))
                    return true;
            }

            return false;
        }

        private static bool WithinOneEdit(string a, string b)
        {
            if (a.Length > b.Length) (a, b) = (b, a);

            var i = 0;
            while (i < a.Length && a[i] == b[i]) i++;
            if (i == a.Length) return b.Length - a.Length <= 1;

            return a.Length == b.Length
                ? a.AsSpan(i + 1).SequenceEqual(b.AsSpan(i + 1))
                : a.AsSpan(i).SequenceEqual(b.AsSpan(i + 1));
        }

        private static CategoryClassificationResult NeedsConfirmation(CategoryClassificationReason reason, string term = null) => new()
        {
            RequiresConfirmation = true,
            Reason = reason,
            MatchedTerm = term
        };

        private sealed class Candidates
        {
            private readonly List<(long CategoryId, string Term)> items = [];

            public void Add(long categoryId, string term)
            {
                if (items.All(c => c.CategoryId != categoryId)) items.Add((categoryId, term));
            }

            public CategoryClassificationResult ToResult() => items.Count switch
            {
                0 => null,
                1 => new CategoryClassificationResult
                {
                    CategoryId = items[0].CategoryId,
                    RequiresConfirmation = false,
                    Reason = CategoryClassificationReason.correspondencia_unica,
                    MatchedTerm = items[0].Term
                },
                _ => new CategoryClassificationResult
                {
                    RequiresConfirmation = true,
                    Reason = CategoryClassificationReason.multiplas_categorias,
                    MatchedTerm = string.Join(", ", items.Select(c => c.Term)),
                    CandidateCategoryIds = items.Select(c => c.CategoryId).ToList()
                }
            };
        }

        private sealed class CategoryKeywordGroup(string[] aliases, string[] strong, string[] establishmentOnly, string[] items,
            string[] strongItems = null, string[] cnae = null)
        {
            public string[] Strong { get; } = strong;
            public string[] EstablishmentOnly { get; } = establishmentOnly;
            public string[] Items { get; } = items;
            public string[] StrongItems { get; } = strongItems ?? [];
            public string[] Cnae { get; } = cnae ?? [];

            public CategoryResult ResolveCategory(IReadOnlyList<CategoryResult> categories)
            {
                foreach (var alias in aliases)
                {
                    var exact = categories.FirstOrDefault(c => Normalize(c.Name) == alias);
                    if (exact is not null) return exact;
                }

                foreach (var alias in aliases)
                {
                    var partial = categories.FirstOrDefault(c => ContainsWord(Normalize(c.Name), alias));
                    if (partial is not null) return partial;
                }

                return null;
            }
        }
    }
}
