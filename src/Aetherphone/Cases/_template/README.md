# Art case spec

An art case is one painted PNG of the phone chassis. The plugin draws it as a single stretched quad and
then paints the black glass band, the screen, the dynamic island, the status bar and the hardware
buttons on top of it. You paint the metal, and nothing else.

Every art case shares this one template. There is no per-case geometry, so a case that follows the
guides will line up at all six phone sizes with no engineering work.

Open `ArtCaseTemplate.svg` for the guides. `generate-template.ps1` regenerates it from the numbers in
`Core/Theme/ChassisMetrics.cs`; run it if those ever change.

## Canvas

**1000 x 2255 px, RGBA PNG, 8 bits per channel, sRGB with the ICC profile stripped.**

The canvas is the phone body, not the plugin window. The hardware buttons stick out past the body and
are drawn by the engine, so they are not your problem and there is no gutter to leave for them.

1000 px is a little over 1:1 at the largest possible on-screen size (XXL phone at 2x UI scale = 961
real px). Going higher only costs memory: the plugin generates no mipmaps, so every extra pixel is
sampled down and gains nothing.

The art stretches to fill. Body aspect varies by 0.21% across the six phone sizes, which is at most
0.8 px of vertical error over a 606 px phone, so do not letterbox and do not anchor.

## Guides, in canvas pixels

| Guide | Rect | Corner box | Who paints it |
|---|---|---|---|
| Silhouette (green) | 0,0 to 1000,2255 | 161.08 | you |
| Metal band | 37.98 wide, all four sides | | you |
| Glass edge (blue) | 37.98,37.98 to 962.02,2217.02 | 123.10 | engine |
| Alpha cutout (red) | 47.98,47.98 to 952.02,2207.02 | 113.10 | boundary |
| Screen (purple) | 54.11,54.11 to 945.89,2200.89 | 106.97 | engine |

Corners are a superellipse (`|x|^4.2 + |y|^4.2 = box^4.2`), not a circle and not a rounded rectangle.
The "corner box" is how far along each edge the curve runs before the edge goes straight. Do not
eyeball it; trace the paths in the SVG, which are generated from the same formula the plugin draws.

## Alpha

- **Straight (non-premultiplied) alpha.** Not premultiplied.
- Alpha 0 outside the silhouette and everywhere inside the red cutout.
- The ring between the blue glass edge and the red cutout is opaque but always covered. Keep it flat,
  no detail, matching the adjacent metal colour.
- **Bleed the RGB at least 8 px past every alpha edge**, both the outer silhouette and the inner
  cutout. Exporters that zero RGB under alpha 0 produce black halos once the image is filtered. Check
  by sampling any transparent pixel within 8 px of an opaque one: its RGB must match its opaque
  neighbour.

## What to paint, and where

The metal band is 38 canvas px wide, uniform on all four sides. That is the whole surface.

The engine skips its procedural bevel for art cases, so **paint your own edge light** or the case will
read flat next to the colour cases. Match the convention: bright along the top and left, dim along the
bottom and right, roughly 4 to 8 canvas px.

**Ornament belongs in the four corner boxes.** The straight edges are where the hardware buttons sit,
and in landscape the whole image rotates 90 degrees clockwise, so an asymmetric edge design reads
differently depending on orientation. Corners are never covered and never ambiguous.

Buttons bite only 4.2 canvas px into the side edges, at these spans (fractions of the long side):

| Button | Span | Portrait edge | Landscape edge |
|---|---|---|---|
| Mute | 0.205 to 0.287 | left | bottom |
| Side | 0.250 to 0.358 | right | top |
| Lock | 0.315 to 0.397 | left | bottom |

**No feature narrower than 6 canvas px.** There are no mipmaps, and an XS phone at 1x UI scale samples
this canvas down about 3.7:1, so hairlines and fine noise will crawl.

## Export

| | |
|---|---|
| Full skin | `<CaseId>.png`, 1000 x 2255, **budget 650 KB**, hard cap 900 KB |
| Picker thumb | `<CaseId>.thumb.png`, 250 x 564, **budget 35 KB** |
| CaseId | PascalCase ASCII, no spaces. It is the config value and the localisation key suffix. |
| Post-process | `oxipng -o 4 -s` on both files, always |

The biggest lever on file size is authoring within a limited palette so `pngquant --quality 85-95` can
index the result; that can take a case from 800 KB to under 300 KB. It is an authoring decision, not
something that can be fixed at export.

The loader does not care about resolution, only aspect. A flat or graphic case may ship at 750 x 1691
or 500 x 1128 with no code change if that lands it comfortably under budget.

## Handing a case over

1. Drop `<CaseId>.png` and `<CaseId>.thumb.png` in `src/Aetherphone/Cases/`.
2. Add one line to `ThemeCatalog.BuiltInCases`: `PhoneCase.Art("<CaseId>", <dominant metal colour>)`.
   That colour is the fallback: it fills the minimised puck, the morph animation and the moment before
   the texture finishes loading, so pick the case's dominant metal tone rather than an accent.
3. Add `catalog.case.<caseid>` to `L.cs` and all nine files in `src/Aetherphone/Localization/`.
