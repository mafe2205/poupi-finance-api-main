using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Tests.Constants;

namespace AppFinancesClassLibrary.Tests.Categories
{
    public class CategoryDocumentClassificationTests
    {
        private const string PANVEL_CNPJ = "92665611064574";

        private readonly DocumentParsingService parser = new();

        private static readonly List<CategoryResult> Categories =
        [
            new() { Id = 1, Name = MERCADO, Type = CategoriesTypes.Saida },
            new() { Id = 2, Name = TRANSPORTE, Type = CategoriesTypes.Saida },
            new() { Id = 3, Name = FARMACIA, Type = CategoriesTypes.Saida },
            new() { Id = 4, Name = RESTAURANTES, Type = CategoriesTypes.Saida },
            new() { Id = 5, Name = SALARIO, Type = CategoriesTypes.Entrada },
            new() { Id = 6, Name = METAS, Type = CategoriesTypes.Saida, IsDefault = true },
            new() { Id = 7, Name = "Banco", Type = CategoriesTypes.Saida },
        ];

        private static CategoryHistory History(params CategoryHistoryEntry[] entries) => new(entries);


        [Fact]
        public void Uma_confirmacao_pelo_cnpj_decide_mesmo_contra_a_palavra_chave()
        {
            var history = History(new CategoryHistoryEntry { EstablishmentKey = PANVEL_CNPJ, SuggestedDescription = "Panvel", CategoryId = 1 });

            var parsed = parser.Parse("""
                Panvel - Filial 873
                CNPJ: 92.665.611/0645-74
                Valor a Pagar R$   62,99
                """, Categories, history);

            Assert.False(parsed.CategoryRequiresConfirmation);
            Assert.Equal(1, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.historico_usuario, parsed.CategoryReason);
        }

        [Fact]
        public void Pelo_nome_sao_precisas_duas_confirmacoes()
        {
            var one = History(new CategoryHistoryEntry { SuggestedDescription = "LOJA CENTRAL", CategoryId = 4 });
            var two = History(
                new CategoryHistoryEntry { SuggestedDescription = "LOJA CENTRAL", CategoryId = 4 },
                new CategoryHistoryEntry { ConfirmedDescription = "Loja Central", CategoryId = 4 });

            var withOne = parser.Parse("LOJA CENTRAL\nTOTAL R$ 50,00", Categories, one);
            var withTwo = parser.Parse("LOJA CENTRAL\nTOTAL R$ 50,00", Categories, two);

            Assert.True(withOne.CategoryRequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.termo_ambiguo, withOne.CategoryReason);
            Assert.Equal(4, withTwo.CategoryId);
            Assert.Equal(CategoryClassificationReason.historico_usuario, withTwo.CategoryReason);
        }

        [Fact]
        public void Nome_lido_com_erro_de_ocr_casa_com_o_historico()
        {
            var history = History(
                new CategoryHistoryEntry { SuggestedDescription = "RECH E SILVA LTDA", CategoryId = 1 },
                new CategoryHistoryEntry { SuggestedDescription = "RECH E SILVA LTDA", CategoryId = 1 });

            var parsed = parser.Parse("RECH E SI1VA LTDA\nTOTAL R$ 50,00", Categories, history);

            Assert.Equal(1, parsed.CategoryId);
        }

        [Fact]
        public void Escolhas_diferentes_para_a_mesma_loja_voltam_para_o_usuario()
        {
            var history = History(
                new CategoryHistoryEntry { EstablishmentKey = PANVEL_CNPJ, CategoryId = 1 },
                new CategoryHistoryEntry { EstablishmentKey = PANVEL_CNPJ, CategoryId = 3 });

            var parsed = parser.Parse("Panvel\nCNPJ: 92.665.611/0645-74\nTOTAL R$ 50,00", Categories, history);

            Assert.True(parsed.CategoryRequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.multiplas_categorias, parsed.CategoryReason);
        }

