using Microsoft.Maui.Graphics;

namespace RouteLab.Services;

public sealed class BusyIndicatorService : IBusyIndicatorService
{
    private readonly object syncRoot = new();
    private readonly Dictionary<long, string> operations = new();
    private WindowOverlay? overlay;
    private BusyOverlayElement? overlayElement;
    private CancellationTokenSource? animationCancellation;
    private Window? window;
    private long nextOperationId;

    public bool IsBusy
    {
        get
        {
            lock (syncRoot)
            {
                return operations.Count > 0;
            }
        }
    }

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        this.window = window;
        window.Created += (_, _) => MainThread.BeginInvokeOnMainThread(EnsureOverlay);
    }

    public IDisposable Show(string message = "Operazione in corso...")
    {
        var operationId = Interlocked.Increment(ref nextOperationId);
        lock (syncRoot)
        {
            operations[operationId] = NormalizeMessage(message);
        }

        MainThread.BeginInvokeOnMainThread(RefreshOverlay);
        return new BusyScope(this, operationId);
    }

    private void Complete(long operationId)
    {
        lock (syncRoot)
        {
            operations.Remove(operationId);
        }

        MainThread.BeginInvokeOnMainThread(RefreshOverlay);
    }

    private void EnsureOverlay()
    {
        if (overlay is not null || window is null)
        {
            return;
        }

        overlayElement = new BusyOverlayElement();
        overlay = new WindowOverlay(window)
        {
            DisableUITouchEventPassthrough = false,
            EnableDrawableTouchHandling = false,
            IsVisible = false
        };
        overlay.AddWindowElement(overlayElement);
        ((IWindow)window).AddOverlay(overlay);
        RefreshOverlay();
    }

    private void RefreshOverlay()
    {
        EnsureOverlay();
        if (overlay is null || overlayElement is null)
        {
            return;
        }

        string? message;
        lock (syncRoot)
        {
            message = operations.Count == 0
                ? null
                : operations.OrderBy(item => item.Key).Last().Value;
        }

        if (message is null)
        {
            animationCancellation?.Cancel();
            animationCancellation?.Dispose();
            animationCancellation = null;
            overlay.DisableUITouchEventPassthrough = false;
            overlay.EnableDrawableTouchHandling = false;
            overlay.IsVisible = false;
            overlay.Invalidate();
            return;
        }

        overlayElement.Message = message;
        overlay.DisableUITouchEventPassthrough = true;
        overlay.EnableDrawableTouchHandling = true;
        overlay.IsVisible = true;
        overlay.Invalidate();
        if (animationCancellation is null)
        {
            animationCancellation = new CancellationTokenSource();
            _ = AnimateAsync(animationCancellation.Token);
        }
    }

    private async Task AnimateAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(75, cancellationToken);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (overlay is null || overlayElement is null || !overlay.IsVisible)
                    {
                        return;
                    }

                    overlayElement.RotationStep = (overlayElement.RotationStep + 1) % 12;
                    overlay.Invalidate();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string NormalizeMessage(string? message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "Operazione in corso..."
            : message.Trim();
    }

    private sealed class BusyScope : IDisposable
    {
        private BusyIndicatorService? owner;
        private readonly long operationId;

        public BusyScope(BusyIndicatorService owner, long operationId)
        {
            this.owner = owner;
            this.operationId = operationId;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Complete(operationId);
        }
    }

    private sealed class BusyOverlayElement : IWindowOverlayElement
    {
        public string Message { get; set; } = "Operazione in corso...";

        public int RotationStep { get; set; }

        public bool Contains(Point point) => true;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.FillColor = new Color(0.04f, 0.035f, 0.025f, 0.78f);
            canvas.FillRectangle(dirtyRect);

            var cardWidth = Math.Min(340f, Math.Max(260f, dirtyRect.Width - 40f));
            const float cardHeight = 154f;
            var cardX = dirtyRect.Center.X - (cardWidth / 2f);
            var cardY = dirtyRect.Center.Y - (cardHeight / 2f);
            var cardBounds = new RectF(cardX, cardY, cardWidth, cardHeight);

            canvas.FillColor = new Color(0.09f, 0.075f, 0.045f, 0.98f);
            canvas.FillRoundedRectangle(cardBounds, 22f);
            canvas.StrokeColor = new Color(0.95f, 0.75f, 0.20f, 1f);
            canvas.StrokeSize = 2f;
            canvas.DrawRoundedRectangle(cardBounds, 22f);

            const float spinnerRadius = 27f;
            const float dotRadius = 4.5f;
            var centerX = dirtyRect.Center.X;
            var centerY = cardY + 53f;
            for (var index = 0; index < 12; index++)
            {
                var rotatedIndex = (index + RotationStep) % 12;
                var angle = (MathF.PI * 2f * index / 12f) - (MathF.PI / 2f);
                var alpha = 0.16f + (0.84f * (rotatedIndex + 1) / 12f);
                canvas.FillColor = new Color(0.95f, 0.75f, 0.20f, alpha);
                canvas.FillCircle(
                    centerX + (MathF.Cos(angle) * spinnerRadius),
                    centerY + (MathF.Sin(angle) * spinnerRadius),
                    dotRadius);
            }

            canvas.FontColor = new Color(0.98f, 0.91f, 0.66f, 1f);
            canvas.FontSize = 16f;
            canvas.DrawString(
                Message,
                cardX + 18f,
                cardY + 100f,
                cardWidth - 36f,
                34f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
            canvas.RestoreState();
        }
    }
}
