namespace PingYi.Core;

public sealed record TranslationExecution(
    TranslationResult Result,
    ProviderMetadata Provider,
    bool UsedFallback);

public static class TranslationFallback
{
    public static async Task<TranslationExecution> ExecuteAsync(
        ITranslationProvider primary,
        ITranslationProvider offlineFallback,
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = await primary.GetAvailabilityAsync(cancellationToken);
            if (!availability.IsAvailable)
            {
                throw new ProviderException(
                    "translation_unavailable",
                    availability.Message ?? "翻译引擎不可用。");
            }

            var result = await primary.TranslateAsync(request, cancellationToken);
            return new TranslationExecution(result, primary.Metadata, UsedFallback: false);
        }
        catch (Exception primaryFailure) when (
            primaryFailure is not OperationCanceledException &&
            !cancellationToken.IsCancellationRequested &&
            primary.Metadata.Id != offlineFallback.Metadata.Id)
        {
            try
            {
                var fallbackAvailability = await offlineFallback.GetAvailabilityAsync(cancellationToken);
                if (!fallbackAvailability.IsAvailable)
                {
                    throw new ProviderException(
                        "translation_fallback_unavailable",
                        fallbackAvailability.Message ?? "本地离线翻译不可用。");
                }

                var result = await offlineFallback.TranslateAsync(request, cancellationToken);
                return new TranslationExecution(result, offlineFallback.Metadata, UsedFallback: true);
            }
            catch (Exception fallbackFailure) when (fallbackFailure is not OperationCanceledException)
            {
                throw new ProviderException(
                    "translation_primary_and_fallback_failed",
                    $"{primaryFailure.Message}；离线回退也不可用：{fallbackFailure.Message}",
                    primaryFailure);
            }
        }
    }
}
