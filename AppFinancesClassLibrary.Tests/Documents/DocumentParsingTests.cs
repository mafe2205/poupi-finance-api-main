using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Tests.Documents
{
    public class DocumentParsingTests
    {
        private readonly DocumentParsingService parser = new();

        [Theory]
        [InlineData("1.234,56", 123456)]
        [InlineData("1234,56", 123456)]
        [InlineData("R$ 1.234,56", 123456)]
        [InlineData("R$1.234,56", 123456)]
        [InlineData("1.234,5", 123450)]
        [InlineData("0,99", 99)]
        [InlineData("39,900", 3990)]
        public void Converte_valor_brasileiro_em_centavos(string value, int expectedCents)
        {
            Assert.Equal(expectedCents, BrazilianMoney.ParseCents(value));
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1,234.56")]
        [InlineData("12.34,56")]
        public void Valor_invalido_retorna_null(string value)
        {
            Assert.Null(BrazilianMoney.ParseCents(value));
        }

        [Fact]
        public void Sem_linha_de_total_nao_inventa_valor()
        {
            var parsed = parser.Parse("""
                PADARIA PAO QUENTE
                10/10/2026
                CAFE COM LEITE 7,50
                PAO DE QUEIJO 6,00
                """, []);

            Assert.Null(parsed.PaymentValueCents);
        }

        [Fact]
        public void Valor_zero_fica_vazio()
        {
            var parsed = parser.Parse("""
                Comprovante de Transferencia
                Valor   R$00,00
                Dados do recebedor
                """, []);

            Assert.Null(parsed.PaymentValueCents);
        }

        [Fact]
        public void Le_valor_de_comprovante_pix()
        {
            var parsed = parser.Parse("""
                COMPROVANTE DE PAGAMENTO PIX
                10/10/2026 14:32
                Valor: R$ 150,00
                """, []);

            Assert.Equal(15000, parsed.PaymentValueCents);
        }

        [Fact]
        public void Le_total_na_linha_seguinte_ao_rotulo()
        {
            var parsed = parser.Parse("""
                MERCADO CENTRAL
                VALOR TOTAL R$
                75,39
                TROCO 4,61
                """, []);

            Assert.Equal(7539, parsed.PaymentValueCents);
        }

        [Fact]
        public void Valor_a_pagar_ilegivel_nao_e_trocado_pelo_valor_total()
        {
            var parsed = parser.Parse("""
                Valor total R$   513,03
                Descontos R$   101,60
                Valor a Pagar R$   411 43
                FORMA DE PAGAMENTO   VALOR PAGO R$
                Dinheiro   412,00
                """, []);

            Assert.Null(parsed.PaymentValueCents);
        }

        [Fact]
        public void Valor_a_pagar_prevalece_sobre_o_valor_total_com_desconto()
        {
            var parsed = parser.Parse("""
                RECH E SILVA SUPERMERCADOS LTDA
                7896921319010 LINGUICA WEBER PERNIL 700G
                1,000 UN     22,99     22,99
                DESCONTO ITEM          -7,00   =   15,99
                Valor total R$                     22,99
                Desconto de Itens                  -7,00
                Valor a Pagar R$                   15,99
                """, []);

            Assert.Equal(1599, parsed.PaymentValueCents);
            Assert.Equal(1599, parsed.ItemsTotalCents);
            Assert.False(parsed.ItemsTotalDivergence);
        }

        [Fact]
        public void Soma_dos_itens_diferente_do_total_sinaliza_divergencia()
        {
            var parsed = parser.Parse("""
                SUPERMERCADO BOM PRECO
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 40,00
                """, []);

            Assert.Equal(3288, parsed.ItemsTotalCents);
            Assert.True(parsed.ItemsTotalDivergence);
        }

        [Fact]
        public void Soma_dos_itens_igual_ao_total_nao_sinaliza()
        {
            var parsed = parser.Parse("""
                SUPERMERCADO BOM PRECO
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 32,88
                """, []);

            Assert.False(parsed.ItemsTotalDivergence);
        }

        [Fact]
        public void Totais_de_linha_lidos_nao_tem_tolerancia()
        {
            var parsed = parser.Parse("""
                SUPERMERCADO BOM PRECO
                001 7891000100103 LEITE INTEGRAL 1L 2 UN X 4,99 9,98
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 32,89
                """, []);

            Assert.All(parsed.Items, item => Assert.False(item.TotalComputed));
            Assert.True(parsed.ItemsTotalDivergence);
        }

        [Fact]
        public void Total_de_linha_calculado_tolera_um_centavo_de_arredondamento()
        {
            var parsed = parser.Parse("""
                SUPERMERCADO BOM PRECO
                001 2000000000017 QUEIJO MUSSARELA 0,347 KG X 19,99
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 29,83
                """, []);

            Assert.True(parsed.Items[0].TotalComputed);
            Assert.False(parsed.Items[1].TotalComputed);
            Assert.Equal(2984, parsed.ItemsTotalCents);
            Assert.False(parsed.ItemsTotalDivergence);
        }

        [Fact]
        public void Total_de_linha_calculado_nao_tolera_mais_que_um_centavo()
        {
            var parsed = parser.Parse("""
                SUPERMERCADO BOM PRECO
                001 2000000000017 QUEIJO MUSSARELA 0,347 KG X 19,99
                002 7896005800010 ARROZ TIPO 1 5KG 1 UN X 22,90 22,90
                TOTAL R$ 29,82
                """, []);

            Assert.True(parsed.ItemsTotalDivergence);
        }

        [Fact]
        public void Documento_sem_texto_vai_para_revisao_sem_valor()
        {
            var reasons = DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "", 0m, 0.8m, null);

            Assert.Contains(DocumentReviewReason.texto_ilegivel, reasons);
            Assert.Contains(DocumentReviewReason.valor_nao_encontrado, reasons);
        }

        [Fact]
        public void Falha_no_ocr_vai_para_revisao_e_pendente_ainda_nao()
        {
            Assert.Equal([DocumentReviewReason.falha_no_ocr], DocumentReviewPolicy.Evaluate(StatusOCRTypes.erro, "", 0m, 0.8m, null));
            Assert.Empty(DocumentReviewPolicy.Evaluate(StatusOCRTypes.pendente, "", 0m, 0.8m, null));
        }

        [Fact]
        public void Baixa_confianca_vai_para_revisao()
        {
            var parsed = new ParsedDocumentResult { Description = "LOJA", PaymentValueCents = 1000, CategoryId = 1, CategoryRequiresConfirmation = false };

            Assert.Contains(DocumentReviewReason.baixa_confianca,
                DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.55m, 0.8m, parsed));
            Assert.Empty(DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.92m, 0.8m, parsed));
        }

        [Fact]
        public void Digito_do_valor_com_baixa_confianca_vai_para_revisao()
        {
            var parsed = new ParsedDocumentResult { Description = "LOJA", PaymentValueCents = 7683, CategoryId = 1, CategoryRequiresConfirmation = false };

            Assert.Equal([DocumentReviewReason.baixa_confianca_valor],
                DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.99m, 0.8m, parsed, 0.68m, 0.9m));
            Assert.Empty(DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.99m, 0.8m, parsed, 0.97m, 0.9m));
            Assert.Empty(DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.99m, 0.8m, parsed, null, 0.9m));
        }

        [Fact]
        public void Sem_estabelecimento_vai_para_revisao_mesmo_com_a_categoria_decidida()
        {
            var parsed = parser.Parse("""
                SICOOB
                Via do Cliente
                CNPJ: 04.975.903/0001-49   AUT: 573825
                VALOR COMPRA: R$ 200,00
                """, [new() { Id = 1, Name = "Gasolina" }], establishmentCnae: 4731800);

            Assert.Null(parsed.Description);
            Assert.False(parsed.CategoryRequiresConfirmation);
            Assert.Equal([DocumentReviewReason.estabelecimento_nao_encontrado],
                DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.99m, 0.8m, parsed));
        }

        // ---------- Estabelecimento que o OCR não leu ----------

        private const string MaquininhaSemNome = """
            SICOOB
            Via do Cliente
            CNPJ: 04.975.903/0001-49   AUT: 573825
            VALOR COMPRA: R$ 200,00
            """;

        [Fact]
        public void Sem_nome_lido_usa_o_nome_do_cadastro_do_cnpj()
        {
            var parsed = parser.Parse(MaquininhaSemNome, [], establishmentRegistryName: "POSTO PELLEGRIN III");

            Assert.Equal("POSTO PELLEGRIN III", parsed.Description);
            Assert.Equal(DocumentDescriptionSource.cadastro_cnpj, parsed.DescriptionSource);
            Assert.DoesNotContain(DocumentReviewReason.estabelecimento_nao_encontrado,
                DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.99m, 0.8m, parsed));
        }

        [Fact]
        public void Nome_confirmado_pelo_usuario_vale_mais_que_o_do_cadastro()
        {
            var history = new CategoryHistory([
                new CategoryHistoryEntry { EstablishmentKey = "04975903000149", ConfirmedDescription = "Posto do Trevo", CategoryId = 1 },
                new CategoryHistoryEntry { EstablishmentKey = "04975903000149", ConfirmedDescription = "Posto Pellegrin", CategoryId = 1 },
            ]);

            var parsed = parser.Parse(MaquininhaSemNome, [], history, establishmentRegistryName: "POSTO PELLEGRIN III");

            Assert.Equal("Posto Pellegrin", parsed.Description);
            Assert.Equal(DocumentDescriptionSource.historico_usuario, parsed.DescriptionSource);
        }

        [Fact]
        public void Nome_lido_pelo_ocr_nunca_e_trocado()
        {
            var history = new CategoryHistory([
                new CategoryHistoryEntry { EstablishmentKey = "03500070000105", ConfirmedDescription = "Rech", CategoryId = 1 }]);

            var parsed = parser.Parse("RECH E SILVA SUPERMERCADOS LTDA\nCNPJ: 03.500.070/0001-05\nTOTAL R$ 50,00", [], history,
                establishmentRegistryName: "COLONIAL SUPERMERCADOS");

            Assert.Equal("RECH E SILVA SUPERMERCADOS LTDA", parsed.Description);
            Assert.Equal(DocumentDescriptionSource.ocr, parsed.DescriptionSource);
        }

        [Fact]
        public void Pix_para_pessoa_sem_nome_lido_usa_so_o_historico()
        {
            const string pix = """
                Dados do recebedor
                Instituição   COOP SICREDI SUL SC
                Nome   ###
                CPF/CNPJ   ***.111.222-**
                Dados do pagador
                Nome   CLIENTE TESTE
                """;
            var history = new CategoryHistory([
                new CategoryHistoryEntry { EstablishmentKey = "cpf:111222", ConfirmedDescription = "Joao (frete)", CategoryId = 1 }]);

            Assert.Equal("Joao (frete)", parser.Parse(pix, [], history).Description);
            Assert.Null(parser.Parse(pix, [], establishmentRegistryName: "QUALQUER").Description);
        }

        [Fact]
        public void Sem_loja_identificada_o_nome_continua_vazio()
        {
            var parsed = parser.Parse("SICOOB\nVia do Cliente\nVALOR COMPRA: R$ 200,00", [], establishmentRegistryName: "POSTO");

            Assert.Null(parsed.Description);
            Assert.Null(parsed.DescriptionSource);
        }

        [Theory]
        [InlineData("PANVEL FARMACIAS", "DIMED S/A", "PANVEL FARMACIAS")]
        [InlineData("", "CLINICA PERPETUE LTDA", "CLINICA PERPETUE LTDA")]
        [InlineData(null, null, null)]
        public void Nome_do_cadastro_e_o_fantasia_ou_a_razao_social(string tradeName, string legalName, string expected)
        {
            Assert.Equal(expected, new CnpjActivity("92665611064574", 4771701, null, tradeName, legalName).RegistryName);
        }

        [Fact]
        public void Sem_valor_a_confianca_do_valor_nao_conta()
        {
            var parsed = new ParsedDocumentResult { Description = "LOJA", CategoryId = 1, CategoryRequiresConfirmation = false };

            Assert.Equal([DocumentReviewReason.valor_nao_encontrado],
                DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "texto", 0.99m, 0.8m, parsed, 0.5m, 0.9m));
        }

        [Fact]
        public void Informa_onde_o_valor_foi_lido()
        {
            var parsed = parser.Parse("""
                COMPROVANTE PIX

                Valor: R$ 150,00
                Data 10/10/2026
                """, []);

            Assert.Equal(new TextSpan(1, 10, 6), parsed.PaymentValueSpan);
        }

        [Fact]
        public void Valor_na_linha_de_baixo_aponta_para_ela()
        {
            var parsed = parser.Parse("""
                VALOR TOTAL R$
                75,39
                """, []);

            Assert.Equal(new TextSpan(1, 0, 5), parsed.PaymentValueSpan);
        }

        [Fact]
        public void Confianca_dos_digitos_de_um_trecho()
        {
            var ocr = new OcrExtractionResult
            {
                Text = "LOJA\nTOTAL R$ 76,83",
                LineConfidences = [[1f, 1f, 1f, 1f], [1f, 1f, 1f, 1f, 1f, 1f, 0.2f, 0.3f, 1f, 0.99f, 0.68f, 0.1f, 0.97f, 0.98f]]
            };

            Assert.Equal(0.68m, ocr.DigitConfidence(new TextSpan(1, 9, 5)));
        }

        [Fact]
        public void Sem_confianca_por_caractere_nao_ha_confianca_do_trecho()
        {
            Assert.Null(new OcrExtractionResult { Text = "TOTAL 10,00" }.DigitConfidence(new TextSpan(0, 6, 5)));
            Assert.Null(new OcrExtractionResult { Text = "TOTAL 10,00", LineConfidences = [[1f, 1f]] }.DigitConfidence(new TextSpan(0, 6, 5)));
            Assert.Null(new OcrExtractionResult { Text = "TOTAL 10,00", LineConfidences = [[1f]] }.DigitConfidence(null));
        }

        [Fact]
        public void Categoria_ambigua_no_comprovante_pede_confirmacao()
        {
            IEnumerable<CategoryResult> categories = [ new() { Id = 1, Name = "Transporte" }, new() { Id = 2, Name = "Saúde" } ];

            var parsed = parser.Parse("""
                POSTO SAO JOSE
                TOTAL R$ 50,00
                """, categories);

            Assert.Null(parsed.CategoryId);
            Assert.True(parsed.CategoryRequiresConfirmation);
            Assert.Contains(DocumentReviewReason.categoria_requer_confirmacao,
                DocumentReviewPolicy.Evaluate(StatusOCRTypes.processado, "POSTO SAO JOSE", 0.95m, 0.8m, parsed));
        }

        [Theory]
        [InlineData("Valor Pago:                         R$ 7 5,83")]
        [InlineData("VALOR IDENTIFICADO.: R$ * 200,00")]
        [InlineData("VALOR INFORMADO....: R$ 1.8650,00")]
        public void Valor_partido_pelo_ocr_fica_vazio(string line)
        {
            var parsed = parser.Parse($"COMPROVANTE\n{line}\n", []);

            Assert.Null(parsed.PaymentValueCents);
        }

        [Fact]
        public void Quantidade_do_item_abaixo_do_cabecalho_nao_vira_total()
        {
            var parsed = parser.Parse("""
                0DTGO        DESCRICÇAO                 OTDE UN VL.UNIT  UL.TOTAL
                997163500149 PAO FARIAS FORMA 5006 1,000 UM      7,89      7,89
                Valor a Pagar R$           :              /
                """, []);

            Assert.Null(parsed.PaymentValueCents);
        }

        [Fact]
        public void Tributos_com_acento_trocado_pelo_ocr_nao_viram_total()
        {
            var parsed = parser.Parse("""
                10TALCRS)                                                  369,00
                (9,74L) Estadudls. h$ 0,00 (0,004) Municipais, Total Tríbutos:
                """, []);

            Assert.Null(parsed.PaymentValueCents);
        }

        [Fact]
        public void Pix_usa_o_nome_do_recebedor_quebrado_em_varias_linhas()
        {
            var parsed = parser.Parse("""
                Pix realizado com sucesso!
                Valor                                              R$ 450,00
                Data/hora                           05/10/2026 12:45:04
                CC UNICRED CENTRO-
                Instituição                        SUL LTDA - UNICRED
                CENTRO-SUL
                AM ORGANIZACOES E
                Nome                                   PROMOCOES DE
                EVENTOS LTDA
                CPF/CNPJ                                       06.289.626/0001-46
                Dados do pagador
                Instituição                        UNICRED
                Nome                                CLIENTE TESTE
                """, []);

            Assert.Equal("AM ORGANIZACOES E PROMOCOES DE EVENTOS LTDA", parsed.Description);
        }

        [Fact]
        public void Pix_sem_recebedor_legivel_nao_usa_o_pagador_nem_o_banco()
        {
            var parsed = parser.Parse("""
                UNICRED
                Pix realizado com sucesso!
                Valor                                              R$ 450,00
                Dados do pagador
                Instituição                        UNICRED
                Nome                                CLIENTE TESTE
                """, []);

            Assert.Null(parsed.Description);
        }

        [Fact]
        public void Boleto_usa_o_nome_fantasia_do_beneficiario()
        {
            var parsed = parser.Parse("""
                08/05/2026                          19:49:59
                UNICRED CENTRO-SUL
                Comprovante de Pagamento de Titulo
                Instituição Emissora:       UNICRED DO BRASIL
                Beneficiário
                Nome Fantasia:              UNICRED DO BRASIL
                Pagador
                Nome:          CLIENTE DE TESTE DA SILVA
                Valor Pago:                         R$ 305,60
                """, []);

            Assert.Equal("UNICRED DO BRASIL", parsed.Description);
            Assert.Equal(30560, parsed.PaymentValueCents);
        }

        [Theory]
        [InlineData("CNPJ: 76.816.842/0003-40 POSTO LEAO CITY", "POSTO LEAO CITY")]
        [InlineData("VIA - CLIENTE\n- JOSE OLIVEIRA", "JOSE OLIVEIRA")]
        [InlineData("fA r'?â\"«;xwr VNS \" ;Q Ã\nREAL SUPERMERCADO", "REAL SUPERMERCADO")]
        public void Estabelecimento_do_cabecalho_sem_cnpj_via_ou_lixo(string header, string expected)
        {
            var parsed = parser.Parse(header + "\nTOTAL R$ 50,00", []);

            Assert.Equal(expected, parsed.Description);
        }

        [Theory]
        [InlineData("Panvel - Filial 873\nCNPJ: 92.665.611/0645-74", "92665611064574")]
        [InlineData("ibis\nCNP: 80732928000604", "80732928000604")]
        [InlineData("CMPJ do Enitente 92 016 757/0055-84 COMERCIAL ZAFFARI", "92016757005584")]
        [InlineData("Panvel\nCNPJ: 92.665.611/0645-75", null)]
        [InlineData("Panvel\nCNPJ: 00.000.000/0000-00", null)]
        public void Le_o_cnpj_do_cabecalho(string header, string expected)
        {
            var parsed = parser.Parse(header + "\nTOTAL R$ 50,00", []);

            Assert.Equal(expected, parsed.Cnpj);
        }

        [Fact]
        public void Pix_para_empresa_usa_o_cnpj_do_recebedor()
        {
            var parsed = parser.Parse("""
                Pix realizado com sucesso!
                Dados do recebedor
                Instituição   CLOUDWALK IP LTDA
                Nome   CLINICA PERPETUE
                CPF/CNPJ   62.975.285/0001-33
                Dados do pagador
                Instituição   SUL LTDA - UNICRED
                Nome   CLIENTE TESTE
                CPF/CNPJ   ***.555.666-**
                """, []);

            Assert.Equal("62975285000133", parsed.Cnpj);
            Assert.Equal("62975285000133", parsed.EstablishmentKey);
        }

        [Theory]
        [InlineData("CPF/CNPJ   ***111.222-**", "cpf:111222")]
        [InlineData("CPF/CNPJ   ***.333.444**", "cpf:333444")]
        [InlineData("CPF/CNPJ   123.111.222-45", "cpf:111222")]
        [InlineData("CPF/CNPJ   ***.79?.609-**", null)]
        public void Pix_para_pessoa_usa_os_digitos_visiveis_do_cpf_so_no_historico(string documentLine, string expectedKey)
        {
            var parsed = parser.Parse($"""
                Dados do recebedor
                Instituição   COOP SICREDI SUL SC
                Nome   JOAO SILVA SANTOS
                {documentLine}
                Dados do pagador
                Nome   CLIENTE TESTE
                CPF/CNPJ   ***.555.666-**
                """, []);

            Assert.Null(parsed.Cnpj);
            Assert.Equal(expectedKey, parsed.EstablishmentKey);
        }

        [Fact]
        public void Cpf_do_pagador_nunca_identifica_o_recebedor()
        {
            var parsed = parser.Parse("""
                Dados do pagador
                Instituição   SUL LTDA - UNICRED
                Nome   CLIENTE TESTE
                CPF/CNPJ   ***.555.666-**
                """, []);

            Assert.Null(parsed.EstablishmentKey);
        }

        [Theory]
        [InlineData("SICOOB\nVia do Cliente\nSTO PELLEGRIN III", "STO PELLEGRIN III")]
        [InlineData("VIA CLENTE   stone\ns ROSAMISTICA   stone", "s ROSAMISTICA")]
        [InlineData("CIELO\nSUPERMERCADO BOM PRECO", "SUPERMERCADO BOM PRECO")]
        public void Nome_da_maquininha_nao_e_o_estabelecimento(string header, string expected)
        {
            var parsed = parser.Parse(header + "\nTOTAL R$ 50,00", []);

            Assert.Equal(expected, parsed.Description);
        }

        [Fact]
        public void Cabecalho_ilegivel_deixa_o_estabelecimento_vazio()
        {
            var parsed = parser.Parse("""
                SSN
                VALOR IDENTIFICADO.: R$ * 200,00
                COMPOSICAO DO DEPOSITO
                """, []);

            Assert.Null(parsed.Description);
        }
    }
}
