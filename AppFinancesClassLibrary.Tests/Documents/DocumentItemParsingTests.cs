using AppFinancesClassLibrary.Application.Documents.Services;

namespace AppFinancesClassLibrary.Tests.Documents
{
    public class DocumentItemParsingTests
    {
        private readonly DocumentParsingService parser = new();

        [Fact]
        public void Le_itens_de_cupom_em_uma_linha()
        {
            var text = """
                SUPERMERCADO BOM PRECO LTDA
                CNPJ 12.345.678/0001-90
                ITEM CODIGO DESCRICAO QTD UN VL UNIT VL ITEM
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1UN X 22,90 F1 22,90
                003 2000123 PICANHA BOVINA 0,532 KG x 79,90 42,51
                TOTAL R$ 75,39
                """;

            var items = parser.ParseItems(text);

            Assert.Equal(3, items.Count);

            Assert.Equal(1, items[0].LineNumber);
            Assert.Equal("7891000100103", items[0].Code);
            Assert.Equal("LEITE INTEGRAL 1L", items[0].Description);
            Assert.Equal(2m, items[0].Quantity);
            Assert.Equal("UN", items[0].Unit);
            Assert.Equal(499, items[0].UnitPriceCents);
            Assert.Equal(998, items[0].TotalCents);

            Assert.Equal("ARROZ TIPO 1 5KG", items[1].Description);
            Assert.Equal(2290, items[1].TotalCents);

            Assert.Equal("PICANHA BOVINA", items[2].Description);
            Assert.Equal(0.532m, items[2].Quantity);
            Assert.Equal("KG", items[2].Unit);
            Assert.Equal(4251, items[2].TotalCents);
        }

        [Fact]
        public void Le_itens_de_cupom_em_duas_linhas()
        {
            var text = """
                001 7891000100103 LEITE INTEGRAL 1L
                    2 UN X 4,99            9,98
                002 7891910000197 ACUCAR REFINADO 1KG
                    3 UN X 5,49  T18,00%   16,47
                """;

            var items = parser.ParseItems(text);

            Assert.Equal(2, items.Count);
            Assert.Equal("LEITE INTEGRAL 1L", items[0].Description);
            Assert.Equal("7891000100103", items[0].Code);
            Assert.Equal(998, items[0].TotalCents);
            Assert.Equal("ACUCAR REFINADO 1KG", items[1].Description);
            Assert.Equal(1647, items[1].TotalCents);
        }

        [Fact]
        public void Le_itens_de_nfce_em_colunas_sem_o_x()
        {
            var text = """
                CODIGO        DESCRICAO                 QTDE UN VL.UNIT VL.TOTAL
                7891024135020 COLGATE TOTAL 12 WHITENING 90G
                2,000 UN     13,99     27,98
                7898921319089 LINGUICA VEBER PERNIL FININHA 5006
                1,000 UN     18,99     18,99
                7696921319010 LINGUICA VEBER PERNIL 7006
                1,000 UM     22,99     22,99
                DESCONTO ITEM                          -7,00       :       15,99
                TB93595343163 BANHA JANE I1KG PCT — 2,000 UN     12,99     25,98
                Qtde. total de itens                                         4
                Valor total R$                                           95,94
                """;

            var items = parser.ParseItems(text);

            Assert.Equal(4, items.Count);
            Assert.Equal("7891024135020", items[0].Code);
            Assert.Equal("COLGATE TOTAL 12 WHITENING 90G", items[0].Description);
            Assert.Equal(2m, items[0].Quantity);
            Assert.Equal(1399, items[0].UnitPriceCents);
            Assert.Equal(2798, items[0].TotalCents);
            Assert.Equal("UN", items[2].Unit);
            Assert.Equal(1599, items[2].TotalCents);
            Assert.Equal("TB93595343163 BANHA JANE I1KG PCT", items[3].Description);
            Assert.Equal(2598, items[3].TotalCents);
            Assert.All(items, i => Assert.False(i.TotalComputed));
        }

        [Fact]
        public void Sem_o_x_e_com_quantidade_inteira_exige_o_total_da_linha()
        {
            Assert.Empty(parser.ParseItems("""
                COLGATE TOTAL 12 WHITENING 90G
                2 UN     13,99
                """));
        }

        [Fact]
        public void Quantidade_com_tres_casas_dispensa_o_total_e_aceita_dois_pontos_no_preco()
        {
            var items = parser.ParseItems("""
                7891024135020 COLGATE TOTAL 12 WHITENING 90G
                2,000 UN     13:99      d
                """);

            var item = Assert.Single(items);
            Assert.Equal(1399, item.UnitPriceCents);
            Assert.Equal(2798, item.TotalCents);
            Assert.True(item.TotalComputed);
        }

        [Fact]
        public void Calcula_total_quando_o_ocr_nao_leu_o_valor_do_item()
        {
            var items = parser.ParseItems("001 PAO FRANCES 0,350 KG X 15,90");

            var item = Assert.Single(items);
            Assert.Null(item.Code);
            Assert.Equal(557, item.TotalCents);
        }

        [Fact]
        public void Comprovante_sem_itens_retorna_vazio()
        {
            var text = """
                COMPROVANTE DE PAGAMENTO PIX
                10/10/2026 14:32
                VALOR R$ 150,00
                """;

            Assert.Empty(parser.ParseItems(text));
            Assert.Empty(parser.ParseItems(""));
        }
    }
}
