using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AppFinancesClassLibrary.Infrastructure.Ocr
{
    public sealed record OcrTextRegion(string Text, float Score, int Left, int Top, int Width, int Height, double Angle = 0,
        IReadOnlyList<float> CharScores = null)
    {
        public int Right => Left + Width;
        public double CenterX => Left + Width / 2.0;
        public double CenterY => Top + Height / 2.0;

        public (string Text, float[] Scores) Trimmed()
        {
            var start = Text.Length - Text.TrimStart().Length;
            var text = Text.Trim();
            var fallback = float.IsNaN(Score) ? 0f : Score;
            var scores = Enumerable.Range(start, text.Length)
                .Select(i => CharScores is not null && CharScores.Count == Text.Length && !float.IsNaN(CharScores[i]) ? CharScores[i] : fallback)
                .ToArray();
            return (text, scores);
        }
    }

    public sealed record OcrText(string Text, IReadOnlyList<float[]> LineConfidences);

    public static class PaddleOcrText
    {
        private const double ColumnGapChars = 2;
        private static readonly Regex DecimalPointRegex = new(
            @"(?<![\d.,])(\d+)\.(\d{2})(?![\d.,/:\-])", RegexOptions.Compiled);
        private static readonly Regex QuantityPointRegex = new(
            @"(?<![\d.,])(\d+)\.(\d{3})(?=\s*(?:UN|UM|KG|LT|ML|PCT|PC|CX|G|L)\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const double MinAspectForSlope = 3;
        private const double MaxSlopeDegrees = 10;

        public static string BuildText(IEnumerable<OcrTextRegion> regions) => Build(regions).Text;

        public static OcrText Build(IEnumerable<OcrTextRegion> regions)
        {
            var read = regions.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();

            var slope = Math.Tan(TextSlopeDegrees(read) * Math.PI / 180);
            double Height(OcrTextRegion r) => r.CenterY - slope * r.CenterX;

            var lines = new List<List<OcrTextRegion>>();
            foreach (var region in read.OrderBy(Height))
            {
                var line = lines.LastOrDefault();
                if (line is not null && Math.Abs(Height(region) - line.Average(Height)) <= Math.Min(region.Height, line.Min(r => r.Height)) / 2.0)
                    line.Add(region);
                else
                    lines.Add([region]);
            }

            var joined = lines.Select(JoinLine).ToList();
            return new OcrText(string.Join('\n', joined.Select(l => l.Text)), joined.Select(l => l.Scores).ToList());
        }

        public static double TextSlopeDegrees(IReadOnlyCollection<OcrTextRegion> regions)
        {
            var reliable = regions
                .Where(r => r.Width >= MinAspectForSlope * r.Height && Math.Abs(r.Angle) <= MaxSlopeDegrees)
                .OrderBy(r => r.Angle)
                .ToList();
            if (reliable.Count == 0) return 0;

            var half = reliable.Sum(r => r.Width) / 2.0;
            var cumulative = 0.0;
            foreach (var region in reliable)
            {
                cumulative += region.Width;
                if (cumulative >= half) return region.Angle;
            }

            return 0;
        }

        public static decimal Confidence(IReadOnlyCollection<OcrTextRegion> regions)
        {
            var read = regions.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
            var chars = read.Sum(r => r.Text.Trim().Length);
            if (chars == 0) return 0m;

            var weighted = read.Sum(r => (float.IsNaN(r.Score) ? 0d : r.Score) * r.Text.Trim().Length);
            return Math.Clamp((decimal)(weighted / chars), 0m, 1m);
        }

        public static string Normalize(string line)
        {
            line = line.Replace('×', 'x');
            line = QuantityPointRegex.Replace(line, "$1,$2");
            return DecimalPointRegex.Replace(line, "$1,$2");
        }

        private static (string Text, float[] Scores) JoinLine(List<OcrTextRegion> line)
        {
            var text = new StringBuilder();
            var scores = new List<float>();
            OcrTextRegion previous = null;
            foreach (var region in line.OrderBy(r => r.Left))
            {
                var (regionText, regionScores) = region.Trimmed();
                if (previous is not null)
                {
                    var charWidth = (double)previous.Width / Math.Max(1, previous.Text.Trim().Length);
                    var separator = region.Left - previous.Right > ColumnGapChars * charWidth ? "   " : " ";
                    text.Append(separator);
                    scores.AddRange(Enumerable.Repeat(1f, separator.Length));
                }

                text.Append(regionText);
                scores.AddRange(regionScores);
                previous = region;
            }

            return (Normalize(text.ToString()), scores.ToArray());
        }
    }
}
