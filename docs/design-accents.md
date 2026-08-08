# Accent colors

Every app tile, header tint, and app palette resolves to one of fourteen colors. This document explains
where those colors come from, why they cannot simply be brightened, and how to add or change one.

## The one rule

**Every accent carries a white glyph at 3:1 or better.** Every tile is a solid accent squircle with a white
glyph, with no exceptions and no per-tile switching. That single constraint drives everything else here.

Two variations were tried and rejected in review: flipping the glyph to dark on light accents (reads as
broken, since neighbouring tiles disagree on ink) and inverting whole tiles to a white body with a colored
glyph (reads as missing artwork at this density). Do not reintroduce either without new evidence.

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

A bright yellow cannot carry a white glyph. `#FFCC00` sits at 1.49:1 against white, less than half the
floor. Holding every accent to 3:1 caps luminance at 0.30, and at that luminance the yellow-green part of
the sRGB gamut simply runs out of chroma: the ceiling across the full hue circle is 0.112, versus 0.30 for
magenta. `Gold` and `Lime` are what that region looks like when it has to stay legible.

This is also why `Teal` and `Cyan` read softer than `Red` or `Orange`. It is a gamut limit, not an
oversight, and brightening them breaks white ink.

## Assignment

`src/Aetherphone/Core/Apps/AppAccents.cs` maps every app id to a ring token. Assignments are not arbitrary:
they satisfy a layout constraint checked by `AccentRingTests`.

- **Neighbours differ by at least 45 degrees.** For every horizontally or vertically adjacent pair in the
  seeded home layout (`HomeLayoutService.DefaultFirstPageApps` and `DefaultSecondPageApps` at
  `Columns` wide), the two accents must be 45 degrees apart in OKLCH. `Slate` is exempt, being neutral.
- **No token repeats within a row or column** of a seeded page.

Adding an app means picking a token that keeps both properties true. Run the tests; they will name the
offending pair and the distance.

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

App backdrops go through `Palette.ShadeToLuminance`, which scales in gamma space to land every backdrop at
the same darkness regardless of how luminous its accent is. A fixed `Darken` factor cannot do that; gold
would sit visibly brighter than azure.

## Changing a color

1. Regenerate rather than hand-edit. A hand-picked value will drift off the luminance target and either
   break white ink or break the family look.
2. Keep the new value at relative luminance 0.285 and at least 22 degrees from every other token.
3. Run `dotnet test src/Aetherphone.Tests/Aetherphone.Tests.csproj`. `AccentRingTests` checks the white
   contrast floor, ring separation, token distinctness, and home layout adjacency.
