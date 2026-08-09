# Music app: current state and defect forensics

Research dossier for the Music app overhaul. Everything here is read from the working tree on branch
`test` (2026-08-09). File:line references are current as of that read. No em dashes anywhere.

Companion document: `design-music-overhaul.md`.

---

## 1. What the app is today

| File | Role |
|---|---|
| `src/Aetherphone/Apps/Music/MusicApp.cs` | App shell: `View` enum, single `ViewRouter<View>`, draw order, top bar, thumb/cover helpers, layout math |
| `src/Aetherphone/Apps/Music/MusicApp.Home.cs` | Greeting, fake search pill, recent chips, Made for you shelf, favorite stations, radio heading, category grid |
| `src/Aetherphone/Apps/Music/MusicApp.Search.cs` | Real search field, scope chips (Songs / Long plays / All), song rows, community matches |
| `src/Aetherphone/Apps/Music/MusicApp.Community.cs` | Community directory, station page, my-station entry, track history, tag rails, links, report |
| `src/Aetherphone/Apps/Music/MusicApp.MyStation.cs` | Owner editor (name, description, tags, schedule, artwork, credentials) |
| `src/Aetherphone/Apps/Music/MusicApp.NowPlaying.cs` | Mini player strip and the Now Playing bottom sheet, both ImGui child overlays |
| `src/Aetherphone/Apps/Music/MusicApp.Playlists.cs` | Playlist shelf, playlist detail, add-to-playlist overlay |
| `src/Aetherphone/Apps/Music/MusicRenderer.cs` | Bespoke painters: `Cover`, `PlayButton`, `Slider`, `ChevronDown` |
| `src/Aetherphone/Core/Radio/CommunityRadioService.cs` | Community directory cache, follow toggle, track history, poll cadence |
| `src/Aetherphone/Core/Radio/RadioService.cs` | Radio Browser directory (60k stations), categories, filters, paging |
| `src/Aetherphone/Core/Playback/PlaybackHub.cs` | Facade over `RadioPlayer` and `SongPlayer` (mutually exclusive queues) |
| `src/Aetherphone/Core/Aethernet/Clients/RadioClient.cs` | Six community radio endpoints |

Navigation is a **single push/pop stack**, no tabs:

```
View : byte { Home, Stations, Search, CountryFilter, LanguageFilter,
              PlaylistDetail, Community, Station, MyStation, StationArtwork }
```

`MusicApp.cs:154` constructs `new ViewRouter<View>(View.Home)`; `MusicApp.cs:219` draws it. Every
destination is a push from Home, so Community Radio sits two levels down a scroll from the app root.

Draw order (`MusicApp.cs:191-229`) is: backdrop, router (input-shielded), mini player, Now Playing
sheet, playlist overlay. The mini player and the sheet are their own `ImRaii.Child` windows, which is
why they land above router content.

Bottom inset today is mini player only (`MusicApp.cs:324-328`):

```csharp
private float BodyBottom(Rect content, float scale)
{
    var inset = (MiniHeight + MiniMargin * 2f) * scale
              * Math.Clamp(miniPresence.Value, 0f, 1f);   // 56 + 16, animated
    return content.Max.Y - inset;
}
```

---

## 2. The blank community station screen

**Root cause: the station page owns no data.** It re-resolves the station id against the directory
snapshot every frame, and when the scan misses it paints a title bar and returns.

`src/Aetherphone/Apps/Music/MusicApp.Community.cs:347-357`

```csharp
private void DrawStationPage(in PhoneContext context)
{
    community.EnsureFresh(true);
    var station = ViewedStation();
    if (station is null)
    {
        DrawTopBar(context, Loc.T(L.Music.CommunityRadio), PopToCommunity);
        return;                       // nothing else is drawn: this is the empty screen
    }
```

`ViewedStation()` (`Community.cs:67-70`) calls `CommunityRadioService.TryFind`
(`CommunityRadioService.cs:224-238`), a linear scan of the in-memory `stations` array and nothing else.

Four distinct ways to reach that branch:

**A. Notification deep link races the fetch.** `MusicApp.cs:172-178` pushes `View.Station`
synchronously while `EnsureFresh(true)` is still in flight. Until `GET /radio/stations` returns,
`stations` is `Array.Empty<>` and the user stares at a bare title bar.

**B. The fetch failed, and failure is silent for a full minute.** `FetchAsync`
(`CommunityRadioService.cs:257-285`) sets `retryAfterTick = now + 60s` on a null page or an exception
and leaves `stations` empty. `EnsureFresh` then early-returns on every frame for that minute
(`CommunityRadioService.cs:59-74`). Per `docs/networking.md:236`, a `null` return also covers
**signed out**: `AethernetTransport` short-circuits to `default` when `Session.IsSignedIn` is false.
So a signed-out player tapping Community Radio gets a permanently blank screen with no explanation.

