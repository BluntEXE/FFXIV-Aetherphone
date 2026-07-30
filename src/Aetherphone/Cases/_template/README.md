# Art case spec

An art case is one painted PNG. The plugin draws it underneath everything else, then paints the black
glass band, the screen, the wallpaper, the interface and the hardware buttons on top.

You paint two areas:

- **The metal band** — a 38 px ring around the phone body. Everything further in is covered, so this is
  the only part *on* the phone that shows.
- **The overflow margin** — 250 px of free space all round the body, outside the phone entirely. Charms,
  straps, ears, figures, anything that breaks the rectangle. It draws outside the plugin window and
  passes clicks through, so it costs nothing.

There is no painting behind the screen — the screen is opaque and drawn above you. But the phone is not
the edge of the canvas.

```
  +-------------------------+
  |   overflow margin       |  250 px, free
  |   +-----------------+   |
  |   | ### metal band  |   |  38 px, the visible ring
  |   | #+-----------+# |   |
  |   | #|  screen   |# |   |  engine draws this
  |   | #+-----------+# |   |
  |   +-----------------+   |
  +-------------------------+
```

Every art case shares one template, so a case that follows the guides lines up at all six phone sizes
with no engineering work. Open `ArtCaseTemplate.svg` for the guides; `generate-template.ps1` regenerates
it from `Core/Theme/ChassisMetrics.cs` if those ever change.

## Canvas

**1500 x 2755 px, RGBA PNG, 8 bits per channel, sRGB with the ICC profile stripped.**

The phone body is **1000 x 2255**, inset 250 px from every edge. Hardware buttons stick out past the
body and are drawn by the plugin, so leave no gutter for them.

1000 px of body is a little over 1:1 at the largest possible on-screen size. Higher gains nothing: the
plugin generates no mipmaps, so extra pixels are only ever sampled down. Body aspect varies 0.21% across
the six phone sizes, so the art stretches to fill — never letterbox, never anchor.

## Guides, in canvas pixels

| Guide | Rect | Corner box | Who paints it |
|---|---|---|---|
| Canvas | 0,0 -> 1500,2755 | | |
| Overflow margin | 250 wide, all four sides | | **you, optional** |
| Silhouette (green) | 250,250 -> 1250,2505 | 161.08 | **you** |
| Metal band | 37.98 wide | | **you** |
| Glass edge (blue) | 287.98,287.98 -> 1212.02,2467.02 | 123.10 | plugin |
| Alpha cutout (red) | 297.98,297.98 -> 1202.02,2457.02 | 113.10 | boundary |
| Screen (purple) | 304.11,304.11 -> 1195.89,2450.89 | 106.97 | plugin |

Corners are a superellipse (`|x|^4.2 + |y|^4.2 = box^4.2`), not a circle and not a rounded rectangle.
The corner box is how far along each edge the curve runs before the edge goes straight. Trace the paths
in the SVG; a rounded-rectangle tool will not match and the mismatch shows as a kink against the screen.

## Alpha

- Straight (non-premultiplied) alpha, not premultiplied.
- Alpha 0 inside the red cutout, and anywhere in the margin you leave unpainted.
- The 10 px ring between the blue glass edge and the red cutout is opaque but always covered. Keep it
  flat, no detail, matching the adjacent metal.
- **Bleed the RGB at least 8 px past every alpha edge** — the silhouette, the cutout, and around anything
  in the margin. Exporters that zero RGB under alpha 0 produce black halos once the image is filtered.
  Check by sampling any transparent pixel within 8 px of an opaque one: its RGB must match its neighbour.

## Design constraints

**Paint your own edge light.** The plugin skips its procedural bevel for art cases, so an unlit case
reads flat next to the default chassis. Bright along the top and left, dim along the bottom and right,
roughly 4-8 px.

**Ornament on the band belongs in the four corner boxes.** The straight edges are overlaid by hardware
buttons, which bite about 4 canvas px in at these spans (fractions of the long side):

| Button | Span | Portrait edge | Landscape edge |
|---|---|---|---|
| Mute | 0.205 - 0.287 | left | bottom |
| Side | 0.250 - 0.358 | right | top |
| Lock | 0.315 - 0.397 | left | bottom |

**In camera mode the whole image rotates 90 degrees clockwise.** Overflow art rotates with it, so a charm
hanging off the left in portrait hangs off the bottom in landscape. Make it read either way, or keep
overflow near the corners.

**Nothing narrower than 6 px.** No mipmaps, and the smallest phone samples this canvas down about 3.7:1.

**Fine repeating texture does not fit the band.** It is 38 px; a carbon weave needs a cell finer than
that to read as material, which is both under the aliasing floor and ruinous for file size. Broad,
low-frequency treatments work. **The margin has no such limit** — nothing is drawn over it.

## Export

| | |
|---|---|
| Full skin | `<CaseId>.png`, 1500 x 2755, **budget 650 KB**, hard cap 900 KB |
| Picker thumb | `<CaseId>.thumb.png`, 375 x 689, **budget 100 KB** |
| CaseId | PascalCase ASCII, no spaces. It is the config value and the localisation key suffix. |
| Post-process | `oxipng -o 4 -s` on both files, always |

The biggest lever on file size is authoring within a limited palette so `pngquant --quality 85-95` can
index the result; that can take a case from 800 KB to under 300 KB. It is an authoring decision, not
something fixable at export. Quantise repeated detail to discrete steps rather than smooth ramps, and
keep unpainted areas flat — they exist only to satisfy the bleed.

The loader does not care about resolution, only aspect. A flat or graphic case may ship at 1125 x 2066
or 750 x 1378 if that lands it comfortably under budget.

## Handing a case over

1. Drop `<CaseId>.png` and `<CaseId>.thumb.png` in `src/Aetherphone/Cases/`.
2. Add one line to `ThemeCatalog.BuiltInCases`: `PhoneCase.Art("<CaseId>", <dominant metal colour>)`.
   That colour is the fallback: it fills the minimised phone, the minimise animation and the moment
   before the texture loads, so pick the case's dominant metal tone rather than an accent.
3. Add `catalog.case.<caseid>` to `L.cs` and all nine files in `src/Aetherphone/Localization/`.

The id is permanent — it is both the saved setting and the translation key, so changing it after release
resets everyone who selected that case.

## Reference cases

`generate-case.ps1` produces conforming cases in six styles. Not a substitute for hand-painted art, but
it is exactly what a hand-painted case must match geometrically, and it carries a `-MetalFraction` knob
for previewing what a wider bezel would buy. **Silkie** is the worked example of overflow: a plain shell
with a head and ears above the top edge and a pom-pom on a chain off the left, none of it touching the
body.
