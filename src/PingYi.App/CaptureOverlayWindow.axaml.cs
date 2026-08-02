using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PingYi.Core;
using CorePixelRect = PingYi.Core.PixelRect;

namespace PingYi.App;

public partial class CaptureOverlayWindow : Window
{
    private readonly ImageFrame _capture;
    private readonly Bitmap _bitmap;
    private readonly TaskCompletionSource<CorePixelRect?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Point _start;
    private bool _selecting;

    public CaptureOverlayWindow() : this(CreatePlaceholderCapture())
    {
    }

    public CaptureOverlayWindow(ImageFrame capture)
    {
        _capture = capture;
        InitializeComponent();
        _bitmap = new Bitmap(new MemoryStream(capture.PngBytes));
        ScreenshotImage.Source = _bitmap;
        Position = new PixelPoint(capture.DesktopBounds.X, capture.DesktopBounds.Y);

        Opened += (_, _) =>
        {
            var originScreen = Screens.ScreenFromPoint(
                new PixelPoint(capture.DesktopBounds.X, capture.DesktopBounds.Y));
            var scale = RenderScaling > 0 ? RenderScaling : originScreen?.Scaling ?? 1;
            Width = capture.Width / scale;
            Height = capture.Height / scale;
            Position = new PixelPoint(capture.DesktopBounds.X, capture.DesktopBounds.Y);
            Activate();
            Focus();
        };
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                Complete(null);
            }
        };
        Closed += (_, _) =>
        {
            _completion.TrySetResult(null);
            _bitmap.Dispose();
        };
    }

    public Task<CorePixelRect?> ShowAndSelectAsync()
    {
        Show();
        return _completion.Task;
    }

    private void SelectionOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SelectionOverlay).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _start = ClampToOverlay(e.GetPosition(SelectionOverlay));
        _selecting = true;
        e.Pointer.Capture(SelectionOverlay);
        SelectionOverlay.Selection = new Rect(_start, _start);
        SelectionSizeBorder.IsVisible = true;
        UpdateSelectionSize(SelectionOverlay.Selection.Value);
    }

    private void SelectionOverlay_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        var selection = Normalize(_start, ClampToOverlay(e.GetPosition(SelectionOverlay)));
        SelectionOverlay.Selection = selection;
        UpdateSelectionSize(selection);
    }

    private void SelectionOverlay_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        e.Pointer.Capture(null);
        var selection = Normalize(_start, ClampToOverlay(e.GetPosition(SelectionOverlay)));
        SelectionOverlay.Selection = selection;
        if (selection.Width < 8 || selection.Height < 8 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            SelectionOverlay.Selection = null;
            SelectionSizeBorder.IsVisible = false;
            return;
        }

        var x = (int)Math.Round(selection.X / Bounds.Width * _capture.Width);
        var y = (int)Math.Round(selection.Y / Bounds.Height * _capture.Height);
        var width = (int)Math.Round(selection.Width / Bounds.Width * _capture.Width);
        var height = (int)Math.Round(selection.Height / Bounds.Height * _capture.Height);
        Complete(new CorePixelRect(x, y, width, height));
    }

    private Point ClampToOverlay(Point point) =>
        new(
            Math.Clamp(point.X, 0, Math.Max(0, SelectionOverlay.Bounds.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, SelectionOverlay.Bounds.Height)));

    private void UpdateSelectionSize(Rect selection)
    {
        var width = Bounds.Width > 0
            ? (int)Math.Round(selection.Width / Bounds.Width * _capture.Width)
            : 0;
        var height = Bounds.Height > 0
            ? (int)Math.Round(selection.Height / Bounds.Height * _capture.Height)
            : 0;
        SelectionSizeText.Text = $"{width} × {height} px";
    }

    private void Complete(CorePixelRect? selection)
    {
        if (_completion.TrySetResult(selection))
        {
            Close();
        }
    }

    private static Rect Normalize(Point first, Point second) =>
        new(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Abs(first.X - second.X),
            Math.Abs(first.Y - second.Y));

    private static ImageFrame CreatePlaceholderCapture()
    {
        const string transparentPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        return new ImageFrame(Convert.FromBase64String(transparentPng), 1, 1, new CorePixelRect(0, 0, 1, 1));
    }
}

public sealed class SelectionOverlayControl : Control
{
    private Rect? _selection;

    public Rect? Selection
    {
        get => _selection;
        set
        {
            _selection = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var shade = new SolidColorBrush(Color.FromArgb(145, 4, 10, 22));
        if (Selection is not { } selection || selection.Width <= 0 || selection.Height <= 0)
        {
            context.FillRectangle(shade, Bounds);
            return;
        }

        context.FillRectangle(shade, new Rect(0, 0, Bounds.Width, Math.Max(0, selection.Top)));
        context.FillRectangle(shade, new Rect(0, selection.Bottom, Bounds.Width, Math.Max(0, Bounds.Height - selection.Bottom)));
        context.FillRectangle(shade, new Rect(0, selection.Top, Math.Max(0, selection.Left), selection.Height));
        context.FillRectangle(shade, new Rect(selection.Right, selection.Top, Math.Max(0, Bounds.Width - selection.Right), selection.Height));
        var accent = new SolidColorBrush(Color.FromRgb(45, 212, 191));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(14, 45, 212, 191)), new Pen(accent, 2), selection);

        const double handleSize = 8;
        var halfHandle = handleSize / 2;
        var handleFill = new SolidColorBrush(Colors.White);
        foreach (var point in new[]
                 {
                     selection.TopLeft,
                     selection.TopRight,
                     selection.BottomLeft,
                     selection.BottomRight
                 })
        {
            var handle = new Rect(point.X - halfHandle, point.Y - halfHandle, handleSize, handleSize);
            context.DrawRectangle(handleFill, new Pen(accent, 2), handle, 2, 2);
        }
    }
}
