# App icon generator

Generates home-screen app icons into `src/Aetherphone/Icons/` from
[Tabler Icons](https://tabler.io/icons) (MIT). The map no longer covers the
full shipped icon set; see "Known drift" below before assuming a regen is
lossless.

## Run

```sh
cd tools/icon-generator
npm install
npm run build
```

This downloads each mapped Tabler outline SVG, recolors it to white, thickens
the stroke slightly for small-size legibility, and rasterizes it to a 256px
transparent PNG named after the app's `IPhoneApp.Id`.

The icons ship **white on transparent** so the client tints them to the active
theme at runtime (`Windows/Components/AppIconTextures.cs` draws them via
`AddImage(..., tint)`). Most app icons come from a PNG; the procedural art in
`AppIconArt` covers the mini-games plus the Gamba casino app, and any id with
neither falls back to the caller's letter glyph.

## Known drift

The `map` in `generate-app-icons.mjs` (40 entries) is out of sync with the 43
PNGs shipped in `src/Aetherphone/Icons/`:

- Shipped but unmapped: `calculator.png`, `coin.png`, `notes.png`. All three
  belong to live apps, so a full regen leaves those icons untouched.
- Mapped but dead: `contacts`, `findpeople`, `phone` (legacy app ids migrated
  away in `Configuration.cs`) and `kupoai` (no shipped app carries that id).
  A regen recreates these PNGs anyway.

Reconcile the map with the app registry before trusting a full regen.

## Changing an icon

Edit the `map` (app id -> Tabler icon name) in `generate-app-icons.mjs` and
re-run. Browse icon names at https://tabler.io/icons. Avoid `brand-*` icons;
those are trademarked logos.

Pass app ids as arguments to regenerate only those icons:

```sh
node generate-app-icons.mjs messages
```

## License

Tabler Icons is MIT licensed; the notice ships with the plugin in
`THIRD-PARTY-NOTICES.md` at the repo root.
