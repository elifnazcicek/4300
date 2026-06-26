using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace ReceiptOCR.API.Services
{
    public class ImagePreprocessingService
    {
        private readonly ILogger<ImagePreprocessingService> _logger;
        private static readonly string ProcessedDir = Path.Combine(Directory.GetCurrentDirectory(), "data");

        public ImagePreprocessingService(ILogger<ImagePreprocessingService> logger)
        {
            _logger = logger;
            Directory.CreateDirectory(ProcessedDir);
        }

        public string GetProcessedDir() => ProcessedDir;

        public async Task<PreprocessingOutput> ProcessAsync(Stream inputStream, string originalFileName)
        {
            _logger.LogInformation("Görsel ön işlemesi başlatılıyor: {Filename}", originalFileName);

            string extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";
            
            string processedFileName = $"processed_{DateTimeOffset.Now.ToUnixTimeSeconds()}{extension}";
            string processedFilePath = Path.Combine(ProcessedDir, processedFileName);

            long originalSize = inputStream.Length;
            var appliedSteps = new System.Collections.Generic.List<string>();

            try
            {
                if (inputStream.CanSeek)
                {
                    inputStream.Position = 0;
                }

                // Load image directly as L8 (Grayscale) to optimize memory and processing
                using (var image = await Image.LoadAsync<L8>(inputStream))
                {
                    // 1. Dimension Optimization (Resize if larger than 1600px)
                    int maxDimension = 1600;
                    if (image.Width > maxDimension || image.Height > maxDimension)
                    {
                        double scale = Math.Min((double)maxDimension / image.Width, (double)maxDimension / image.Height);
                        int newWidth = (int)(image.Width * scale);
                        int newHeight = (int)(image.Height * scale);
                        
                        image.Mutate(x => x.Resize(newWidth, newHeight));
                        appliedSteps.Add($"Boyut Optimizasyonu (Maks 1600px, Yeni Boyut: {newWidth}x{newHeight})");
                    }

                    // 2. Deskew (Yamuk Açı Düzleştirme)
                    double bestAngle = DetectSkewAngle(image);
                    if (Math.Abs(bestAngle) >= 0.5)
                    {
                        image.Mutate(x => x.Rotate((float)bestAngle));
                        appliedSteps.Add($"Eğiklik Düzeltme / Deskew (Açı: {bestAngle:F1} derece)");
                    }
                    else
                    {
                        appliedSteps.Add("Eğiklik Düzeltme / Deskew (Gerek görülmedi, açı < 0.5)");
                    }

                    // 3. CLAHE (Contrast Limited Adaptive Histogram Equalization)
                    ApplyClahe(image, tileX: 8, tileY: 8, clipLimit: 3.0f);
                    appliedSteps.Add("Kontrast İyileştirme (CLAHE - Grid: 8x8, Clip Limit: 3.0)");

                    // 4. Noise Reduction (Median Filter 3x3)
                    using (var denoised = ApplyMedianFilter(image))
                    {
                        // 5. Text Sharpening
                        denoised.Mutate(x => x.GaussianSharpen(0.4f));
                        appliedSteps.Add("Gürültü Temizleme (3x3 Medyan Filtresi)");
                        appliedSteps.Add("Metin Keskinleştirme (Gaussian Sharpen 0.4)");

                        // Save as JPEG with 85% quality compression
                        var encoder = new JpegEncoder
                        {
                            Quality = 85
                        };
                        await denoised.SaveAsync(processedFilePath, encoder);
                        appliedSteps.Add("JPEG Kalite Sıkıştırma (85%)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görsel işlenirken hata oluştu, orijinal dosya doğrudan kopyalanıyor.");
                
                if (inputStream.CanSeek)
                {
                    inputStream.Position = 0;
                }
                using (var fileStream = new FileStream(processedFilePath, FileMode.Create))
                {
                    await inputStream.CopyToAsync(fileStream);
                }
                appliedSteps.Add("Ham Kopyalama (Fallback)");
            }

            long processedSize = new FileInfo(processedFilePath).Length;
            double compressionRatio = Math.Round((double)originalSize / processedSize, 2);

            _logger.LogInformation("Görsel ön işleme ve disk kaydı tamamlandı: {ProcessedFileName}. Sıkıştırma Oranı: {CompressionRatio}", 
                processedFileName, compressionRatio);

            return new PreprocessingOutput
            {
                OriginalFileName = originalFileName,
                OriginalSizeKb = Math.Round((double)originalSize / 1024, 2),
                ProcessedFileName = processedFileName,
                ProcessedSizeKb = Math.Round((double)processedSize / 1024, 2),
                CompressionRatio = compressionRatio,
                AppliedSteps = appliedSteps.ToArray()
            };
        }

        private double DetectSkewAngle(Image<L8> src)
        {
            int maxDimension = 300;
            int w = src.Width;
            int h = src.Height;

            if (w > maxDimension || h > maxDimension)
            {
                double scale = Math.Min((double)maxDimension / w, (double)maxDimension / h);
                w = (int)(w * scale);
                h = (int)(h * scale);
            }

            // Create a small cloned version for fast angle analysis
            using var small = src.Clone(x => x.Resize(w, h));

            // Calculate global threshold for binarization
            long sum = 0;
            for (int y = 0; y < small.Height; y++)
            {
                for (int x = 0; x < small.Width; x++)
                {
                    sum += small[x, y].PackedValue;
                }
            }
            byte threshold = (byte)(sum / (small.Width * small.Height));

            // Apply binarization to small image
            for (int y = 0; y < small.Height; y++)
            {
                for (int x = 0; x < small.Width; x++)
                {
                    small[x, y] = new L8(small[x, y].PackedValue < threshold ? (byte)0 : (byte)255);
                }
            }

            double bestAngle = 0;
            double maxVariance = -1;

            // Search between -10 to +10 degrees in steps of 1 degree
            for (double angle = -10; angle <= 10; angle += 1.0)
            {
                using var rotated = small.Clone(x => x.Rotate((float)angle));
                
                int rW = rotated.Width;
                int rH = rotated.Height;
                long[] rowSums = new long[rH];

                for (int y = 0; y < rH; y++)
                {
                    long rSum = 0;
                    for (int x = 0; x < rW; x++)
                    {
                        rSum += rotated[x, y].PackedValue;
                    }
                    rowSums[y] = rSum;
                }

                // Calculate projection profile variance
                double avg = rowSums.Average();
                double variance = rowSums.Select(val => (val - avg) * (val - avg)).Average();

                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    bestAngle = angle;
                }
            }

            return bestAngle;
        }

        private void ApplyClahe(Image<L8> image, int tileX, int tileY, float clipLimit)
        {
            int width = image.Width;
            int height = image.Height;

            int tileWidth = width / tileX;
            int tileHeight = height / tileY;

            if (tileWidth < 2 || tileHeight < 2) return;

            // 1. Calculate histograms for each tile
            int[,,] histograms = new int[tileY, tileX, 256];
            int totalTilePixels = tileWidth * tileHeight;
            float averageBinCount = totalTilePixels / 256.0f;
            int actualClipLimit = (int)Math.Max(1.0f, clipLimit * averageBinCount);

            for (int ty = 0; ty < tileY; ty++)
            {
                for (int tx = 0; tx < tileX; tx++)
                {
                    int yStart = ty * tileHeight;
                    int yEnd = Math.Min(yStart + tileHeight, height);
                    int xStart = tx * tileWidth;
                    int xEnd = Math.Min(xStart + tileWidth, width);

                    for (int y = yStart; y < yEnd; y++)
                    {
                        for (int x = xStart; x < xEnd; x++)
                        {
                            byte val = image[x, y].PackedValue;
                            histograms[ty, tx, val]++;
                        }
                    }
                }
            }

            // 2. Clip histograms and redistribute clipped pixels
            for (int ty = 0; ty < tileY; ty++)
            {
                for (int tx = 0; tx < tileX; tx++)
                {
                    int clippedSum = 0;
                    for (int bin = 0; bin < 256; bin++)
                    {
                        if (histograms[ty, tx, bin] > actualClipLimit)
                        {
                            clippedSum += histograms[ty, tx, bin] - actualClipLimit;
                            histograms[ty, tx, bin] = actualClipLimit;
                        }
                    }

                    int redistributed = clippedSum / 256;
                    int remainder = clippedSum % 256;

                    for (int bin = 0; bin < 256; bin++)
                    {
                        histograms[ty, tx, bin] += redistributed;
                    }

                    for (int bin = 0; bin < remainder; bin++)
                    {
                        histograms[ty, tx, bin]++;
                    }
                }
            }

            // 3. Compute CDFs
            float[,,] tileCDFs = new float[tileY, tileX, 256];
            for (int ty = 0; ty < tileY; ty++)
            {
                for (int tx = 0; tx < tileX; tx++)
                {
                    int sum = 0;
                    for (int bin = 0; bin < 256; bin++)
                    {
                        sum += histograms[ty, tx, bin];
                        tileCDFs[ty, tx, bin] = sum;
                    }

                    float cdfMin = 0;
                    for (int bin = 0; bin < 256; bin++)
                    {
                        if (tileCDFs[ty, tx, bin] > 0)
                        {
                            cdfMin = tileCDFs[ty, tx, bin];
                            break;
                        }
                    }

                    float denominator = totalTilePixels - cdfMin;
                    if (denominator < 1) denominator = 1;

                    for (int bin = 0; bin < 256; bin++)
                    {
                        float val = (tileCDFs[ty, tx, bin] - cdfMin) / denominator * 255.0f;
                        tileCDFs[ty, tx, bin] = Math.Clamp(val, 0.0f, 255.0f);
                    }
                }
            }

            // 4. Map pixels using Bilinear Interpolation
            byte[,] outputPixels = new byte[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte g = image[x, y].PackedValue;

                    float txCoord = (x - tileWidth / 2.0f) / tileWidth;
                    float tyCoord = (y - tileHeight / 2.0f) / tileHeight;

                    int tx1 = (int)Math.Floor(txCoord);
                    int ty1 = (int)Math.Floor(tyCoord);

                    int tx2 = tx1 + 1;
                    int ty2 = ty1 + 1;

                    tx1 = Math.Clamp(tx1, 0, tileX - 1);
                    tx2 = Math.Clamp(tx2, 0, tileX - 1);
                    ty1 = Math.Clamp(ty1, 0, tileY - 1);
                    ty2 = Math.Clamp(ty2, 0, tileY - 1);

                    float fx = txCoord - (float)Math.Floor(txCoord);
                    float fy = tyCoord - (float)Math.Floor(tyCoord);

                    float c11 = tileCDFs[ty1, tx1, g];
                    float c12 = tileCDFs[ty1, tx2, g];
                    float c21 = tileCDFs[ty2, tx1, g];
                    float c22 = tileCDFs[ty2, tx2, g];

                    float interpolated = (1.0f - fx) * (1.0f - fy) * c11
                                       + fx * (1.0f - fy) * c12
                                       + (1.0f - fx) * fy * c21
                                       + fx * fy * c22;

                    outputPixels[x, y] = (byte)Math.Clamp(interpolated, 0.0f, 255.0f);
                }
            }

            // Write back to image
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = new L8(outputPixels[x, y]);
                }
            }
        }

        private Image<L8> ApplyMedianFilter(Image<L8> src)
        {
            int width = src.Width;
            int height = src.Height;
            var dest = src.Clone();

            byte[] neighbors = new byte[9];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    neighbors[0] = src[x - 1, y - 1].PackedValue;
                    neighbors[1] = src[x, y - 1].PackedValue;
                    neighbors[2] = src[x + 1, y - 1].PackedValue;
                    neighbors[3] = src[x - 1, y].PackedValue;
                    neighbors[4] = src[x, y].PackedValue;
                    neighbors[5] = src[x + 1, y].PackedValue;
                    neighbors[6] = src[x - 1, y + 1].PackedValue;
                    neighbors[7] = src[x, y + 1].PackedValue;
                    neighbors[8] = src[x + 1, y + 1].PackedValue;

                    Array.Sort(neighbors);

                    dest[x, y] = new L8(neighbors[4]);
                }
            }

            return dest;
        }
    }

    public class PreprocessingOutput
    {
        public string OriginalFileName { get; set; } = string.Empty;
        public double OriginalSizeKb { get; set; }
        public string ProcessedFileName { get; set; } = string.Empty;
        public double ProcessedSizeKb { get; set; }
        public double CompressionRatio { get; set; }
        public string[] AppliedSteps { get; set; } = Array.Empty<string>();
    }
}
