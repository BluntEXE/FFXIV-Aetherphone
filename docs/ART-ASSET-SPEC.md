# Aetherphone art asset specification

Everything the art team needs to produce assets that drop into the plugin without engineering work.
Three asset types are covered: **app icons**, **phone cases** and **avatar frames**.

If a value here disagrees with the code, the code wins and this document is stale. The authoritative
sources are `Windows/Components/AppIconTextures.cs` for icons, `Core/Theme/ChassisMetrics.cs` plus
`Windows/Components/CaseArt.cs` for cases, and `Windows/Components/AvatarView.cs` for frames.

---

# 1. App icons

| | |
|---|---|
| Canvas | **256 x 256 px** |
| Format | PNG-32, RGBA, 8 bits per channel |
| Colour | **Pure white (255,255,255) on every visible pixel. The shape lives entirely in the alpha channel.** |
| Safe area | Glyph bounding box roughly **84%** of the canvas (about a 20 px margin at 256); the plugin then draws the whole file at **62%** of the tile |
| File name | `<appid>.png`, lowercase, no spaces |
| Location | `src/Aetherphone/Icons/` |
| Size budget | **<= 8 KB** (existing 43 icons run 2.4 - 6.4 KB, mean 4.2 KB) |

## Icons are stencils, not artwork

Every visible pixel in a shipped icon is one RGB value -- white -- and all shape information is carried
by alpha. At draw time the engine multiplies the whole image by a tint colour that follows the theme, so
white pixels come out as the tint and anything else is darkened by it. A full-colour icon therefore
fights the theme instead of following it, and reads wrong on every background the tint is meant to
adapt to.

Think of it the way an iOS template image or a font glyph works: you are authoring a stencil.

- No gradients, no multi-colour marks, no inner colour detail. Depth comes from shape alone.
- Semi-transparent alpha does render, so soft edges and lighter secondary strokes work -- a 50% alpha
  pixel becomes a 50% tint pixel, not a different hue.
- Holes are alpha 0 and show the tile colour through.

## Geometry

The icon is drawn centred at **62% of the app tile** on home tiles (small-tile contexts such as coin
rows, the share sheet and the App Store draw a hair tighter, about 61%). On top of that, **match the
shipped inset**: the whole set keeps Tabler's 2-in-24 margin, so the glyph's bounding box runs roughly
**84% of the canvas** -- about a 20 px margin on each side at 256. Do not fill the canvas edge to edge;
a full-bleed glyph ships about 18% larger than every icon around it and reads oversized next to the set.

## Style

Tabler-derived line iconography: even stroke weight, geometric, functional over branded.

- Stroke weight **24 px** at 256 px (the shipped set measures 24 everywhere), consistent within an
  icon and across the set.
- Rounded caps and joins.
- Optically balanced, not mathematically centred.
- Nothing finer than **10 px**. Icons draw as small as ~30 px and there is no mipmapping.

## Export

