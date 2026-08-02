using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PingYi.Core;

namespace PingYi.Infrastructure;

public sealed class BaiduOcrProvider(HttpClient httpClient, ISecretStore secretStore) : IOcrProvider
{
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public ProviderMetadata Metadata { get; } = new(
        "baidu-ocr",
        "百度云 OCR",
        ProviderExecutionLocation.Cloud,
        UploadsImage: true,
        RequiresSecret: true,
        ["zh", "en"]);

    public async ValueTask<ProviderAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await secretStore.GetAsync(SecretKeys.BaiduOcrApiKey, cancellationToken);
        var secret = await secretStore.GetAsync(SecretKeys.BaiduOcrSecretKey, cancellationToken);
        return string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secret)
            ? new ProviderAvailability(false, "请先填写百度 OCR API Key 和 Secret Key。")
            : ProviderAvailability.Available;
    }

    public async Task<OcrResult> RecognizeAsync(
        ImageFrame image,
        OcrOptions options,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://aip.baidubce.com/rest/2.0/ocr/v1/general?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["image"] = Convert.ToBase64String(image.PngBytes),
                ["language_type"] = "CHN_ENG",
                ["detect_direction"] = "true",
                ["paragraph"] = "true",
                ["vertexes_location"] = "true"
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException("baidu_ocr_http", $"百度 OCR 请求失败：HTTP {(int)response.StatusCode}。");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.TryGetProperty("error_code", out var errorCode))
        {
            var message = root.TryGetProperty("error_msg", out var errorMessage)
                ? errorMessage.GetString()
                : "未知错误";
            throw new ProviderException("baidu_ocr_api", $"百度 OCR 返回错误 {errorCode}: {message}");
        }

        var blocks = new List<OcrBlock>();
        if (root.TryGetProperty("words_result", out var wordsResult))
        {
            foreach (var item in wordsResult.EnumerateArray())
            {
                var location = item.TryGetProperty("location", out var value) ? value : default;
                blocks.Add(new OcrBlock(
                    item.GetProperty("words").GetString() ?? string.Empty,
                    location.ValueKind == JsonValueKind.Object
                        ? new PixelRect(
                            location.GetProperty("left").GetInt32(),
                            location.GetProperty("top").GetInt32(),
                            location.GetProperty("width").GetInt32(),
                            location.GetProperty("height").GetInt32())
                        : new PixelRect(0, blocks.Count * 24, image.Width, 24),
                    0));
            }
        }

        var text = TextProcessing.BuildPlainText(blocks);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ProviderException("no_text", "所选区域中没有识别到文字。");
        }

        return new OcrResult(blocks, text, TextProcessing.DetectLanguage(text));
    }

    public async Task ValidateCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            _accessToken = null;
            _accessTokenExpiresAt = default;
        }
        finally
        {
            _tokenGate.Release();
        }

        _ = await GetAccessTokenAsync(cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _accessToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _accessToken;
            }

            var apiKey = await secretStore.GetAsync(SecretKeys.BaiduOcrApiKey, cancellationToken);
            var secret = await secretStore.GetAsync(SecretKeys.BaiduOcrSecretKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secret))
            {
                throw new ProviderException("credentials_missing", "尚未配置百度 OCR 凭据。");
            }

            var url = "https://aip.baidubce.com/oauth/2.0/token" +
                      $"?grant_type=client_credentials&client_id={Uri.EscapeDataString(apiKey)}" +
                      $"&client_secret={Uri.EscapeDataString(secret)}";
            using var response = await httpClient.PostAsync(url, null, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!response.IsSuccessStatusCode || !document.RootElement.TryGetProperty("access_token", out var token))
            {
                throw new ProviderException("credentials_invalid", "百度 OCR 凭据验证失败。");
            }

            _accessToken = token.GetString();
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiry)
                ? expiry.GetInt32()
                : 2_592_000;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return _accessToken!;
        }
        finally
        {
            _tokenGate.Release();
        }
    }
}

public sealed class BaiduTranslationProvider(HttpClient httpClient, ISecretStore secretStore) : ITranslationProvider
{
    public ProviderMetadata Metadata { get; } = new(
        "baidu-translate",
        "百度翻译",
        ProviderExecutionLocation.Cloud,
        UploadsImage: false,
        RequiresSecret: true,
        ["zh", "en"]);

    public async ValueTask<ProviderAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var appId = await secretStore.GetAsync(SecretKeys.BaiduTranslateAppId, cancellationToken);
        var secret = await secretStore.GetAsync(SecretKeys.BaiduTranslateSecret, cancellationToken);
        return string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret)
            ? new ProviderAvailability(false, "请先填写百度翻译 APP ID 和密钥。")
            : ProviderAvailability.Available;
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        var appId = await secretStore.GetAsync(SecretKeys.BaiduTranslateAppId, cancellationToken);
        var secret = await secretStore.GetAsync(SecretKeys.BaiduTranslateSecret, cancellationToken);
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret))
        {
            throw new ProviderException("credentials_missing", "尚未配置百度翻译凭据。");
        }

        var salt = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString(CultureInfo.InvariantCulture);
        var signBytes = MD5.HashData(Encoding.UTF8.GetBytes(appId + request.Text + salt + secret));
        var sign = Convert.ToHexString(signBytes).ToLowerInvariant();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["q"] = request.Text,
            ["from"] = request.SourceLanguage is "zh" or "en" ? request.SourceLanguage : "auto",
            ["to"] = request.TargetLanguage,
            ["appid"] = appId,
            ["salt"] = salt,
            ["sign"] = sign
        });

        using var response = await httpClient.PostAsync(
            "https://fanyi-api.baidu.com/api/trans/vip/translate",
            content,
            cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var hasErrorCode = root.TryGetProperty("error_code", out var errorCode);
        if (!response.IsSuccessStatusCode || hasErrorCode)
        {
            var error = hasErrorCode ? errorCode.GetString() : "HTTP";
            var message = root.TryGetProperty("error_msg", out var errorMessage)
                ? errorMessage.GetString()
                : "请求失败";
            throw new ProviderException("baidu_translate_api", $"百度翻译返回错误 {error}: {message}");
        }

        var translated = root.GetProperty("trans_result")
            .EnumerateArray()
            .Select(item => item.GetProperty("dst").GetString() ?? string.Empty);
        return new TranslationResult(
            string.Join(Environment.NewLine, translated),
            request.SourceLanguage,
            request.TargetLanguage);
    }

    public async Task ValidateCredentialsAsync(CancellationToken cancellationToken = default)
    {
        _ = await TranslateAsync(new TranslationRequest("test", "en", "zh"), cancellationToken);
    }
}
