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

    public static Color ForegroundColor { get; set; } = Colors.White;

    public static WindowIcon CreateIcon(int? percent, int size = DefaultSize)
    {
        size = Math.Max(8, size);

        var bmp = new RenderTargetBitmap(new PixelSize(size, size));
        using var ctx = bmp.CreateDrawingContext(true);

        // Thicker-than-logical outline (tray optics > math purity)
        var stroke = Clamp(size * 0.12, 2.0, 3.2);
        var inset = Math.Ceiling(stroke / 2.0);

        var totalW = size - inset * 2;
        var totalH = size - inset * 2;

        // --- proportions (wider battery look) ---

        // Nub should be slimmer than before
        var nubW = totalW * 0.11;

        // Gap should be small (big gaps make the battery feel narrow)
        var gap = Math.Max(0.5, totalW * 0.015);

        // Body gets the rest (wider silhouette)
        var bodyW = totalW - nubW - gap;
        
        var bodyX = inset;
        var nubX  = bodyX + bodyW + gap;

        // Keep height as you had it (tallness is fine); widen is the goal
        var bodyH = totalH * 0.62;
        var bodyY = inset + (totalH - bodyH) / 2.0;

        // Nub height slightly larger so it feels less “twiggy”
        var nubH = bodyH * 0.34;
        var nubY = bodyY + (bodyH - nubH) / 2.0;


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

        // Hollow outline only (no interior shell fill)
        ctx.DrawRectangle(null, outlinePen, bodyRect, bodyR, bodyR);
        ctx.DrawRectangle(null, outlinePen, nubRect,  nubR,  nubR);

        DrawFill(ctx, percent, bodyRect, stroke, ForegroundColor);

        return new WindowIcon(bmp);
    }

    private static void DrawFill(DrawingContext ctx, int? percent, Rect bodyRect, double stroke, Color fgColor)
    {
        var pad = Math.Ceiling(stroke * 1.25);

        var inner = new Rect(
            bodyRect.X + pad,
            bodyRect.Y + pad,
            Math.Max(0, bodyRect.Width - pad * 2),
            Math.Max(0, bodyRect.Height - pad * 2)
        );

        if (inner.Width < 1 || inner.Height < 1)
            return;

        // Slight translucency so outline dominates at small sizes
        var fillBrush = new SolidColorBrush(Color.FromArgb(
            230, fgColor.R, fgColor.G, fgColor.B
        ));

        if (!percent.HasValue)
        {
            // Unknown state: receding wedge/trapezoid (no dot, no inner contour)
            var wedgeW = inner.Width * 0.45;

            var p0 = new Point(inner.X, inner.Y);
            var p1 = new Point(inner.X + wedgeW, inner.Y);
            var p2 = new Point(inner.X + wedgeW * 0.55, inner.Bottom);
            var p3 = new Point(inner.X, inner.Bottom);

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(p0, isFilled: true);
                g.LineTo(p1);
                g.LineTo(p2);
                g.LineTo(p3);
                g.EndFigure(isClosed: true);
            }

            ctx.DrawGeometry(fillBrush, null, geo);
            return;
        }

        var p = Math.Clamp(percent.Value, 0, 100);
        var fillW = inner.Width * (p / 100.0);

        if (fillW < 0.9)
            return;

        var fillRect = SnapRect(new Rect(inner.X, inner.Y, fillW, inner.Height));
        ctx.DrawRectangle(fillBrush, null, fillRect);
    }


    private static Rect SnapRect(Rect r)
    {
        // Snap to half pixels to reduce blur after rasterization
        double Snap(double v) => Math.Round(v * 2, MidpointRounding.AwayFromZero) / 2.0;
        return new Rect(Snap(r.X), Snap(r.Y), Snap(r.Width), Snap(r.Height));
    }

    private static double Clamp(double v, double min, double max)
        => v < min ? min : (v > max ? max : v);
}