1. PNG-32 at 256 x 256, straight (non-premultiplied) alpha.
2. Keep RGB pure white on every pixel that has any alpha, right through the anti-aliased edges. What
   sits under fully transparent pixels does not matter: every shipped icon carries black there (the
   exporter's default) and renders cleanly, so match the set rather than forcing white under alpha 0.
3. Strip the ICC profile.
4. Run `oxipng -o 4 -s`.

Drop the file in `src/Aetherphone/Icons/`; `appid` must match the app's registered id exactly. A
missing icon degrades to a procedural drawing or the app's glyph rather than breaking.

---

# 2. Phone cases

## What a case is

A case is **one PNG**, drawn as a single stretched quad at the very bottom of the render order. The
engine then paints the black glass band, the screen, the wallpaper, the app content, the dynamic
island, the status bar and the hardware buttons on top of it.

Your artwork lives in two places:

- **The metal band** -- a 38 px ring around the phone body. Everything further in is covered by the
  glass and the screen, so the band is the only part *on* the phone that shows.
- **The overflow margin** -- 250 px of free space all round the body, outside the phone entirely. This
  is where charms, straps, ears, figures and any silhouette that breaks the rectangle go. It draws
  outside the plugin window and passes clicks straight through, so it costs nothing.

```
  ┌─────────────────────────┐
  │   overflow margin       │ ← 250 px, free: charms, ears, straps, figures
  │   ┌─────────────────┐   │
  │   │ ▓▓ metal band ▓ │   │ ← 38 px, the visible ring on the phone
  │   │ ▓┌───────────┐▓ │   │
  │   │ ▓│  screen   │▓ │   │ ← engine: glass, screen, wallpaper, apps
  │   │ ▓└───────────┘▓ │   │
  │   └─────────────────┘   │
  └─────────────────────────┘
```

There is no painting *behind* the screen -- the screen is opaque and drawn above you. But the phone is
not the edge of your canvas.

## Summary

| | |
|---|---|
| Canvas | **1500 x 2755 px** |
| Phone body | **1000 x 2255**, inset 250 px from every edge |
| Format | Author as PNG-32, RGBA, 8 bits per channel, sRGB, ICC stripped; after the `pngquant` step below, the shipped file usually ends up palette-indexed, which is expected |
| Alpha | Straight (non-premultiplied) |
| File names | `<CaseId>.png` and `<CaseId>.thumb.png` |
| Thumb canvas | **375 x 689 px** (quarter scale; 2755 / 4 = 688.75, rounded to 689) |
| Size budget | **<= 650 KB** full, **<= 100 KB** thumb |
| Location | `src/Aetherphone/Cases/` |
| Template | `src/Aetherphone/Cases/_template/ArtCaseTemplate.svg` |

1000 px of body is a little over 1:1 at the largest possible on-screen size. Going higher gains
nothing: the plugin generates no mipmaps, so extra pixels are only ever sampled down.

## Guides, in canvas pixels

| Guide | Rect | Corner box | Who paints it |
|---|---|---|---|
| Canvas | 0,0 → 1500,2755 | | |
| Overflow margin | 250 px, all four sides | | **you, optional** |
| Silhouette (green) | 250,250 → 1250,2505 | 161.08 | **you** |
| Metal band | 37.98 wide | | **you** |
| Glass edge (blue) | 287.98,287.98 → 1212.02,2467.02 | 123.10 | engine |
| Alpha cutout (red) | 297.98,297.98 → 1202.02,2457.02 | 113.10 | boundary |
| Screen (purple) | 304.11,304.11 → 1195.89,2450.89 | 106.97 | engine |

**Corners are a superellipse**, not a circle and not a rounded rectangle: `|x|^4.2 + |y|^4.2 = box^4.2`.
The corner box is how far along each edge the curve runs before the edge goes straight. Trace the paths
in `ArtCaseTemplate.svg` -- it is generated from the same formula the engine draws. `generate-template.ps1`
regenerates it if a chassis dimension ever changes.

## Alpha rules

- Alpha 0 everywhere inside the red cutout, and everywhere in the margin you do not paint.
- The 10 px ring between the blue glass edge and the red cutout is opaque but always covered. Keep it
  flat and matching the adjacent metal -- no detail.
- **Bleed RGB at least 8 px past every alpha edge**, on the silhouette, the cutout, and around anything
  in the margin. Exporters that zero RGB under alpha 0 produce black halos once filtered.

## Design constraints

**Paint your own edge light.** The engine skips its procedural bevel for art cases, so an unlit case
reads flat next to the default chassis. Bright along the top and left, dim along the bottom and right,
roughly 4-8 px.

**Ornament on the band belongs in the four corner boxes.** The straight edges are overlaid by hardware
buttons, which bite ~4 px in at these positions (fractions of the long side):

| Button | Span | Portrait edge | Landscape edge |
|---|---|---|---|
| Mute | 0.205 - 0.287 | left | bottom |
| Side | 0.250 - 0.358 | right | top |
| Lock | 0.315 - 0.397 | left | bottom |

**In camera mode the whole image rotates 90 degrees counter-clockwise.** Overflow art rotates with it, so a
charm that hangs off the left in portrait hangs off the bottom in landscape. Design something that
reads either way, or keep overflow near the corners.

**Nothing narrower than 6 px.** No mipmaps, and the smallest phone preset samples this canvas down
about 3.7:1; free resize goes smaller still, to about 4.3:1. Hairlines and fine noise will crawl.

**Fine repeating texture does not fit the band.** It is 38 px; a carbon weave needs a cell finer than
that to read as material, which is both under the aliasing floor and ruinous for file size. Broad,
low-frequency treatments work. The margin has no such limit -- it is as big as you need.

## File size

Smooth shapes compress well; continuous-tone detail does not. The shipped cases run 30-403 KB
against the 650 KB budget, and the most textured one needed its pattern quantised to discrete steps
to fit.

1. **Author within a limited palette** so `pngquant --quality 85-95` can index the result. Much the
   biggest lever, and an authoring decision rather than an export setting.
2. Quantise repeating detail to discrete steps rather than smooth ramps.
3. Keep unpainted areas flat. The margin and the interior exist only to satisfy the 8 px bleed; any
   variation there costs bytes for pixels nobody sees.
4. `oxipng -o 4 -s` on both files, always.
5. If a case still will not fit, ship at 1125 x 2066 or 750 x 1378. The loader is resolution-agnostic
   -- only the aspect ratio is contractual.

## Handing over

1. Drop `<CaseId>.png` and `<CaseId>.thumb.png` into `src/Aetherphone/Cases/`.
2. Name the case's **dominant metal colour**. It stands in for the artwork on the minimised phone,
   during the minimise animation, and before the texture loads, so pick the main body tone rather than
   an accent.
3. Engineering adds one line to `ThemeCatalog.BuiltInCases` and the name to all nine language files.

`CaseId` is PascalCase ASCII, no spaces. It is both the config value and the localisation key suffix,
so it cannot change after release without resetting everyone who selected it.

## Reference implementation

`src/Aetherphone/Cases/_template/generate-case.ps1` generates conforming cases in six styles. Not a
substitute for hand-painted art, but it is exactly what a hand-painted case must match geometrically,
and it carries a `-MetalFraction` knob for previewing what a wider bezel would buy. `Silkie` is the
worked example of overflow art: a plain shell with a head above the top edge and a charm off the left.

---

# 3. Avatar frames

A frame is a ring that sits around a player's round profile picture. It is bought in the Aether Coin
shop, worn one at a time, and drawn over the avatar everywhere that face appears.

| | |
|---|---|
| Canvas | **512 x 512 px**, square. Anything from 128 to 1024 is accepted, square is enforced within a 2% tolerance |
| Format | **PNG-32 or WebP, RGBA.** Animated WebP and APNG are accepted and play in the client. JPEG and GIF are rejected: JPEG has no alpha, and GIF only has the 1-bit kind |
| File size | 2 MB hard cap |
| Centre | **Fully transparent.** The avatar is drawn underneath and shows through |
| Sizes | **One file.** Do not export a size ladder. The client resamples the master down to whatever it draws at |
| Upload | Mod console, Frames page. Name it, drop the file, set the scale. Any filename works; the server renames on store |

## The scale number is the whole geometry

There is one number to get right, and it is set per frame in the console rather than baked into the
client, so it can be retuned without a plugin release.

**Scale** is the frame's drawn width as a percentage of the avatar's diameter.

- At **100** the frame exactly covers the avatar and cannot overhang.
- At **138**, the default, the avatar hole occupies the centre 72% of your canvas and the outer 14%
  on each side is free for decoration that breaks the circle.
- The console clamps to **100 to 200**, and the client clamps again on the way in.

So the rule for the artist: **decide how far your decoration sticks out, then tell the console.**

At the 138 default on a 512 canvas:

| | |
|---|---|
| Avatar hole | Centred circle, **371 px** diameter (72.5% of 512) |
| Decoration margin | **70 px** on every side |
| Hole edge | Keep the inner opening a clean circle. The avatar is a hard-edged circle and any gap shows |

If your design needs a bigger flourish, raise the scale rather than shrinking the hole. Shrinking the
hole crops the face; raising the scale gives you room.

## Rules that come from how it renders

**Centred, always.** Scale controls size, not position. There is no offset knob, so a decoration
weighted to one side still has to sit in a centred canvas with its own margin. A bow on top means
margin on all four sides, not just the top.

**It has to read at 30 px.** The same art is drawn on a profile header and on a chat row. Below a
radius of 15 px the client skips the frame rather than draw it at all. Above that it resamples your
master to the size it is about to draw, so a 512 canvas on a chat row is filtered down properly
rather than point-sampled into a sparkling mess. Filtering is not free detail though: silhouette and
two or three strong colours survive the trip to 30 px, hairline filigree averages into a smudge. The
console previews both sizes side by side while you tune, and the small preview is the one to trust.

**Send the master, not a ladder.** The client picks a power-of-two size that covers the drawn
diameter and downsamples the 512 into it once, in the background. Exporting your own 30, 48, 64 and
96 px versions gains nothing the resampler does not already do, and the extra files have nowhere to
go: the catalog stores one asset per frame.

**Never cover the face.** The avatar exists so people recognise each other. Decoration belongs in the
margin. Art that reaches into the hole, or that is loud enough to win attention from the person
inside it, has failed no matter how good it looks alone.

**Soft edges beat hard ones.** The frame is composited straight over the avatar with no blending
tricks, so a hard alpha cutout against a busy photo reads as a sticker. Feather the outer edge by a
pixel or two.

**Animated frames.** Export an animated WebP or an APNG and upload it like any other frame; the
console marks it Animated and the client plays it wherever the face appears, every copy on screen in
step with one shared clock. Two limits sit on top of the static ones: **512 x 512 at most** and **128
frames at most**, with the 2 MB cap unchanged, so keep the loop short and let the resting pose carry
the design. Every frame stays resident in video memory while the face is on screen (a 256 px loop of
128 frames costs about 32 MB, and the client resamples the loop to the size it draws, same as a
still), which is why the caps are tight, and the 2 MB file cap is what keeps a long 512 px loop out. GIF is refused on purpose: its 1-bit alpha turns a feathered edge
into a jagged cutout. A frame drawn below the 15 px radius floor is skipped, animated or not.

---

# 4. Open decisions

**Off-centre art.** Scale handles size, not position. If a design genuinely cannot sit centred, that
is two more numbers on the frame, and worth asking for rather than working around.

**Full-colour app icons.** Icons are stencils today. Moving to full-colour per-app illustrations is
under discussion; it would replace section 1 entirely and is all-or-nothing, since stencils beside
illustrations read as unfinished.

**Case band width.** The band is 38 px and carries trim-level detail. Comparable plugins use roughly
2.4x that. Widening it would change the canvas and require re-exporting finished cases, so raise it
before production if the art direction depends on a richer band. Note this only affects the band --
the overflow margin is already unconstrained.
