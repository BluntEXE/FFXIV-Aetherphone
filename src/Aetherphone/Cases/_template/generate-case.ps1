# Generates a template-conformant art case PNG (plus its picker thumb) from three colours.
# Reference implementation of the spec in README.md: it is what a hand-painted case must match
# geometrically. Hand-painted art replaces these; the geometry does not change.
#
#   .\generate-case.ps1 -Id Ironworks -Top '#6b7280' -Bottom '#374151' -Accent '#93c5fd'

param(
    [Parameter(Mandatory = $true)][string]$Id,
    [Parameter(Mandatory = $true)][string]$Top,
    [Parameter(Mandatory = $true)][string]$Bottom,
    [Parameter(Mandatory = $true)][string]$Accent
)

[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class CaseBaker
{
    const double Exponent = 4.2;
    const int Canvas = 1000;
    const int CanvasHeight = 2255;
    const double RailFraction = 0.0195;
    const int Samples = 3;

    // Inside the shape when (1-a)^4.2 + (1-b)^4.2 <= 1, with a,b the normalised position inside the
    // corner box. This is the closed form of the curve Squircle.BuildUnitCorner traces.
    static bool Inside(double x, double y, double x0, double y0, double x1, double y1, double box)
    {
        if (x < x0 || y < y0 || x > x1 || y > y1) { return false; }
        if (box <= 0.0) { return true; }
        double a = 1.0, b = 1.0;
        if (x < x0 + box) { a = (x - x0) / box; } else if (x > x1 - box) { a = (x1 - x) / box; }
        if (y < y0 + box) { b = (y - y0) / box; } else if (y > y1 - box) { b = (y1 - y) / box; }
        if (a >= 1.0 || b >= 1.0) { return true; }
        return Math.Pow(1.0 - a, Exponent) + Math.Pow(1.0 - b, Exponent) <= 1.0;
    }

    static double Coverage(int px, int py, double x0, double y0, double x1, double y1, double box)
    {
        int hits = 0;
        for (int sy = 0; sy < Samples; sy++)
        {
            for (int sx = 0; sx < Samples; sx++)
            {
                double x = px + (sx + 0.5) / Samples;
                double y = py + (sy + 0.5) / Samples;
                if (Inside(x, y, x0, y0, x1, y1, box)) { hits++; }
            }
        }
        return (double)hits / (Samples * Samples);
    }

    static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

    public static void Bake(string fullPath, string thumbPath, int topArgb, int bottomArgb, int accentArgb)
    {
        double span = 1.0 - 2.0 * RailFraction;
        double metal = 0.0365 / span * Canvas;
        double glass = 0.0155 / span * Canvas;
        double bodyBox = 0.1548 / span * Canvas;
        double bleed = 10.0;

        double cutX0 = metal + bleed, cutY0 = metal + bleed;
        double cutX1 = Canvas - metal - bleed, cutY1 = CanvasHeight - metal - bleed;
        double cutBox = bodyBox - metal - bleed;

        Color top = Color.FromArgb(topArgb), bottom = Color.FromArgb(bottomArgb), accent = Color.FromArgb(accentArgb);

        var bitmap = new Bitmap(Canvas, CanvasHeight, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, Canvas, CanvasHeight);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        byte[] buffer = new byte[stride * CanvasHeight];

        for (int y = 0; y < CanvasHeight; y++)
        {
            double vertical = (double)y / (CanvasHeight - 1);
            for (int x = 0; x < Canvas; x++)
            {
                // RGB is computed for every pixel, including fully transparent ones, so the colour
                // bleeds past both alpha edges and bilinear filtering cannot produce halos.
                double r = top.R + (bottom.R - top.R) * vertical;
                double g = top.G + (bottom.G - top.G) * vertical;
                double b = top.B + (bottom.B - top.B) * vertical;

                double fromLeft = x, fromRight = Canvas - 1 - x;
                double fromTop = y, fromBottom = CanvasHeight - 1 - y;
                double edge = Math.Min(Math.Min(fromLeft, fromRight), Math.Min(fromTop, fromBottom));

                // Edge light: bright along top and left, dim along bottom and right, per the spec.
                double rim = Clamp01(1.0 - edge / 7.0);
                bool brightSide = fromLeft <= fromRight && fromTop <= fromBottom;
                double lift = brightSide ? rim * 0.55 : -rim * 0.30;
                r += (255 - r) * Math.Max(lift, 0.0) + r * Math.Min(lift, 0.0);
                g += (255 - g) * Math.Max(lift, 0.0) + g * Math.Min(lift, 0.0);
                b += (255 - b) * Math.Max(lift, 0.0) + b * Math.Min(lift, 0.0);

                // Accent glow in the four corner boxes, which are the only region never overlaid by a
                // hardware button in either orientation.
                double cornerX = Math.Min(x, Canvas - 1 - x);
                double cornerY = Math.Min(y, CanvasHeight - 1 - y);
                if (cornerX < bodyBox && cornerY < bodyBox)
                {
                    double reach = 1.0 - Math.Max(cornerX, cornerY) / bodyBox;
                    double glow = Clamp01(reach) * Clamp01(1.0 - edge / metal) * 0.7;
                    r += (accent.R - r) * glow;
                    g += (accent.G - g) * glow;
                    b += (accent.B - b) * glow;
                }

                double body = Coverage(x, y, 0, 0, Canvas - 1, CanvasHeight - 1, bodyBox);
                double cut = Coverage(x, y, cutX0, cutY0, cutX1, cutY1, cutBox);
                double alpha = body * (1.0 - cut);

                int index = y * stride + x * 4;
                buffer[index + 0] = (byte)Math.Round(Clamp01(b / 255.0) * 255.0);
                buffer[index + 1] = (byte)Math.Round(Clamp01(g / 255.0) * 255.0);
                buffer[index + 2] = (byte)Math.Round(Clamp01(r / 255.0) * 255.0);
                buffer[index + 3] = (byte)Math.Round(Clamp01(alpha) * 255.0);
            }
        }

        Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        bitmap.UnlockBits(data);
        bitmap.Save(fullPath, ImageFormat.Png);

        int thumbWidth = Canvas / 4, thumbHeight = CanvasHeight / 4;
        var thumb = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(thumb))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(bitmap, 0, 0, thumbWidth, thumbHeight);
        }
        thumb.Save(thumbPath, ImageFormat.Png);
        thumb.Dispose();
        bitmap.Dispose();
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing, System.Drawing.Primitives

function ToArgb([string]$hex) {
    $clean = $hex.TrimStart('#')
    $r = [Convert]::ToInt32($clean.Substring(0, 2), 16)
    $g = [Convert]::ToInt32($clean.Substring(2, 2), 16)
    $b = [Convert]::ToInt32($clean.Substring(4, 2), 16)
    return ([int](0xFF -shl 24)) -bor ($r -shl 16) -bor ($g -shl 8) -bor $b
}

$outDir = Split-Path -Parent $PSScriptRoot
$full = Join-Path $outDir "$Id.png"
$thumb = Join-Path $outDir "$Id.thumb.png"

[CaseBaker]::Bake($full, $thumb, (ToArgb $Top), (ToArgb $Bottom), (ToArgb $Accent))

"{0,-14} {1,8:N0} KB   thumb {2,6:N0} KB" -f $Id, ((Get-Item $full).Length / 1KB), ((Get-Item $thumb).Length / 1KB)
