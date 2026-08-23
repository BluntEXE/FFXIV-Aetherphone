# Accent colors

Every app tile, header tint, and app palette resolves to one of seventeen built-in accents: the
fourteen ring tokens below plus the three brand colors in `BrandAccents` (see Brand exceptions).
Users can also set an arbitrary custom hex accent (`ThemeCatalog.IsCustomAccent`) on top of those;
the tile path shades it to legibility (see The one rule). This document explains where those colors
come from, why they cannot simply be brightened, and how to add or change one.

## The one rule

**Every accent carries a white glyph at 3:1 or better.** Every tile is a solid accent squircle with a white
glyph, with no exceptions and no per-tile switching. That single constraint drives everything else here.

Two variations were tried and rejected in review: flipping the glyph to dark on light accents (reads as
broken, since neighbouring tiles disagree on ink) and inverting whole tiles to a white body with a colored
glyph (reads as missing artwork at this density). Do not reintroduce either without new evidence.

`IconTile.Surface` is the normaliser for tints that never went through the ring: it shades any accent
down to `AccentRing.TileLuminance`, so white always reads on the result, while ring accents already sit
at that luminance and pass through untouched. The routed paths use it for you: `SettingsRow` icon tiles,
`ShortcutArt`, and the coin and app rows built on `IconTile.DrawApp` all shade through `Surface` before
filling. This is a convention, not a machine-enforced gate: `IconTile.Draw` fills whatever tint it is
handed, and some call sites do pass unshaded accents today. When you draw a tile, shade the fill with
`IconTile.Surface` and paint the glyph `AccentRing.Ink` rather than passing a raw tint straight to a fill.

## The ring

`src/Aetherphone/Core/Theme/AccentRing.cs` holds thirteen chromatic accents plus a neutral `Slate`.
They are generated, not eyeballed:

| Property | Value | Why |
| --- | --- | --- |
| Hue spacing | at least 22 degrees apart in OKLCH | below that, two tiles read as the same color |
| Relative luminance | 0.285 for all thirteen | fixes white-glyph contrast at 3.13:1 everywhere |
| Chroma | 94 percent of the sRGB gamut edge at that luminance | as vivid as the gamut allows |

Because luminance is identical across the ring, **hue is the only variable between tiles**. That is what
makes the set read as one family instead of a bag of unrelated colors.

| Token | Hex | Token | Hex |
| --- | --- | --- | --- |
| Rose | `#F95589` | Teal | `#21A29D` |
| Red | `#F95C53` | Cyan | `#219FB6` |
| Orange | `#E1731D` | Azure | `#1F96F1` |
| Gold | `#BE871D` | Indigo | `#728AF9` |
| Lime | `#809C1D` | Violet | `#A778F9` |
| Green | `#21A837` | Orchid | `#EC42F8` |
| Emerald | `#21A47D` | Slate | `#8A8F9C` |

### Why there is no bright yellow

Contrast, not chroma. A bright yellow cannot carry a white glyph: `#FFCC00` sits at 1.51:1 against
white, barely half the floor. Holding every accent to 3:1 caps relative luminance at 0.30, and `Gold`
and `Lime` are what the yellow region looks like once it is darkened enough to stay legible.

The gamut squeeze lives elsewhere. At `TileLuminance` the sRGB gamut pinches around teal and cyan: on
the shipped ring `Teal` carries 0.104 chroma and `Cyan` 0.107, against 0.278 for `Orchid` (`Gold` and
`Lime` sit higher, at 0.130 and 0.148). That is why `Teal` and `Cyan` read softer than `Red` or
`Orange`. It is a gamut limit, not an oversight, and brightening them breaks white ink.

## Assignment

`src/Aetherphone/Core/Apps/AppAccents.cs` maps every app id to a ring token. Assignments are not arbitrary:

- **Neighbours differ by at least 45 degrees.** For every horizontally or vertically adjacent pair in the
  seeded home layout (`HomeLayoutService.DefaultFirstPageApps` and `DefaultSecondPageApps` at
  `Columns` wide), the two accents must be 45 degrees apart in OKLCH. `Slate` is exempt, being neutral.
  `AccentRingTests.DefaultLayoutNeverPutsLikeColorsSideBySide` checks this pair by pair.
- **No token repeats within a row or column** of a seeded page. The shipped layout satisfies this today,
  but no test checks it; keep it true by hand when you rearrange tiles.

Adding an app means picking a token that keeps both properties true. Run the tests; they check the white
contrast floor, ring separation, token distinctness, and home layout adjacency, and an adjacency failure
names the offending pair and the distance.

## Brand exceptions

`src/Aetherphone/Core/Theme/BrandAccents.cs` holds identities that predate the ring and are kept off it:

| App | Hex |
| --- | --- |
| Chirper | `#2985F0` |
| Velvet | `#E51A5B` |
| Aethergram | `#EB4C61` |

These still clear the 3:1 white-glyph floor, so ink stays uniform, but they do not honour ring hue
spacing: Velvet and Aethergram sit close together deliberately. `AppAccents.IsBrandLocked` marks them and
the adjacency test skips pairs where both sides are brand-locked. Do not fold them into `AccentRing`, and
do not add new entries here without a real brand reason.

## Derived palettes

`AppPalettes.Tinted(accent)` builds all fourteen `AppPalette` fields from a single accent, so in-app chrome
always matches the tile. `AppPalettes.Neutral(accent)` is the variant for apps with dark neutral chrome
(News, Music, Calculator, Clock) that use the accent only as a highlight. Notes and Calendar stay
theme-driven because they support light mode.

App backdrops go through `Palette.ShadeToLuminance`, which linearises the sRGB channels, scales them in
linear light, and re-encodes, landing every backdrop at the same darkness regardless of how luminous its
accent is. A fixed `Darken` factor (a gamma-space lerp toward black) cannot do that; gold would sit
visibly brighter than azure.

## Changing a color

1. Regenerate rather than hand-edit. A hand-picked value will drift off the luminance target and either
   break white ink or break the family look.
2. Keep the new value at relative luminance 0.285 and at least 22 degrees from every other token.
3. Run `dotnet test src/Aetherphone.Tests/Aetherphone.Tests.csproj`. `AccentRingTests` checks the white
   contrast floor, ring separation, token distinctness, and home layout adjacency.
