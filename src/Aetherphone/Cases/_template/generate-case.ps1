# Generates a template-conformant art case PNG (plus its picker thumb).
# Reference implementation of the spec in README.md: it is what a hand-painted case must match
# geometrically. Hand-painted art replaces these; the geometry does not change.
#
#   .\generate-case.ps1 -Id Ironworks -Style Tech   -Top '#8a97a6' -Bottom '#252c36' -Accent '#5ad2ff'
#   .\generate-case.ps1 -Id Emberforge -Style Ornate -Top '#e0b356' -Bottom '#6b4415' -Accent '#123054'
#   .\generate-case.ps1 -Id Voidsent  -Style Neon   -Top '#2a1f4a' -Bottom '#0d0a1c' -Accent '#d94ae8'

param(
    [Parameter(Mandatory = $true)][string]$Id,
    [Parameter(Mandatory = $true)][ValidateSet('Tech', 'Ornate', 'Neon', 'Armor', 'Weave', 'Stone')][string]$Style,
    [Parameter(Mandatory = $true)][string]$Top,
    [Parameter(Mandatory = $true)][string]$Bottom,
    [Parameter(Mandatory = $true)][string]$Accent,

    # Exploration knobs. MetalFraction is the band width as a fraction of window width and must match
    # ChassisMetrics.ArtMetalFraction for anything that ships; override it only to preview what a wider
    # bezel would buy. OutDir keeps such previews out of the shipped Cases folder.
    [double]$MetalFraction = 0.0365,
    [string]$OutDir
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
    const int Canvas = 1500;
    const int CanvasHeight = 2755;
    // Free space around the phone for charms, ears, straps and figures. The body itself stays 1000 x 2255.
    const int Margin = 250;
    const int BodyX1 = Canvas - Margin - 1;
    const int BodyY1 = CanvasHeight - Margin - 1;
    const double RailFraction = 0.0195;
    const int Samples = 3;

    static double Metal, Glass, BodyBox, Bleed;

    // Inside when (1-a)^4.2 + (1-b)^4.2 <= 1, with a,b the normalised position inside the corner box.
    // This is the closed form of the curve Squircle.BuildUnitCorner traces.
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
                if (Inside(px + (sx + 0.5) / Samples, py + (sy + 0.5) / Samples, x0, y0, x1, y1, box)) { hits++; }
            }
        }
        return (double)hits / (Samples * Samples);
    }

    // How far the point sits inside the silhouette, measured along the inward normal of the squircle
    // family rather than to the rect edges, so ornament bands stay parallel to the curve at the corners.
    // Straight regions are exact; corner boxes bisect the inset that puts the point on the curve.
    static double InsetDepth(double x, double y)
    {
        double cornerX = Math.Min(x - Margin, BodyX1 - x);
        double cornerY = Math.Min(y - Margin, BodyY1 - y);
        if (cornerX < 0.0 || cornerY < 0.0) { return Math.Min(cornerX, cornerY); }
        if (cornerX >= BodyBox || cornerY >= BodyBox) { return Math.Min(cornerX, cornerY); }

        double low = 0.0, high = BodyBox;
        for (int step = 0; step < 14; step++)
        {
            double mid = (low + high) * 0.5;
            double box = BodyBox - mid;
            double a = box <= 0.0 ? 1.0 : (cornerX - mid) / box;
            double b = box <= 0.0 ? 1.0 : (cornerY - mid) / box;
            double f = Math.Pow(1.0 - Math.Min(a, 1.0), Exponent) + Math.Pow(1.0 - Math.Min(b, 1.0), Exponent) - 1.0;
            if (f < 0.0) { low = mid; } else { high = mid; }
        }
        return low;
    }

    static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

    // Soft band centred on c with half-width w, used for grooves, inlays and neon runs.
    static double Band(double t, double c, double w)
    {
        double d = Math.Abs(t - c) / w;
        return d >= 1.0 ? 0.0 : (1.0 - d * d) * (1.0 - d * d);
    }

    static double Pulse(double v, double period, double duty)
    {
        double phase = v / period;
        phase -= Math.Floor(phase);
        return phase < duty ? 1.0 : 0.0;
    }

    struct Rgb
    {
        public double R, G, B;
        public Rgb(double r, double g, double b) { R = r; G = g; B = b; }
        public static Rgb Of(Color c) { return new Rgb(c.R, c.G, c.B); }
        public Rgb Mix(Rgb o, double k) { return new Rgb(R + (o.R - R) * k, G + (o.G - G) * k, B + (o.B - B) * k); }
        public Rgb Lift(double k)
        {
            if (k >= 0.0) { return new Rgb(R + (255 - R) * k, G + (255 - G) * k, B + (255 - B) * k); }
            return new Rgb(R * (1.0 + k), G * (1.0 + k), B * (1.0 + k));
        }
    }

    static Rgb Shade(string style, double x, double y, double depth, Rgb top, Rgb bottom, Rgb accent)
    {
        double t = Clamp01(depth / Metal);
        Rgb c = top.Mix(bottom, Clamp01((y - Margin) / (BodyY1 - Margin)));

        // Perimeter parameter: run along the long axis on the sides, the short axis on top and bottom.
        double cornerX = Math.Min(x - Margin, BodyX1 - x);
        double cornerY = Math.Min(y - Margin, BodyY1 - y);
        bool vertical = cornerX < cornerY;
        double run = vertical ? y - Margin : x - Margin;
        bool inCorner = cornerX < BodyBox && cornerY < BodyBox;
        double cornerReach = inCorner ? Clamp01(1.0 - Math.Max(cornerX, cornerY) / BodyBox) : 0.0;

        if (style == "Tech")
        {
            c = c.Lift(0.55 * Band(t, 0.05, 0.10));            // outer chamfer catch-light
            c = c.Lift(-0.55 * Band(t, 0.36, 0.09));           // panel seam
            c = c.Lift(-0.45 * Band(t, 0.82, 0.09));           // inner seam
            c = c.Lift(0.18 * Band(t, 0.62, 0.14));            // raised centre rail
            double runLit = inCorner ? 1.0 : Pulse(run, 260.0, 0.62);
            c = c.Mix(accent, 0.85 * Band(t, 0.50, 0.075) * runLit);
            c = c.Mix(accent, 0.30 * Band(t, 0.50, 0.20) * runLit);
            if (!inCorner) { c = c.Mix(accent, 0.75 * Band(t, 0.20, 0.055) * Pulse(run + 90.0, 260.0, 0.09)); }
            c = c.Mix(accent, 0.55 * cornerReach * Band(t, 0.50, 0.28));
        }
        else if (style == "Ornate")
        {
            c = c.Lift(0.62 * Band(t, 0.06, 0.11));            // gold outer bevel
            c = c.Lift(0.34 * Band(t, 0.93, 0.09));            // gold inner lip
            double inlay = Band(t, 0.50, 0.30);
            c = c.Mix(accent, 0.92 * inlay);                   // dark enamel channel
            c = c.Lift(-0.30 * Band(t, 0.29, 0.06));
            c = c.Lift(-0.30 * Band(t, 0.71, 0.06));
            if (!inCorner)
            {
                double rune = Pulse(run, 118.0, 0.30) * Band(t, 0.50, 0.20);
                c = c.Lift(0.55 * rune);                        // engraved ticks in the channel
            }
            double gem = Clamp01(1.0 - Math.Max(cornerX, cornerY) / (BodyBox * 0.42));
            c = c.Mix(accent, 0.85 * gem);
            c = c.Lift(0.75 * Clamp01(gem * 2.0 - 1.2));       // gem catch-light
        }
        else if (style == "Armor")
        {
            // Rugged shell: two rails around a recessed grip channel, thickening into corner bumpers.
            c = c.Lift(0.30 * Band(t, 0.10, 0.16));
            c = c.Lift(0.22 * Band(t, 0.90, 0.13));
            c = c.Lift(-0.45 * Band(t, 0.50, 0.26));
            if (!inCorner)
            {
                double rib = Pulse(run, 74.0, 0.46) * Band(t, 0.50, 0.24);
                c = c.Lift(0.34 * rib);                        // grip ribs
            }
            double bumper = Clamp01(1.0 - Math.Max(cornerX, cornerY) / (BodyBox * 0.72));
            c = c.Lift(0.20 * bumper);
            c = c.Mix(accent, 0.80 * Clamp01(bumper * 1.9 - 1.0));
            c = c.Mix(accent, 0.55 * Band(t, 0.50, 0.07) * cornerReach);
        }
        else if (style == "Weave")
        {
            // Carbon twill as crossed diagonals rather than woven cells. A cell grid needs to be finer
            // than the 38px band to read as carbon, which puts it under the aliasing floor; crossed
            // sines give the same weave at a frequency the band can actually carry. Keyed to x and y,
            // because a weave runs in a fixed direction across the material, not around the frame.
            // Quantised to discrete steps: continuous tone is the worst case for PNG and pushed this
            // case past its size budget on its own. Posterised threads also read more like carbon.
            double diag = Math.Sin((x + y) * 0.26) + Math.Sin((x - y) * 0.26);
            c = c.Lift(0.095 * Math.Round(diag));
            double sheen = Math.Exp(-Math.Pow(((x + y) % 900.0 - 450.0) / 220.0, 2.0));
            c = c.Lift(0.26 * sheen);                          // diagonal gloss sweep
            c = c.Lift(0.40 * Band(t, 0.06, 0.10));
            c = c.Mix(accent, 0.85 * Band(t, 0.50, 0.045));    // lacquered pinstripe
            c = c.Lift(-0.35 * Band(t, 0.95, 0.07));
        }
        else if (style == "Stone")
        {
            // Marble: veins from summed sines of position, so they wander instead of repeating.
            double vein = Math.Sin(x * 0.0113 + Math.Sin(y * 0.0071) * 3.1)
                        + 0.62 * Math.Sin(y * 0.0169 + 1.3)
                        + 0.28 * Math.Sin((x + y) * 0.0091 - 0.7);
            c = c.Lift(-0.42 * Clamp01(1.0 - Math.Abs(vein) / 0.20));
            c = c.Lift(-0.14 * Clamp01(1.0 - Math.Abs(vein - 1.1) / 0.45));
            c = c.Mix(accent, 0.90 * Band(t, 0.05, 0.09));     // gold leaf outer edge
            c = c.Mix(accent, 0.70 * Band(t, 0.95, 0.07));     // gold leaf inner edge
            c = c.Lift(0.30 * Band(t, 0.50, 0.30));            // polished crown
        }
        else
        {
            c = c.Lift(0.30 * Band(t, 0.05, 0.09));
            c = c.Lift(-0.40 * Band(t, 0.45, 0.16));           // recessed trough the neon sits in
            double runLit = inCorner ? 1.0 : Pulse(run, 340.0, 0.52);
            c = c.Mix(accent, 0.95 * Band(t, 0.27, 0.055) * runLit);
            c = c.Mix(accent, 0.38 * Band(t, 0.27, 0.17) * runLit);
            double inner = inCorner ? 1.0 : Pulse(run + 170.0, 340.0, 0.40);
            c = c.Mix(accent, 0.90 * Band(t, 0.70, 0.05) * inner);
            c = c.Mix(accent, 0.30 * Band(t, 0.70, 0.16) * inner);
            c = c.Mix(accent, 0.50 * cornerReach);
        }

        // Directional edge light the engine no longer draws for art cases: bright top and left,
        // dim bottom and right.
        double rim = Band(t, 0.0, 0.16);
        bool bright = x - Margin <= BodyX1 - x && y - Margin <= BodyY1 - y;
        c = c.Lift(bright ? rim * 0.30 : -rim * 0.28);

        // Past the cutout these pixels are invisible and exist only to bleed colour outward. Collapsing
        // them to the flat gradient keeps the bleed intact and stops fine ornament from tripling the
        // file size on 85% of the canvas that nobody ever sees.
        // Inward, ornament must survive the whole band before it can flatten. Outward, only the 8px bleed
        // matters, so it collapses almost immediately -- otherwise a fine pattern is computed across the
        // entire overflow margin and the file size doubles for pixels that are fully transparent.
        var fade = depth < 0.0
            ? Clamp01((-depth - 8.0) / 12.0)
            : Clamp01((depth - (Metal + Bleed + 8.0)) / 18.0);
        return fade <= 0.0 ? c : c.Mix(top.Mix(bottom, Clamp01((y - Margin) / (BodyY1 - Margin))), fade);
    }

    public static void Bake(string fullPath, string thumbPath, string style, int topArgb, int bottomArgb,
        int accentArgb, double metalFraction)
    {
        // Chassis fractions are of the phone body, which is the canvas minus the overflow margin.
        double span = 1.0 - 2.0 * RailFraction;
        double bodyWidth = Canvas - 2.0 * Margin;
        Metal = metalFraction / span * bodyWidth;
        Glass = 0.0155 / span * bodyWidth;
        BodyBox = 0.1548 / span * bodyWidth;
        Bleed = 10.0;

        double cutX0 = Margin + Metal + Bleed, cutY0 = Margin + Metal + Bleed;
        double cutX1 = BodyX1 - Metal - Bleed, cutY1 = BodyY1 - Metal - Bleed;
        double cutBox = BodyBox - Metal - Bleed;

        Rgb top = Rgb.Of(Color.FromArgb(topArgb));
        Rgb bottom = Rgb.Of(Color.FromArgb(bottomArgb));
        Rgb accent = Rgb.Of(Color.FromArgb(accentArgb));

        var bitmap = new Bitmap(Canvas, CanvasHeight, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, Canvas, CanvasHeight);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        byte[] buffer = new byte[stride * CanvasHeight];

        for (int y = 0; y < CanvasHeight; y++)
        {
            for (int x = 0; x < Canvas; x++)
            {
                // Colour is written for every pixel, including fully transparent ones, so it bleeds past
                // both alpha edges and bilinear filtering cannot produce halos.
                Rgb c = Shade(style, x, y, InsetDepth(x, y), top, bottom, accent);
                double body = Coverage(x, y, Margin, Margin, BodyX1, BodyY1, BodyBox);
                double cut = Coverage(x, y, cutX0, cutY0, cutX1, cutY1, cutBox);

                int index = y * stride + x * 4;
                buffer[index + 0] = (byte)Math.Round(Clamp01(c.B / 255.0) * 255.0);
                buffer[index + 1] = (byte)Math.Round(Clamp01(c.G / 255.0) * 255.0);
                buffer[index + 2] = (byte)Math.Round(Clamp01(c.R / 255.0) * 255.0);
                buffer[index + 3] = (byte)Math.Round(Clamp01(body * (1.0 - cut)) * 255.0);
            }
        }

        Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        bitmap.UnlockBits(data);
        bitmap.Save(fullPath, ImageFormat.Png);
        bitmap.Dispose();

        // Box-filtered by hand: GDI+ interop is not reachable from an inline compile, and averaging
        // channels straight is correct because RGB is valid even under alpha 0.
        const int Shrink = 4;
        int thumbWidth = Canvas / Shrink, thumbHeight = CanvasHeight / Shrink;
        var thumb = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format32bppArgb);
        var thumbData = thumb.LockBits(new Rectangle(0, 0, thumbWidth, thumbHeight), ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        byte[] thumbBuffer = new byte[thumbData.Stride * thumbHeight];

        for (int y = 0; y < thumbHeight; y++)
        {
            for (int x = 0; x < thumbWidth; x++)
            {
                int b = 0, g = 0, r = 0, a = 0;
                for (int sy = 0; sy < Shrink; sy++)
                {
                    for (int sx = 0; sx < Shrink; sx++)
                    {
                        int src = (y * Shrink + sy) * stride + (x * Shrink + sx) * 4;
                        b += buffer[src + 0]; g += buffer[src + 1]; r += buffer[src + 2]; a += buffer[src + 3];
                    }
                }

                int count = Shrink * Shrink;
                int dst = y * thumbData.Stride + x * 4;
                thumbBuffer[dst + 0] = (byte)(b / count);
                thumbBuffer[dst + 1] = (byte)(g / count);
                thumbBuffer[dst + 2] = (byte)(r / count);
                thumbBuffer[dst + 3] = (byte)(a / count);
            }
        }

        Marshal.Copy(thumbBuffer, 0, thumbData.Scan0, thumbBuffer.Length);
        thumb.UnlockBits(thumbData);
        thumb.Save(thumbPath, ImageFormat.Png);
        thumb.Dispose();
    }
}
'@

