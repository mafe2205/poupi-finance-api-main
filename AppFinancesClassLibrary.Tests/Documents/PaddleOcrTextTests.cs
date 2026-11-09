using AppFinancesClassLibrary.Infrastructure.Ocr;

namespace AppFinancesClassLibrary.Tests.Documents
{
    public class PaddleOcrTextTests
    {
        [Fact]
        public void Junta_na_mesma_linha_os_trechos_lado_a_lado_e_ordena_pela_esquerda()
        {
            var text = PaddleOcrText.BuildText(
            [
                new("REFEICAO", 0.99f, 300, 468, 113, 36),
                new("001 1", 0.97f, 50, 470, 78, 38),
                new("0,628 KG x 159,90", 0.96f, 235, 502, 246, 35),
                new("100,42", 1f, 610, 500, 85, 36)
            ]);

            Assert.Equal("001 1   REFEICAO\n0,628 KG x 159,90   100,42", text);
        }

        [Fact]
        public void Compensa_a_inclinacao_da_foto_ao_juntar_rotulo_e_valor()
        {
            var text = PaddleOcrText.BuildText(
            [
                new("Qtde. total de itens", 1f, 630, 1534, 600, 53, -1.3),
                new("513.03", 1f, 1643, 1553, 140, 49),
                new("Valor total R$", 1f, 631, 1579, 420, 55, -2.2),
                new("101.60", 1f, 1644, 1598, 140, 47),
                new("Descontos R$", 1f, 632, 1626, 380, 53, -1.4),
                new("411 43", 1f, 1644, 1646, 140, 45),
                new("Valor a Pagar R$", 1f, 635, 1670, 470, 56, -1.7)
            ]);

            Assert.Equal("Qtde. total de itens\nValor total R$   513,03\nDescontos R$   101,60\nValor a Pagar R$   411 43", text);
        }

        [Fact]
        public void Confianca_de_cada_caractere_acompanha_o_texto_montado()
        {
            var text = PaddleOcrText.Build(
            [
                new("TOTAL", 1f, 0, 0, 50, 20, CharScores: [0.9f, 0.9f, 0.9f, 0.9f, 0.9f]),
                new(" 76.83", 1f, 200, 0, 60, 20, CharScores: [0.5f, 0.99f, 0.68f, 0.97f, 0.98f, 0.96f])
            ]);

            Assert.Equal("TOTAL   76,83", text.Text);
            Assert.Equal([0.9f, 0.9f, 0.9f, 0.9f, 0.9f, 1f, 1f, 1f, 0.99f, 0.68f, 0.97f, 0.98f, 0.96f], Assert.Single(text.LineConfidences));
        }

        [Fact]
        public void Sem_confianca_por_caractere_usa_a_do_trecho()
        {
            var text = PaddleOcrText.Build([new("R$ 10,00", 0.8f, 0, 0, 80, 20)]);

            Assert.All(Assert.Single(text.LineConfidences), score => Assert.Equal(0.8f, score));
        }

        [Fact]
        public void Sem_trechos_compridos_nao_ha_inclinacao()
        {
            Assert.Equal(0, PaddleOcrText.TextSlopeDegrees([new("4", 1f, 0, 0, 20, 40, 30), new("0,57", 1f, 100, 0, 60, 40, -5)]));
        }

        [Fact]
        public void Separa_com_um_espaco_os_trechos_proximos()
        {
            var text = PaddleOcrText.BuildText([new("VALOR", 1f, 0, 0, 50, 20), new("TOTAL", 1f, 60, 0, 50, 20)]);

            Assert.Equal("VALOR TOTAL", text);
        }

        [Fact]
        public void Ignora_trechos_vazios()
        {
            Assert.Equal("TOTAL", PaddleOcrText.BuildText([new(" ", 0.5f, 0, 0, 10, 10), new("TOTAL", 1f, 20, 0, 50, 10)]));
        }

        [Theory]
        [InlineData("VALOR TOTAL R$ 107.42", "VALOR TOTAL R$ 107,42")]
        [InlineData("1 UN 12.99 12.99", "1 UN 12,99 12,99")]
        [InlineData("0.628 KG × 159.90", "0,628 KG x 159,90")]
        [InlineData("1.000 UN × 7.00", "1,000 UN x 7,00")]
        public void Troca_o_ponto_decimal_pela_virgula(string line, string expected)
        {
            Assert.Equal(expected, PaddleOcrText.Normalize(line));
        }

        [Theory]
        [InlineData("18.11.2025 14:32")]
        [InlineData("18.11.25")]
        [InlineData("CNPJ:92.016.757/0055-84")]
        [InlineData("1.234,56")]
        [InlineData("Qtd 1.000")]
        public void Mantem_datas_documentos_e_valores_ja_com_virgula(string line)
        {
            Assert.Equal(line, PaddleOcrText.Normalize(line));
        }

        [Fact]
        public void Confianca_e_a_media_ponderada_pelos_caracteres()
        {
            var confidence = PaddleOcrText.Confidence([new("ABCD", 1f, 0, 0, 1, 1), new("XY", 0.4f, 0, 0, 1, 1), new("Z", float.NaN, 0, 0, 1, 1)]);

            Assert.Equal((4 * 1m + 2 * 0.4m) / 7, confidence, 4);
        }

        [Fact]
        public void Sem_texto_a_confianca_e_zero()
        {
            Assert.Equal(0m, PaddleOcrText.Confidence([]));
        }
    }
}