        [Fact]
        public void Historico_com_categoria_desativada_nao_conta()
        {
            var categories = Categories.Select(c => c.Id == 1
                ? new CategoryResult { Id = 1, Name = MERCADO, Type = CategoriesTypes.Saida, Active = false }
                : c).ToList();
            var history = History(new CategoryHistoryEntry { EstablishmentKey = PANVEL_CNPJ, CategoryId = 1 });

            var parsed = parser.Parse("Panvel\nCNPJ: 92.665.611/0645-74\nTOTAL R$ 50,00", categories, history);

            Assert.Equal(3, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.correspondencia_unica, parsed.CategoryReason);
        }

        [Fact]
        public void Reclassificacao_nao_passa_por_cima_do_historico()
        {
            var history = History(
                new CategoryHistoryEntry { SuggestedDescription = SUPERMERCADO_TESTE, CategoryId = 4 },
                new CategoryHistoryEntry { SuggestedDescription = SUPERMERCADO_TESTE, CategoryId = 4 });
            var transaction = new Transaction
            {
                Id = 1, CategoryId = 4, Description = SUPERMERCADO_TESTE, DescriptionOCR = SUPERMERCADO_TESTE, CategoryConfirmedManually = false
            };

            Assert.Equal(1, TransactionReclassifier.NewCategoryFor(transaction, Categories));
            Assert.Null(TransactionReclassifier.NewCategoryFor(transaction, Categories, history));
        }

        // ---------- Texto inteiro: só termos fortes, nome de categoria só no cabeçalho ----------

        [Fact]
        public void Endereco_nao_decide_a_categoria()
        {
            var parsed = parser.Parse("""
                XYZ COMERCIO DE ARTIGOS
                AV. IPIRANGA, 1200 - CENTRO
                TOTAL R$ 50,00
                """, Categories);

            Assert.NotEqual(2, parsed.CategoryId);
            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Fact]
        public void Palavra_comum_no_rodape_nao_decide_a_categoria()
        {
            var lines = string.Join('\n', Enumerable.Range(1, 9).Select(i => $"LINHA {i}"));
            var parsed = parser.Parse($"XYZ LTDA\n{lines}\nTOTAL R$ 50,00\nFicou claro? Consulte a nota no site\n", Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.sem_correspondencia, parsed.CategoryReason);
        }

        [Theory]
        [InlineData("MERCADO PAGO*LOJA XYZ")]
        [InlineData("MERCADOLIVRE")]
        [InlineData("MERCADO LIVRE")]
        public void Mercado_pago_e_mercado_livre_nao_sao_mercado(string description)
        {
            var parsed = parser.Parse($"{description}\nPagamento via Mercado Pago\nTOTAL R$ 50,00", Categories);

            Assert.NotEqual(1, parsed.CategoryId);
        }