**C. The directory is capped at 30 and cannot be paged, so a station outside it is unreachable.**
`FetchAsync` reads `page.Items` and ignores `CommunityStationPage.NextCursor`, but the cursor was
never the answer: the backend accepts no cursor parameter and hardcodes
`Take(RadioLimits.DirectoryPageSize)` (30) then returns `new RadioStationPage(items, null)`
(`FFXIV-Aethernet/Aethernet.Api/Endpoints/RadioEndpoints.cs:20-38`). `NextCursor` is dead on the
wire. Client-side paging is therefore impossible, and lookup by id is the only way to resolve a
station the directory omits.

**D. A background refresh can blank an open page.** `DrawStationPage:351` calls `EnsureFresh(true)`
every frame, and `FetchAsync:264` swaps the whole array. If the refreshed list drops the station, a
fully rendered page goes blank between frames while the user is reading it.

**The fix already exists at the transport layer and is not called.**
`src/Aetherphone/Core/Aethernet/Clients/RadioClient.cs:19-23` implements
`GET /radio/stations/{id}` returning a single `CommunityStationDto`. Grep confirms **zero call
sites** anywhere in the plugin.

Contributing: `OpenStationPage(station)` (`Community.cs:56-60`) stores only `station.Id` and throws
away the DTO it was handed. The Home shelf (`Community.cs:128`), the directory (`Community.cs:155`)
and search matches (`Search.cs:124`) all funnel through it, so all three depend on the snapshot
surviving.

### Second flavour of "I saw nothing"

`DrawCommunitySection` (`Community.cs:94-100`) returns early when `community.Stations.Length == 0`,
so the entire "Community Radio" heading disappears from Home during the first fetch and after any
failure. A player who tapped where the section used to be, or who opened Music cold, sees no
community radio entry point at all.

### Other missing states

| Location | Gap |
|---|---|
| `Community.cs:352-357` | Station page: no loading, no error, no not-found, no retry |
| `Community.cs:241-253` | Directory empty state is a hand-rolled centred text pair, not `EmptyState` |
| `Community.cs:617-623` | Track history returns early on empty; `community.TracksLoading` exists (`CommunityRadioService.cs:176`) and is read by nothing |
| `Community.cs:498-501` | Disabled "Listen live" pill with no reason given |
| `CommunityRadioService.cs:76-86` | `Refresh(bool active = true)` ignores its parameter entirely |
| `CommunityRadioService.cs:191-196` | `ForgetTracks()` is dead code |
| `Community.cs:45-54` | `OpenCommunityWithTag` pops once then conditionally pushes; reached from Search it leaves the stack as `[Home, Search, Community]`, so Back lands on a stale search. Boxes the enum every frame it runs |

---

## 3. Why the two screenshots read as unfinished

### Screenshot 1: the station "Recently played" list

`MusicApp.Community.cs:617-636` (`DrawRecentTracks`) and `:638-669` (`DrawTrackRow`).

Each row is exactly two draws: `Typography.FitText(track.Title, ..., Subheadline)` in `BodyInk` on
the left, `TimeText.Clock(track.PlayedAtUnix)` in `Caption2` muted on the right. No artwork (the
`RadioTrackDto` carries none, only `Title` and `PlayedAtUnix`), no artist split, no separators, no
index, no now-playing marker, no play affordance. Twelve rows at 34px is the tallest and densest
block on the page, and the section header is a bare `Caption1` muted label rather than a shelf
heading.

The emphasis is inverted: a radio station's history matters far less than what is on right now, and
the history is what dominates the page.

### Screenshot 2: the station page

`DrawStationPage` (`Community.cs:347-380`) then `DrawStationHeadline` (`:382-407`),
`DrawStationActions` (`:475-517`), `DrawStationBody` (`:558-593`).

The page is a centred column on a flat near-black field: a 132px square cover, a centred `Title2`
name, a centred host row, a centred status line, one full-width 42px pill, a left-aligned paragraph,
then a wrapping wall of grey chips. Specific problems:

- **The largest element is a dead control.** `DrawStationActions:498-501` redraws the "Listen live"
  pill as a disabled slab when `!station.IsLive`, with no reason text and no alternative action. On
  an off-air station the visual centre of gravity is a grey rectangle that does nothing.
- **No identity.** `AppPalettes.Music` is `Neutral(...)` (`AppPalettes.cs:82`), a fixed near-black
  backdrop with the accent used only at 10 percent bloom. The station's own artwork is a small square
  and contributes nothing to the page colour, so every station page looks identical.
- **Centre alignment defeats hierarchy.** Four consecutive centred blocks of different sizes give the
  eye no left edge to scan.
