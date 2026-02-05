using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ScimitarBattery;

/// <summary>
/// Tray-friendly monochrome battery icon (outline + fill).
/// percent: 0–100 or null for unknown.
/// Designed to read cleanly at small tray sizes (16/20/24/32).
/// </summary>
public static class BatteryIconFactory
{
    public const int DefaultSize = 32;

    // You can set this once if you ever want black icons (light tray), etc.
    public static Color ForegroundColor { get; set; } = Colors.White;

    public static WindowIcon CreateIcon(int? percent, int size = DefaultSize)
    {
        size = Math.Max(8, size);

        var bmp = new RenderTargetBitmap(new PixelSize(size, size));
        using var ctx = bmp.CreateDrawingContext(true);

        // 1) Thicker-than-logical outline (tray optics > math purity)
        var stroke = Clamp(size * 0.12, 2.0, 3.2);

        // Half-stroke inset keeps edges from getting clipped.
        var inset = Math.Ceiling(stroke / 2.0);

        var totalW = size - inset * 2;
        var totalH = size - inset * 2;

        // Battery proportions tuned for readability
        var bodyH = totalH * 0.56;
        var bodyY = inset + (totalH - bodyH) / 2.0;

        // 5) Chunkier nub
        var nubW = totalW * 0.16;

        // Slight gap between body and nub helps separation after downsampling
        var gap = Math.Max(1.0, totalW * 0.03);
        var bodyW = totalW - nubW - gap;

        var bodyX = inset;
        var nubX = bodyX + bodyW + gap;

        var nubH = bodyH * 0.34;
        var nubY = bodyY + (bodyH - nubH) / 2.0;

        // Keep rounding on the shell only (fill stays square)
        var bodyR = Clamp(bodyH * 0.14, 1.0, 4.0);
        var nubR  = Clamp(nubH * 0.22, 1.0, 3.0);

        var bodyRect = SnapRect(new Rect(bodyX, bodyY, bodyW, bodyH));
        var nubRect  = SnapRect(new Rect(nubX,  nubY,  nubW,  nubH));

        var fg = new SolidColorBrush(ForegroundColor);
        var outlinePen = new Pen(
            fg,
            stroke,
            lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round
        );

        // 1) Solid-ish shell fill (high alpha) so silhouette reads in the tray
        var shellFill = new SolidColorBrush(Color.FromArgb(
            180,
            ForegroundColor.R,
            ForegroundColor.G,
            ForegroundColor.B
        ));

        // Draw shell (body + nub)
        ctx.DrawRectangle(shellFill, outlinePen, bodyRect, bodyR, bodyR);
        ctx.DrawRectangle(shellFill, outlinePen, nubRect,  nubR,  nubR);

        // Draw interior content (fill / unknown)
        DrawFill(ctx, percent, bodyRect, stroke, ForegroundColor);

        return new WindowIcon(bmp);
    }

    private static void DrawFill(DrawingContext ctx, int? percent, Rect bodyRect, double stroke, Color fgColor)
    {
        // 4) Increased inner padding to preserve a clear gutter at small sizes
        var pad = Math.Ceiling(stroke * 1.6);

        var inner = new Rect(
            bodyRect.X + pad,
            bodyRect.Y + pad,
            Math.Max(0, bodyRect.Width - pad * 2),
            Math.Max(0, bodyRect.Height - pad * 2)
        );

        if (inner.Width < 1 || inner.Height < 1)
            return;

        if (!percent.HasValue)
        {
            // Unknown state: simple centered bar (readable at 16px)
            var barW = inner.Width * 0.22;
            var barH = inner.Height * 0.70;

            var barRect = SnapRect(new Rect(
                inner.X + (inner.Width - barW) / 2.0,
                inner.Y + (inner.Height - barH) / 2.0,
                barW,
                barH
            ));

            var unknownBrush = new SolidColorBrush(Color.FromArgb(
                220, fgColor.R, fgColor.G, fgColor.B
            ));

            ctx.DrawRectangle(unknownBrush, null, barRect);
            return;
        }

        var p = Math.Clamp(percent.Value, 0, 100);

        // Monochrome urgency: alpha changes only (no colors)
        byte alpha = p switch
        {
            <= 10 => (byte)255,
            <= 20 => (byte)235,
            _     => (byte)220
        };

        var fillBrush = new SolidColorBrush(Color.FromArgb(alpha, fgColor.R, fgColor.G, fgColor.B));

        var fillW = inner.Width * (p / 100.0);

        // Avoid rendering tiny slivers that look like noise after scaling
        if (fillW < 0.9)
            return;

        var fillRect = SnapRect(new Rect(inner.X, inner.Y, fillW, inner.Height));

        // 3) Fill is rectangular (no rounded corners) to avoid blur/mush in tray
        ctx.DrawRectangle(fillBrush, null, fillRect);

        // Optional small “critical notch” at <=10% to make “low” more obvious
        if (p <= 10)
        {
            var notchW = Math.Max(1.0, Math.Round(inner.Width * 0.06));
            var notchRect = SnapRect(new Rect(inner.Right - notchW, inner.Y, notchW, inner.Height));

            var notchBrush = new SolidColorBrush(Color.FromArgb(
                90, fgColor.R, fgColor.G, fgColor.B
            ));

            ctx.DrawRectangle(notchBrush, null, notchRect);
        }
    }

    private static Rect SnapRect(Rect r)
    {
        // Snap to half pixels to reduce outline blur after rasterization
        double Snap(double v) => Math.Round(v * 2, MidpointRounding.AwayFromZero) / 2.0;
        return new Rect(Snap(r.X), Snap(r.Y), Snap(r.Width), Snap(r.Height));
    }

    private static double Clamp(double v, double min, double max)
        => v < min ? min : (v > max ? max : v);
}
