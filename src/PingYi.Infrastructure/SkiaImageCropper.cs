using PingYi.Core;
using SkiaSharp;

namespace PingYi.Infrastructure;

public sealed class SkiaImageCropper : IImageCropper
{
    public ImageFrame Crop(ImageFrame source, PixelRect cropBounds)
    {
        using var sourceBitmap = SKBitmap.Decode(source.PngBytes)
            ?? throw new InvalidOperationException("无法读取屏幕截图。");

        var x = Math.Clamp(cropBounds.X, 0, sourceBitmap.Width - 1);
        var y = Math.Clamp(cropBounds.Y, 0, sourceBitmap.Height - 1);
        var width = Math.Clamp(cropBounds.Width, 1, sourceBitmap.Width - x);
        var height = Math.Clamp(cropBounds.Height, 1, sourceBitmap.Height - y);

        using var cropped = new SKBitmap(width, height, sourceBitmap.ColorType, sourceBitmap.AlphaType);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(
                sourceBitmap,
                new SKRect(x, y, x + width, y + height),
                new SKRect(0, 0, width, height));
        }

        using var image = SKImage.FromBitmap(cropped);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new ImageFrame(
            data.ToArray(),
            width,
            height,
            new PixelRect(
                source.DesktopBounds.X + x,
                source.DesktopBounds.Y + y,
                width,
                height));
    }
}
