using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PingYi.Core;
using SkiaSharp;

namespace PingYi.Infrastructure;

public sealed class PaddleOcrProvider(AppDataPaths paths) : IOcrProvider, IDisposable
{
    private const string DetectionModelName = "PP-OCRv5_mobile_det_onnx";
    private const string RecognitionModelName = "PP-OCRv5_mobile_rec_onnx";
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private readonly Dictionary<string, (long Length, DateTime LastWriteUtc, string Hash)> _hashCache = [];
    private InferenceSession? _detectionSession;
    private InferenceSession? _recognitionSession;
    private IReadOnlyList<string>? _characters;

    public ProviderMetadata Metadata { get; } = new(
        "local-paddle",
        "本地 PaddleOCR ONNX",
        ProviderExecutionLocation.Local,
        UploadsImage: false,
        RequiresSecret: false,
        ["zh", "en"]);

    public ValueTask<ProviderAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TryResolveModelFiles(out _, out _, out _, out var message)
            ? ProviderAvailability.Available
            : new ProviderAvailability(false, message));
    }

    public async Task<OcrResult> RecognizeAsync(
        ImageFrame image,
        OcrOptions options,
        CancellationToken cancellationToken = default)
    {
        await _inferenceGate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            using var bitmap = SKBitmap.Decode(image.PngBytes)
                ?? throw new ProviderException("ocr_image_invalid", "无法读取所选截图。");

            var boxes = DetectTextBoxes(bitmap, cancellationToken);
            var blocks = new List<OcrBlock>(boxes.Count);
            foreach (var box in boxes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recognition = RecognizeBox(bitmap, box);
                if (!string.IsNullOrWhiteSpace(recognition.Text))
                {
                    blocks.Add(new OcrBlock(recognition.Text, box, recognition.Confidence));
                }
            }

            var ordered = blocks
                .OrderBy(block => block.Bounds.Y)
                .ThenBy(block => block.Bounds.X)
                .ToArray();
            var plainText = TextProcessing.BuildPlainText(ordered);
            return new OcrResult(ordered, plainText, TextProcessing.DetectLanguage(plainText));
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    public Task InstallModelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryResolveModelFiles(out _, out _, out _, out _))
        {
            return Task.CompletedTask;
        }

        throw new ProviderException(
            "ocr_models_missing",
            "离线 OCR 模型未包含在安装包中，请重新安装标准离线版或导入离线模型包。");
    }

    private void EnsureInitialized()
    {
        if (_detectionSession is not null && _recognitionSession is not null && _characters is not null)
        {
            return;
        }

        if (!TryResolveModelFiles(
                out var detectionModel,
                out var recognitionModel,
                out var recognitionConfig,
                out var message))
        {
            throw new ProviderException("ocr_models_missing", message);
        }

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        _detectionSession = new InferenceSession(detectionModel, sessionOptions);
        _recognitionSession = new InferenceSession(recognitionModel, sessionOptions);
        _characters = ReadCharacterDictionary(recognitionConfig);
    }

    private List<PixelRect> DetectTextBoxes(SKBitmap bitmap, CancellationToken cancellationToken)
    {
        const int maxSide = 960;
        var scale = Math.Min(1d, (double)maxSide / Math.Max(bitmap.Width, bitmap.Height));
        var resizedWidth = Math.Max(32, (int)Math.Round(bitmap.Width * scale / 32d) * 32);
        var resizedHeight = Math.Max(32, (int)Math.Round(bitmap.Height * scale / 32d) * 32);
        var data = new float[3 * resizedHeight * resizedWidth];

        using var resized = bitmap.Resize(new SKImageInfo(resizedWidth, resizedHeight), SKSamplingOptions.Default)
            ?? throw new ProviderException("ocr_resize_failed", "OCR 图像缩放失败。");
        FillDetectionTensor(resized, data);

        var tensor = new DenseTensor<float>(data, [1, 3, resizedHeight, resizedWidth]);
        var input = NamedOnnxValue.CreateFromTensor("x", tensor);
        using var outputs = _detectionSession!.Run([input]);
        var map = outputs.First().AsTensor<float>();
        var dimensions = map.Dimensions.ToArray();
        var mapHeight = dimensions[^2];
        var mapWidth = dimensions[^1];
        var probabilities = map.ToArray();
        return ExtractConnectedTextRegions(
            probabilities,
            mapWidth,
            mapHeight,
            bitmap.Width,
            bitmap.Height,
            cancellationToken);
    }

    private static void FillDetectionTensor(SKBitmap bitmap, float[] data)
    {
        var plane = bitmap.Width * bitmap.Height;
        var means = new[] { 0.485f, 0.456f, 0.406f };
        var stds = new[] { 0.229f, 0.224f, 0.225f };
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var index = y * bitmap.Width + x;
                data[index] = (color.Blue / 255f - means[0]) / stds[0];
                data[plane + index] = (color.Green / 255f - means[1]) / stds[1];
                data[2 * plane + index] = (color.Red / 255f - means[2]) / stds[2];
            }
        }
    }

    private static List<PixelRect> ExtractConnectedTextRegions(
        float[] probabilities,
        int width,
        int height,
        int originalWidth,
        int originalHeight,
        CancellationToken cancellationToken)
    {
        const float threshold = 0.3f;
        const float boxThreshold = 0.6f;
        var visited = new bool[width * height];
        var queue = new Queue<int>();
        var boxes = new List<PixelRect>();
        var neighborOffsets = new (int X, int Y)[]
        {
            (-1, -1), (0, -1), (1, -1),
            (-1, 0),            (1, 0),
            (-1, 1),  (0, 1),  (1, 1)
        };

        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var start = y * width + x;
                if (visited[start] || probabilities[start] < threshold)
                {
                    continue;
                }

                visited[start] = true;
                queue.Enqueue(start);
                var minX = x;
                var maxX = x;
                var minY = y;
                var maxY = y;
                var score = 0d;
                var count = 0;
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var currentY = current / width;
                    var currentX = current - currentY * width;
                    minX = Math.Min(minX, currentX);
                    maxX = Math.Max(maxX, currentX);
                    minY = Math.Min(minY, currentY);
                    maxY = Math.Max(maxY, currentY);
                    score += probabilities[current];
                    count++;

                    foreach (var (offsetX, offsetY) in neighborOffsets)
                    {
                        var nextX = currentX + offsetX;
                        var nextY = currentY + offsetY;
                        if ((uint)nextX >= (uint)width || (uint)nextY >= (uint)height)
                        {
                            continue;
                        }

                        var next = nextY * width + nextX;
                        if (!visited[next] && probabilities[next] >= threshold)
                        {
                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }

                if (count < 6 || score / count < boxThreshold || maxX - minX < 2 || maxY - minY < 2)
                {
                    continue;
                }

                var regionWidth = maxX - minX + 1;
                var regionHeight = maxY - minY + 1;
                var expandX = Math.Max(2, (int)Math.Round(regionHeight * 0.35));
                var expandY = Math.Max(1, (int)Math.Round(regionHeight * 0.18));
                minX = Math.Max(0, minX - expandX);
                maxX = Math.Min(width - 1, maxX + expandX);
                minY = Math.Max(0, minY - expandY);
                maxY = Math.Min(height - 1, maxY + expandY);

                var left = Math.Clamp((int)Math.Floor((double)minX / width * originalWidth), 0, originalWidth - 1);
                var top = Math.Clamp((int)Math.Floor((double)minY / height * originalHeight), 0, originalHeight - 1);
                var right = Math.Clamp((int)Math.Ceiling((double)(maxX + 1) / width * originalWidth), left + 1, originalWidth);
                var bottom = Math.Clamp((int)Math.Ceiling((double)(maxY + 1) / height * originalHeight), top + 1, originalHeight);
                boxes.Add(new PixelRect(left, top, right - left, bottom - top));
            }
        }

        return MergeNearbyBoxes(boxes);
    }

    private static List<PixelRect> MergeNearbyBoxes(List<PixelRect> boxes)
    {
        var ordered = boxes.OrderBy(box => box.Y).ThenBy(box => box.X).ToList();
        var merged = new List<PixelRect>();
        foreach (var box in ordered)
        {
            var match = merged.FindIndex(existing =>
            {
                var verticalOverlap = Math.Min(existing.Y + existing.Height, box.Y + box.Height) - Math.Max(existing.Y, box.Y);
                var minHeight = Math.Min(existing.Height, box.Height);
                var gap = box.X - (existing.X + existing.Width);
                return verticalOverlap > minHeight * 0.55 && gap >= -minHeight * 0.4 && gap <= minHeight * 1.3;
            });
            if (match < 0)
            {
                merged.Add(box);
                continue;
            }

            var existing = merged[match];
            var left = Math.Min(existing.X, box.X);
            var top = Math.Min(existing.Y, box.Y);
            var right = Math.Max(existing.X + existing.Width, box.X + box.Width);
            var bottom = Math.Max(existing.Y + existing.Height, box.Y + box.Height);
            merged[match] = new PixelRect(left, top, right - left, bottom - top);
        }

        return merged.Where(box => box.Width >= 4 && box.Height >= 4).ToList();
    }

    private (string Text, double Confidence) RecognizeBox(SKBitmap source, PixelRect box)
    {
        const int targetHeight = 48;
        const int targetWidth = 320;
        using var cropped = new SKBitmap(box.Width, box.Height);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(
                source,
                new SKRect(box.X, box.Y, box.X + box.Width, box.Y + box.Height),
                new SKRect(0, 0, box.Width, box.Height));
        }

        var resizedWidth = Math.Clamp((int)Math.Ceiling((double)targetHeight * box.Width / box.Height), 1, targetWidth);
        using var resized = cropped.Resize(new SKImageInfo(resizedWidth, targetHeight), SKSamplingOptions.Default)
            ?? throw new ProviderException("ocr_resize_failed", "OCR 文字区域缩放失败。");
        var data = new float[3 * targetHeight * targetWidth];
        Array.Fill(data, 1f);
        var plane = targetHeight * targetWidth;
        for (var y = 0; y < targetHeight; y++)
        {
            for (var x = 0; x < resizedWidth; x++)
            {
                var color = resized.GetPixel(x, y);
                var index = y * targetWidth + x;
                data[index] = color.Blue / 127.5f - 1f;
                data[plane + index] = color.Green / 127.5f - 1f;
                data[2 * plane + index] = color.Red / 127.5f - 1f;
            }
        }

        var tensor = new DenseTensor<float>(data, [1, 3, targetHeight, targetWidth]);
        var input = NamedOnnxValue.CreateFromTensor("x", tensor);
        using var outputs = _recognitionSession!.Run([input]);
        var output = outputs.First().AsTensor<float>();
        var dimensions = output.Dimensions.ToArray();
        var steps = dimensions[^2];
        var classes = dimensions[^1];
        var values = output.ToArray();
        var result = new StringBuilder();
        var confidence = 0d;
        var accepted = 0;
        var previous = -1;
        for (var step = 0; step < steps; step++)
        {
            var offset = step * classes;
            var bestIndex = 0;
            var bestScore = values[offset];
            for (var index = 1; index < classes; index++)
            {
                var value = values[offset + index];
                if (value > bestScore)
                {
                    bestScore = value;
                    bestIndex = index;
                }
            }

            if (bestIndex != 0 && bestIndex != previous)
            {
                var character = CharacterForIndex(bestIndex);
                if (character is not null)
                {
                    result.Append(character);
                    confidence += bestScore;
                    accepted++;
                }
            }

            previous = bestIndex;
        }

        return (result.ToString().Trim(), accepted == 0 ? 0 : confidence / accepted);
    }

    private string? CharacterForIndex(int index)
    {
        if (index <= 0)
        {
            return null;
        }

        if (index <= _characters!.Count)
        {
            return _characters[index - 1];
        }

        return index == _characters.Count + 1 ? " " : null;
    }

    private bool TryResolveModelFiles(
        out string detectionModel,
        out string recognitionModel,
        out string recognitionConfig,
        out string message)
    {
        foreach (var root in paths.ModelSearchDirectories)
        {
            var officialModels = Path.Combine(root, "paddle", "official_models");
            detectionModel = Path.Combine(officialModels, DetectionModelName, "inference.onnx");
            recognitionModel = Path.Combine(officialModels, RecognitionModelName, "inference.onnx");
            recognitionConfig = Path.Combine(officialModels, RecognitionModelName, "inference.yml");
            if (File.Exists(detectionModel) && File.Exists(recognitionModel) && File.Exists(recognitionConfig))
            {
                if (!VerifyModelIntegrity(root, detectionModel, recognitionModel))
                {
                    message = "中英 OCR 离线模型完整性校验失败，请重新安装标准离线版。";
                    continue;
                }

                message = string.Empty;
                return true;
            }
        }

        detectionModel = string.Empty;
        recognitionModel = string.Empty;
        recognitionConfig = string.Empty;
        message = "安装包缺少中英 OCR 离线模型，请重新安装标准离线版。";
        return false;
    }

    private bool VerifyModelIntegrity(string root, string detectionModel, string recognitionModel)
    {
        var manifestPath = Path.Combine(root, "ocr-models.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
            var expected = document.RootElement.GetProperty("sha256");
            return string.Equals(
                       GetSha256(detectionModel),
                       expected.GetProperty("paddle-detection").GetString(),
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       GetSha256(recognitionModel),
                       expected.GetProperty("paddle-recognition").GetString(),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            KeyNotFoundException or
            InvalidOperationException)
        {
            return false;
        }
    }

    private string GetSha256(string path)
    {
        var info = new FileInfo(path);
        if (_hashCache.TryGetValue(path, out var cached) &&
            cached.Length == info.Length &&
            cached.LastWriteUtc == info.LastWriteTimeUtc)
        {
            return cached.Hash;
        }

        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        _hashCache[path] = (info.Length, info.LastWriteTimeUtc, hash);
        return hash;
    }

    private static IReadOnlyList<string> ReadCharacterDictionary(string configurationPath)
    {
        var characters = new List<string>(18_383);
        var reading = false;
        foreach (var line in File.ReadLines(configurationPath, Encoding.UTF8))
        {
            if (!reading)
            {
                reading = string.Equals(line.Trim(), "character_dict:", StringComparison.Ordinal);
                continue;
            }

            if (!line.StartsWith("  -", StringComparison.Ordinal))
            {
                if (characters.Count > 0)
                {
                    break;
                }

                continue;
            }

            var value = line.Length > 3 ? line[3..].TrimStart() : string.Empty;
            if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                value = value[1..^1].Replace("''", "'", StringComparison.Ordinal);
            }
            else if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1]
                    .Replace("\\\"", "\"", StringComparison.Ordinal)
                    .Replace("\\\\", "\\", StringComparison.Ordinal);
            }

            characters.Add(value);
        }

        if (characters.Count != 18_383)
        {
            throw new ProviderException(
                "ocr_dictionary_invalid",
                $"OCR 字符表不完整：应有 18383 项，实际为 {characters.Count} 项。");
        }

        return characters;
    }

    public void Dispose()
    {
        _detectionSession?.Dispose();
        _recognitionSession?.Dispose();
        _inferenceGate.Dispose();
    }
}