- **The links are a chip wall.** `DrawStationLinks` (`:701-744`) manually wraps `ui.Chip` calls across
  lines, which contradicts `CLAUDE.md:43` ("One pannable ChipRail for chip rows, never a wrapping
  chip wall").

### Fields the DTO carries and the UI never shows

`CommunityStationDto` (`RadioDtos.cs:5-28`) includes `LastLiveAtUnix`, `CreatedAtUnix`, `Slug`, and
`RepeatsWeekly` (public page). `LastLiveAtUnix` in particular is exactly the value an off-air station
needs to feel like a real station rather than a dead listing.

`CommunityRadioService.LiveCount` (`:38-54`) is implemented and displayed nowhere.

---

## 4. Liveness is invisible and slow

- Live state and listener counts come from polling only: `ActiveIntervalMilliseconds = 30_000`,
  `IdleIntervalMilliseconds = 300_000`, `RetryIntervalMilliseconds = 60_000`
  (`CommunityRadioService.cs:8-10`). `RealtimeSignalBus` has no radio hooks, so there is no push
  channel and the snapshot can be 30 seconds stale on the best path.
- `RadioPlayer` exposes `State`, `CurrentStation`, `NowPlaying` (ICY `StreamTitle`) and
  `CurrentStationInfo`, but **no live flag and no listener count**; those live only on the DTO
  (`RadioPlayer.cs:46-63`).
- Live and off-air stations are interleaved in one unsorted directory list.
- `MusicApp.BadgeCount` never reflects anyone being on air, so the phone home screen gives no signal.
- `CommunityRadioService` is constructed inline by `MusicApp` (`MusicApp.cs:143`) rather than
  registered in `PhoneServices`, so its cache dies with the app instance and nothing outside Music
  can read live state.

---

## 5. House-convention drift inside Music

These are cheap to fix while restructuring and they are the difference between "custom app" and
"part of the phone".

| Convention | Source | Music today |
|---|---|---|
| Spacing and radius tokens times `UiScale.Current` | `docs/ui-toolkit.md:65-81` | Hardcoded 6/8/10/12/14/16/18px literals throughout |
| `ScrollLayout.StableContentWidth()` inside scroll regions | `docs/ui-toolkit.md:160-162` | `ImGui.GetContentRegionAvail().X` at `Home.cs:417`, `Community.cs:258,386,560,641` and others |
| `EmptyState.Draw` for empty lists | `docs/ui-toolkit.md:226` | Never used; hand-rolled centred pairs at `Home.cs:399-412`, `Search.cs:152-161`, `Community.cs:241-253` |
| One pannable `ChipRail`, never a chip wall | `CLAUDE.md:43` | `DrawStationLinks` wraps chips manually |

---

## 6. Existing assets the overhaul should reuse

**Bottom navigation already exists.** `src/Aetherphone/Windows/Components/BottomTabBar.cs` (119
lines): `NavTab(FontAwesomeIcon Icon, string Label, int Badge, bool Raised, string? AnchorKey)`,
`const float Height = 52f`, `int Draw(Rect bar, AppSkin ui, PhoneTheme theme, ReadOnlySpan<NavTab>
tabs, int active)`. It is icon-only (label goes to the hover tooltip) with a spring hover pill,
`ActivityBadge` per tab and an optional raised FAB slot. Sole consumer is `YellowPagesApp`
(fields `:53-54`, layout `:218-240`, dispatch `:242-271`).

**The persistent-chrome layout is proven.** `AppStoreApp.cs:115-118` shrinks the router stage and
draws its bar in the freed strip:

```csharp
var stage = new Rect(content.Min, new Vector2(content.Max.X, content.Max.Y - TabBarHeight * scale));
router.Draw(stage, AppSkin.Transparent, delta, drawView);
DrawTabBar(new Rect(new Vector2(content.Min.X, stage.Max.Y), content.Max), scale);
```

This is mandatory, not stylistic: per the `router-chrome-layering` finding, chrome drawn into the
parent draw list after `router.Draw` is painted underneath the layer child and is invisible while
still being clickable. `AppStoreApp.cs:148-182` also has the icon-plus-`Caption1`-label tab cell that
Music wants, hand-rolled rather than shared.

**Reusable painters already in the toolkit**: `Equalizer.Draw` (3 animated bars),
`PlayBadge.Draw` (filled circle with play/pause glyph), `MediaGlyph`, `TransportButton`, `Scrubber`,
`Marquee.DrawLeft/DrawCentered`, `LoadingPulse`, `ArtGradient`/`ArtworkCache`, `Elevation`,
`Squircle`, `ChipRail`, `SegmentStrip`, `InfiniteScroll`, `AvatarView`, `UserName.DrawAuto`,
`EmptyState`, `TimeText.FutureMoment`/`FutureDayLabel`.

**Endpoints available**

| Method | Endpoint | Called from |
|---|---|---|
| `StationsAsync` | `GET /radio/stations` | `CommunityRadioService.FetchAsync:261` (cursor dropped) |
| `StationAsync` | `GET /radio/stations/{id}` | **nowhere** |
| `MineAsync` / `UpdateMineAsync` | `GET` / `PUT /radio/mine` | My Station editor |
| `TracksAsync` | `GET /radio/stations/{id}/tracks` | `FetchTracksAsync:202` |
| `FollowAsync` | `POST` / `DELETE /radio/stations/{id}/follow` | `FollowAsync:135` |