        [Fact]
        public void Instituicao_do_pix_nao_vira_a_categoria_banco()
        {
            var parsed = parser.Parse("""
                Pix realizado com sucesso!
                Valor   R$ 450,00
                Nome   ANA PEREIRA
                Instituição   BANCO BTG PACTUAL S.A.
                """, Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Fact]
        public void Nome_da_categoria_fora_do_cabecalho_nao_conta()
        {
            var lines = string.Join('\n', Enumerable.Range(1, 9).Select(i => $"LINHA {i}"));
            var parsed = parser.Parse($"XYZ LTDA\n{lines}\nObrigado, volte ao Restaurantes\n", Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Fact]
        public void Termo_forte_vale_no_texto_inteiro()
        {
            var parsed = parser.Parse("""
                XYZ LTDA
                1409-1 GASOLINA COMUM   23,114 L   6,49   150,00
                TOTAL R$ 150,00
                """, Categories);

            Assert.Equal(2, parsed.CategoryId);
        }

        // ---------- Itens do cupom ----------

        [Fact]
        public void Itens_de_mercado_decidem_quando_o_nome_e_ambiguo()
        {
            var parsed = parser.Parse("""
                COMERCIAL ZAFFARI LTDA
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                003 7896005800027 FEIJAO PRETO 1KG 1 UN X 8,90 8,90
                TOTAL R$ 41,78
                """, Categories);

            Assert.False(parsed.CategoryRequiresConfirmation);
            Assert.Equal(1, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.itens_do_documento, parsed.CategoryReason);
        }

        [Fact]
        public void Um_item_so_nao_decide()
        {
            var parsed = parser.Parse("""
                COMERCIAL XYZ LTDA
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 PILHA AA 1 UN X 22,90 22,90
                TOTAL R$ 32,88
                """, Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Fact]
        public void Itens_divididos_entre_categorias_nao_decidem()
        {
            var parsed = parser.Parse("""
                COMERCIAL XYZ LTDA
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                003 7896005800034 DIPIRONA 500MG 1 UN X 8,90 8,90
                004 7896005800041 PARACETAMOL 750MG 1 UN X 9,90 9,90
                TOTAL R$ 51,68
                """, Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Fact]
        public void Texto_e_itens_discordando_pede_confirmacao()
        {
            var parsed = parser.Parse("""
                COMERCIAL XYZ LTDA
                Medicamentos e perfumaria
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 32,88
                """, Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.multiplas_categorias, parsed.CategoryReason);
        }

        // ---------- Erros de OCR no estabelecimento ----------

        [Theory]
        [InlineData("DROGARlA SAO PAULO", 3)]
        [InlineData("SUPERMERCAD0 BOM PRECO", 1)]
        [InlineData("SUPERMRCADO BOM PRECO", 1)]
        [InlineData("RESTAURANTF DA ESQUINA", 4)]
        public void Estabelecimento_com_erro_de_ocr_ainda_casa(string description, long expectedCategoryId)
        {
            var result = CategoryClassifier.Classify(description, Categories);

            Assert.False(result.RequiresConfirmation);
            Assert.Equal(expectedCategoryId, result.CategoryId);
        }

        [Fact]
        public void Termo_ambiguo_com_erro_de_ocr_continua_ambiguo()
        {
            var result = CategoryClassifier.Classify("P0STO LEAO CITY", Categories);

            Assert.Equal(CategoryClassificationReason.termo_ambiguo, result.Reason);
        }

        [Theory]
        [InlineData("MERCADINHO DA VILA")]
        [InlineData("DROGARIAS E MERCADOS")]
        public void Palavra_parecida_nao_e_tratada_como_erro_de_ocr(string description)
        {
            var result = CategoryClassifier.Classify(description, Categories);

            Assert.NotEqual(CategoryClassificationReason.correspondencia_unica, result.Reason);
        }

        // ---------- Grupos ----------

        [Fact]
        public void Clinica_vai_para_saude_e_nunca_para_farmacia()
        {
            List<CategoryResult> withHealth = [.. Categories, new() { Id = 8, Name = "Saúde", Type = CategoriesTypes.Saida }];

            Assert.Equal(8, CategoryClassifier.Classify("CLINICA PERPETUE LTDA", withHealth).CategoryId);
            Assert.True(CategoryClassifier.Classify("CLINICA PERPETUE LTDA", Categories).RequiresConfirmation);
        }

        [Fact]
        public void Combustivel_vai_para_a_categoria_mais_especifica()
        {
            List<CategoryResult> withFuel = [.. Categories, new() { Id = 8, Name = "Gasolina", Type = CategoriesTypes.Saida }];

            Assert.Equal(8, CategoryClassifier.Classify("AUTO POSTO IPIRANGA", withFuel).CategoryId);
            Assert.Equal(2, CategoryClassifier.Classify("UBER *TRIP", withFuel).CategoryId);
        }

        // ---------- Atividade do CNPJ (CNAE) ----------

        [Theory]
        [InlineData(4711302, 1)] // supermercados
        [InlineData(4639701, 1)] // atacadista de produtos alimentícios
        [InlineData(4731800, 2)] // combustíveis
        [InlineData(4771701, 3)] // farmácia
        [InlineData(5611201, 4)] // restaurantes
        public void Atividade_do_cnpj_decide_quando_o_nome_nao_decide(int cnae, long expectedCategoryId)
        {
            var parsed = parser.Parse("COMERCIAL XYZ LTDA\nTOTAL R$ 50,00", Categories, establishmentCnae: cnae);

            Assert.False(parsed.CategoryRequiresConfirmation);
            Assert.Equal(expectedCategoryId, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.atividade_do_cnpj, parsed.CategoryReason);
        }

        [Theory]
        [InlineData(4763602)] // artigos esportivos: fora dos grupos
        [InlineData(5510801)] // hotéis: o usuário não tem categoria de hospedagem
        public void Atividade_sem_categoria_do_usuario_nao_decide(int cnae)
        {
            var parsed = parser.Parse("COMERCIAL XYZ LTDA\nTOTAL R$ 50,00", Categories, establishmentCnae: cnae);

            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Fact]
        public void Hotel_vai_para_a_categoria_de_hospedagem()
        {
            List<CategoryResult> withHotel = [.. Categories, new() { Id = 8, Name = "Hotel", Type = CategoriesTypes.Saida }];

            var parsed = parser.Parse("ibis\nTOTAL R$ 50,00", withHotel, establishmentCnae: 5510801);

            Assert.Equal(8, parsed.CategoryId);
        }

        [Fact]
        public void Nome_e_atividade_discordando_pede_confirmacao()
        {
            var parsed = parser.Parse("SUPERMERCADO BOM PRECO\nTOTAL R$ 50,00", Categories, establishmentCnae: 5611201);

            Assert.True(parsed.CategoryRequiresConfirmation);
            Assert.Equal(CategoryClassificationReason.multiplas_categorias, parsed.CategoryReason);
        }

        [Fact]
        public void Atividade_vale_mais_que_o_texto()
        {
            var parsed = parser.Parse("COMERCIAL XYZ LTDA\nTOTAL R$ 50,00\nMedicamentos so com receita", Categories, establishmentCnae: 4711302);

            Assert.Equal(1, parsed.CategoryId);
        }

        [Fact]
        public void Historico_vale_mais_que_a_atividade()
        {
            var history = History(new CategoryHistoryEntry { EstablishmentKey = PANVEL_CNPJ, CategoryId = 1 });

            var parsed = parser.Parse("Panvel\nCNPJ: 92.665.611/0645-74\nTOTAL R$ 50,00", Categories, history, establishmentCnae: 4771701);

            Assert.Equal(1, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.historico_usuario, parsed.CategoryReason);
        }

        // ---------- Histórico pelo recebedor do Pix ----------

        [Fact]
        public void Segundo_pix_para_a_mesma_pessoa_usa_o_historico()
        {
            var history = History(new CategoryHistoryEntry { EstablishmentKey = "cpf:111222", SuggestedDescription = "JOAO SILVA SANTOS", CategoryId = 2 });

            var parsed = parser.Parse("""
                Dados do recebedor
                Instituição   COOP SICREDI SUL SC
                Nome   JOAO SILVA SANTOS
                CPF/CNPJ   ***.111.222-**
                """, Categories, history);

            Assert.Equal(2, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.historico_usuario, parsed.CategoryReason);
        }

        // ---------- Item forte e nomes de restaurante ----------

        [Fact]
        public void Refeicao_por_quilo_decide_sozinha()
        {
            var parsed = parser.Parse("""
                COMERCIAL XYZ LTDA
                001 1   REFEICAO
                0,628 KG x 159,90   100,42
                002 97   SCHWEPPES TONICA
                1,000 UN x 7,00   7,00
                TOTAL R$ 107,42
                """, Categories);

            Assert.Equal(4, parsed.CategoryId);
            Assert.Equal(CategoryClassificationReason.itens_do_documento, parsed.CategoryReason);
        }

        [Fact]
        public void Item_forte_com_voto_contrario_nao_decide()
        {
            var parsed = parser.Parse("""
                COMERCIAL XYZ LTDA
                001 1   REFEICAO
                0,628 KG x 159,90   100,42
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 123,32
                """, Categories);

            Assert.True(parsed.CategoryRequiresConfirmation);
        }

        [Theory]
        [InlineData("TEXANO GRILL")]
        [InlineData("CANTINA DA NONNA")]
        [InlineData("SUSHI KEN")]
        public void Nomes_tipicos_de_restaurante(string description)
        {
            Assert.Equal(4, CategoryClassifier.Classify(description, Categories).CategoryId);
        }
    }
}
