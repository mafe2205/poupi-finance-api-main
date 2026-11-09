using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Options;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using System.Collections.Concurrent;

namespace AppFinancesClassLibrary.Infrastructure.Ocr
{
    public sealed class PaddleOcrEngine : IDisposable
    {
        private readonly BlockingCollection<(byte[] Image, TaskCompletionSource<OcrExtractionResult> Result)> jobs = new();
        private readonly Thread worker;

        public PaddleOcrEngine(IOptions<OcrOptions> ocrOptions)
        {
            var detectionMaxSize = ocrOptions.Value.PaddleDetectionMaxSize;
            worker = new Thread(() => Work(detectionMaxSize)) { IsBackground = true, Name = "PaddleOCR" };
            worker.Start();
        }

        public Task<OcrExtractionResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            var result = new TaskCompletionSource<OcrExtractionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            jobs.Add((imageBytes, result), cancellationToken);
            return result.Task.WaitAsync(cancellationToken);
        }

        private void Work(int detectionMaxSize)
        {
            PaddleOcrAll engine = null;
            Exception loadError = null;
            try
            {
                engine = new PaddleOcrAll(LocalFullModels.LatinV5, PaddleDevice.Mkldnn())
                {
                    AllowRotateDetection = true,
                    Enable180Classification = false
                };
                engine.Detector.MaxSize = detectionMaxSize;
            }
            catch (Exception ex)
            {
                loadError = ex;
            }

            using (engine)
            {
                foreach (var (image, result) in jobs.GetConsumingEnumerable())
                {
                    if (loadError is not null)
                    {
                        result.SetException(new InvalidOperationException("Falha ao carregar o modelo do PaddleOCR.", loadError));
                        continue;
                    }

                    try { result.SetResult(Extract(engine, image)); }
                    catch (Exception ex) { result.SetException(ex); }
                }
            }
        }

        private static OcrExtractionResult Extract(PaddleOcrAll engine, byte[] imageBytes)
        {
            using var image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (image.Empty()) throw new InvalidOperationException("Imagem inválida para o OCR.");

            var regions = engine.Run(image).Regions
                .Select(r =>
                {
                    var box = r.Rect.BoundingRect();
                    return new OcrTextRegion(r.Text ?? string.Empty, r.Score, box.X, box.Y, box.Width, box.Height, LongSideAngle(r.Rect),
                        r.Chars?.Select(c => c.Score).ToList());
                })
                .ToList();

            var text = PaddleOcrText.Build(regions);
            return new OcrExtractionResult
            {
                Text = text.Text,
                TrustOcr = PaddleOcrText.Confidence(regions),
                LineConfidences = text.LineConfidences
            };
        }

        private static double LongSideAngle(RotatedRect rect)
        {
            double angle = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle - 90;
            while (angle > 90) angle -= 180;
            while (angle <= -90) angle += 180;
            return angle;
        }

        public void Dispose()
        {
            jobs.CompleteAdding();
            worker.Join();
            jobs.Dispose();
        }
    }
}
