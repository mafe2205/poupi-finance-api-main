namespace AppFinancesClassLibrary.Application.Documents.Entities.Results
{
    /// Trecho do texto do OCR: linha (contando só as linhas não vazias), posição e tamanho dentro dela.
    public sealed record TextSpan(int Line, int Start, int Length);
}