$references = @(
    [System.Drawing.Bitmap].Assembly.Location,
    [System.Drawing.Color].Assembly.Location,
    [System.Runtime.InteropServices.Marshal].Assembly.Location
) | Sort-Object -Unique

Add-Type -TypeDefinition $source -ReferencedAssemblies $references

function ToArgb([string]$hex) {
    $clean = $hex.TrimStart('#')
    $r = [Convert]::ToInt32($clean.Substring(0, 2), 16)
    $g = [Convert]::ToInt32($clean.Substring(2, 2), 16)
    $b = [Convert]::ToInt32($clean.Substring(4, 2), 16)
    return ([int](0xFF -shl 24)) -bor ($r -shl 16) -bor ($g -shl 8) -bor $b
}

$target = if ($OutDir) { $OutDir } else { Split-Path -Parent $PSScriptRoot }
$full = Join-Path $target "$Id.png"
$thumb = Join-Path $target "$Id.thumb.png"

[CaseBaker]::Bake($full, $thumb, $Style, (ToArgb $Top), (ToArgb $Bottom), (ToArgb $Accent), $MetalFraction)

"{0,-12} {1,-7} {2,7:N0} KB   thumb {3,5:N0} KB" -f $Id, $Style, ((Get-Item $full).Length / 1KB),
    ((Get-Item $thumb).Length / 1KB)
