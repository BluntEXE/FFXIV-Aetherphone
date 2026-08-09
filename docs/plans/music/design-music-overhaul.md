# Music app overhaul: information architecture, live radio, and station pages

Design document for the Music refactor. Reads on top of `understand-music-app.md`, which holds the
current-state forensics and every file:line reference cited here. No em dashes anywhere in this
document, its copy, or its proposed string literals.

---

## 1. What we are fixing

The Music app carries three unrelated content domains (YouTube song search, the 60,000 station Radio
Browser directory, and our own Community Radio) inside one scrolling Home screen and one push/pop
stack. Everything follows from that:

1. **Community Radio, the feature we most want to grow, is buried.** It is a three row shelf near the
   bottom of Home that hides itself entirely whenever its fetch has not landed.
2. **Tapping a community station can show a blank screen.** The station page has no data source of
   its own and resolves ids against a directory snapshot that is frequently empty. Four separate code
   paths reach the blank branch (dossier section 2).
3. **The station page has no visual identity or hierarchy** and its largest element is a disabled grey
   button (dossier section 3).
4. **Nothing feels live.** Live state is a 30 second poll, live stations are not sorted or surfaced
   separately, `LiveCount` and `LastLiveAtUnix` are computed or carried and never displayed, and the
   app icon never signals that anyone is on air.

### What Spotify actually does, and which parts we are taking

| Spotify pattern | Taking it | Why |
|---|---|---|
| Persistent bottom tabs, each a durable destination with its own back stack and preserved scroll | Yes | The single biggest structural gap. Spotify explicitly treats "tab switch resets scroll" as a bug |
| Mini player docked directly above the tab bar, expanding to a full sheet | Yes, already built, needs to be lifted above the new bar | Our sheet already matches |
| Home as shelves: a quick grid of recents, then horizontal card rows with section headings | Yes | We already have shelves, they just need the right order and a live rail on top |
| Artist/playlist page: full bleed art bleeding into a colour scrim, left aligned title, metadata line, then a horizontal action row whose primary is a filled accent circle | Yes | This is the direct answer to screenshot 2 |
| Browse All: a grid of vividly coloured category tiles | Yes, for the world radio directory | Our category grid is already a 2 column gradient grid, it needs the colour and the corner art |
| Card primitive reused across every surface | Yes | Matches how `AppSkin` already works |
| Three tabs exactly (Home, Search, Library) | No | We have a fourth domain Spotify does not: live human broadcasters |

Sources consulted: [Spotify Newsroom on the tablet experience](https://newsroom.spotify.com/2026-04-16/new-tablet-app-experience/),
[Spotify support: Your Library](https://support.spotify.com/ws/article/your-library/),
[Spotify Community: Android tab scroll position fix](https://community.spotify.com/t5/Community-Blog/Ongoing-Issues-Review-May-2026/ba-p/7443262),
[How Spotify's design system goes beyond platforms](https://www.designsystems.com/how-spotifys-design-system-goes-beyond-platforms/).

---

## 2. Information architecture: four tabs

```
+---------------------------------------------------+
|  content (per tab router stage)                   |
|                                                   |
+---------------------------------------------------+
|  mini player  (56px + margins, animated presence) |
+---------------------------------------------------+
|  [Home]     [Live]     [Radio]     [Library]      |   62px
+---------------------------------------------------+
|  home indicator inset                             |
+---------------------------------------------------+
```

| Tab | Icon | Owns |
|---|---|---|
| **Home** | `Home` | Greeting, Live now rail, Jump back in, Made for you, Your playlists |
| **Live** | `TowerBroadcast` | Community Radio: on-air hero, On air, Up next, Following, All stations |
| **Radio** | `Broadcast` / `Podcast` | World radio: category grid, filters, station browse and paging |
| **Library** | `LayerGroup` | Recently played, Playlists, Favourite stations, Followed stations |

**Search is not a tab.** It is a route pushed onto whichever tab you are in, from a pill on Home and a
search icon in the top bar of Live, Radio and Library. The existing scope chips (Songs / Long plays /
All) gain a Stations and a Community scope, and **the launching tab preselects the scope**: search
from Radio opens on Stations, from Live on Community, from Home or Library on Songs. Back returns you
to the tab you left, at your scroll position.

Rationale for four rather than five: search here is a task, not a browsing destination, and one
unified search across songs, world stations and community stations is better than three separate
boxes. Four cells at a 360 unit design width give 90 units each, which is comfortable for an icon
plus a `Caption1` label. Five would be 72 and would force truncation in German and Russian.

If the Songs domain later grows its own browsing surface (charts, genres, new releases) it becomes
the fifth tab and search moves into it. Nothing in this design blocks that.

---

## 3. Shell mechanics

### 3.1 Layout

`MusicApp.Draw` gains a tab strip below the mini player and shrinks the router stage to match. Order
of operations, replacing `MusicApp.cs:215-229`:

```csharp
var tabHeight = MusicTabBar.Height * scale;                       // 62
var stage = new Rect(content.Min, new Vector2(content.Max.X, content.Max.Y - tabHeight));
using (InputShield.Engage(sheetValue > 0.15f || overlayValue > 0.15f))
{
    routers[(int)tab].Draw(stage, AppSkin.Transparent, delta, drawView);
}

using (InputShield.Engage(overlayValue > 0.15f))
{
    DrawMiniPlayer(stage, scale);          // anchors to stage.Max.Y, not content.Max.Y
    DrawNowPlayingSheet(content, scale, sheetValue, delta);
}

using (InputShield.Engage(sheetValue > 0.15f || overlayValue > 0.15f))
{
    DrawTabBar(new Rect(new Vector2(content.Min.X, stage.Max.Y), content.Max), scale);
}

DrawPlaylistOverlay(content, scale, overlayValue, delta);
```

Shrinking the stage is mandatory, not cosmetic. Per the `router-chrome-layering` finding, chrome
drawn into the parent draw list after `router.Draw` is painted underneath the layer child and ends up
invisible but still clickable; that is exactly the bug the App Store tab bar shipped with. Copy
`AppStoreApp.cs:115-118` verbatim in shape.

`BodyBottom` (`MusicApp.cs:324-328`) keeps its animated mini player inset but is now measured against
the shrunken stage, so it needs no tab term of its own. `DrawMiniPlayer` currently anchors to
`content.Max.Y - margin - height` (`NowPlaying.cs:46-48`) and must take the stage rect instead, or it
will sit exactly on top of the tab bar.

The Now Playing sheet keeps the full `content` rect and deliberately covers the tab bar, matching
Spotify. The `InputShield` wrapper above stops the covered bar from responding.

### 3.2 Per-tab router stacks

Replace the single `ViewRouter<View>` with one per tab:

```csharp
private readonly ViewRouter<View>[] routers =
[
    new(View.Home), new(View.Live), new(View.Radio), new(View.Library),
];
```

Tab switching does **not** reset the stack or the scroll, which is the behaviour Spotify shipped a
fix for and the App Store's `router.Reset()` on tab change gets wrong. Two consequences to handle:

- Wrap the router draw in `ImGui.PushID((int)tab)` so each tab's `AppSurface` lands in its own ImGui
  window family. Per the `appsurface-shared-scroll-id` finding, every surface uses the hardcoded
  child id `##appSurface` and takes its identity entirely from the id chain above it, so without the
  push two tabs at the same depth would share one persisted scroll offset.
- `OnOpened` still calls `Reset()` on every router and returns to the Home tab, so the app always
  reopens at its root. `AppVisits` already resets scroll per fresh visit.

Tapping the active tab again jumps that tab to its root and scrolls to top, as on iOS.

### 3.3 Shared tab bar component

`AppStoreApp.cs:148-182` hand-rolls the icon plus `Caption1` label cell that Music wants;
`BottomTabBar` (the shared component) is icon-only with a tooltip. Add a labelled mode to the shared
component rather than writing a third copy:

```csharp
// src/Aetherphone/Windows/Components/BottomTabBar.cs
public const float Height = 52f;
public const float LabelledHeight = 62f;

public int Draw(Rect bar, AppSkin ui, PhoneTheme theme, ReadOnlySpan<NavTab> tabs, int active,
                bool showLabels = false);
```

Labelled cells draw the icon at `1.02` active / `0.94` idle with the label at `TextStyles.Caption1`
below it, active ink `ui.Accent`, idle `ui.MutedInk`, hover `ui.TitleInk`, exactly as the App Store
bar does today. Migrate `AppStoreApp` onto it in the same change and delete its private
`DrawTabBar`, `TabIcon` and `TabLabel`. `YellowPagesApp` is untouched because `showLabels` defaults
to false.

---

## 4. Screen designs

### 4.1 Home tab

Draw order, replacing `MusicApp.Home.cs:22-48`:

1. Top bar: greeting (`Title2`, left aligned) with a search icon at the right.
2. Search pill (existing `DrawSearchPill`), routing to Search with the Songs scope.
3. **Live now rail** (new, always first, never hidden).
4. **Jump back in**: the existing recent chips grid, kept. It is the one part of Music that already
   looks right. Widen the art slightly and overlay `Equalizer` on the tile that is currently playing.
5. **Made for you**: existing featured shelf with its pulsing skeletons, kept.
6. **Your playlists**: existing shelf, kept.
7. Radio categories and the radio heading **move out** to the Radio tab. Home stops being a dumping
   ground.

**Live now rail** is the centrepiece and the main reason Home changes at all. A single pannable
horizontal rail of 150 by 200 unit cards:

```
+-----------------+  +-----------------+
| [* LIVE 23]     |  | [* LIVE 4]      |     <- pulsing dot + count, accent pill
|                 |  |                 |
|   station art   |  |   station art   |
|   (scrim below) |  |                 |
| AetherPhone FM  |  | Moogle Mix      |     <- FootnoteEmphasized, up to 2 lines
| DROELOE - OATH  |  | Ready for a...  |     <- Caption1 muted, Marquee
+-----------------+  +-----------------+
```

Tapping a card **plays immediately** and does not navigate, matching the rule already established
when the Spotify-style overhaul landed: playing a track mid-slide looked broken, so playback raises
the mini player instead of pushing a route. A small chevron affordance in the card corner opens the
station page.

When nothing is live, the rail does not disappear. It becomes one wide card: "No one is on air right
now" with a subline naming the next scheduled show (`TimeText.FutureMoment`) and a tap target that
opens the Live tab. This directly kills the "I clicked community radio and saw nothing" report from
the Home side.

### 4.2 Live tab: the flagship

Top bar: "Live", a live count chip ("3 on air") when `LiveCount > 0`, and a search icon.

**Hero.** When anything is live, a full width hero roughly 180 units tall for the live station with
the most listeners, pannable across all live stations when there is more than one:

```
+-------------------------------------------------+
|                                                 |
|            station artwork, cover cropped        |
|   [* LIVE  23 listening]                        |   <- accent pill, pulsing dot
|                                                 |
|   AetherPhone FM                          ( > ) |   <- LargeTitle over scrim, 52px accent play
|   (avatar) Hosted by Xeldar (badges)            |
|   Now playing: DROELOE - OATH                   |   <- Marquee
+-------------------------------------------------+
```

The artwork is drawn cover cropped across the full content width with a vertical scrim from
transparent at the top to `BackdropTop` at the bottom, so the text sits on the station's own colour
instead of on flat near-black. This is what gives each station an identity.

When nothing is live the hero does not become an empty box. It shows the next scheduled station's art
at reduced opacity with "Next up: AetherPhone FM, Thursday 20:00" and a Follow action. An off-air
Live tab should still look designed.

**Sections below the hero**, each hidden only when genuinely empty and each preceded by a shelf
heading:

| Section | Contents | Sort |
|---|---|---|
| On air | Every live station | Listeners descending |
| Up next | Stations with `NextBroadcastAtUnix` in the future | Soonest first |
| Following | Off-air stations you follow, with "Last live 3 h ago" from `LastLiveAtUnix` | Most recently live first |
| All stations | Everything else, behind the existing tag `ChipRail` | Alphabetical |

Live rows are 72 units: 56 unit art, name in `BodyEmphasized`, now-playing marquee, and a
`LIVE, n listening` line in accent with the pulsing dot. `Equalizer` renders on the row you are
currently playing. A trailing play button on each row plays without navigating; the row body opens
the station.

Loading, empty and error states go through `EmptyState.Draw` with a retry action, never through a
bare return.

### 4.3 Station page: the screenshot 2 rewrite

Replace the centred column with the Spotify artist/playlist structure. Top to bottom:

1. **Full bleed header, about 200 units.** Station artwork cover cropped across the full content
   width, vertical scrim into `BackdropTop`. Over the bottom of the scrim, left aligned: the live
   pill (`LIVE, 23 listening`) or a muted `OFF AIR` pill, then the station name at `Title1` through
   `Typography.FitScale` so long names shrink rather than truncate.
2. **Host row.** Avatar plus "Hosted by Xeldar" with badges through the existing `UserName.DrawAuto`,
   left aligned at `Subheadline`.
3. **On air now card** (live only). An `ui.Card` holding a small animated `Equalizer`, the label
   "On air now", and the ICY title as a `Marquee`. This becomes the emotional centre of the page and
   replaces the dead grey slab that occupies that role today.
4. **Action row**, horizontal, not a full width slab:
   - Left: Follow / Following ghost pill, and an overflow icon carrying Share and Report.
   - Right: a **filled accent play circle at 52 units** (`MusicRenderer.PlayButton` already draws
     exactly this, with its press spring).
   - When the station is off air the play circle is replaced, not disabled. The primary action
     becomes a bell: "Get notified when they go live", which toggles follow. An off-air page should
     convert a visitor into a follower instead of showing them a dead button.
   - Off-air metadata line under the row: "Last live 3 h ago" or "Next broadcast Thursday 20:00",
     both already available in the DTO and both currently unused.
5. **Tags**: one `ChipRail`, unchanged behaviour.
6. **About**: description at `Subheadline` in `BodyInk`, left aligned.
7. **Links**: replace the manual wrapping chip wall (`Community.cs:701-744`) with a single `ChipRail`
   of icon plus label chips, one per allowed kind (Twitch, YouTube, Discord, Bluesky, X, Ko-fi,
   Patreon). This also brings the page back into line with the repo rule against chip walls.
8. **Last played**: see below.

### 4.4 Last played: the screenshot 1 rewrite

Renamed from "Recently played" (which collides with the Home recents concept) to **"Last played"**,
and demoted:

- **Five rows by default**, not twelve, with a "Show all" row pushing a full history view.
- **Hidden entirely when the station has never been live** (`LastLiveAtUnix == 0`). A station that
  has never broadcast should not show an empty history slot.
- Row design, 44 units:

```
+------+---------------------------------+----------+
| [::] | Written Maze                    | 4 min ago|
| grad | DROELOE, Iris Penning           |          |
+------+---------------------------------+----------+
```

  - A 28 unit rounded square filled with an `ArtGradient` seeded from the track title, so the list has
    colour despite `RadioTrackDto` carrying no artwork.
  - Title and artist split on the first " - " in the ICY title, title at `Subheadline` in `BodyInk`,
    artist at `Caption1` muted. When there is no separator, the whole string is the title.
  - Right aligned **relative** time ("4 min ago") rather than a raw `HH:mm` clock. All clock text
    still goes through `TimeText`; add a `TimeText.PastMoment` counterpart to the existing
    `FutureMoment` if one does not already exist.
  - The first row is marked as currently playing when it matches the live ICY title.
- `community.TracksLoading` finally gets read: a three row skeleton while loading, `EmptyState` when
  a live station has no history yet.

Tapping a row keeps today's behaviour (search for that track), but must not reset the router. With
per-tab routers it pushes Search onto the Live tab instead of resetting to Home.

### 4.5 Radio tab

The existing Stations screen becomes the tab root, and inherits from Home:

- Category grid, restyled as Spotify "Browse all": vivid per category squircle tiles using the
  accent ring, each with a small rotated artwork square bleeding out of the bottom right corner.
- The existing sort dropdown, country and language filter sub-views, search field, `InfiniteScroll`
  paging and `RadioFilter` threading are all kept as they are. They work.

### 4.6 Library tab

`SegmentStrip` at the top (the house pattern, as Photos uses for Library and Albums) over four
sections, or a single scroll with headings if the counts stay small:

- Recently played (the full `SongHistory`, as rows rather than the 6 tile Home grid)
- Playlists (existing)
- Favourite radio stations (existing `DrawFavoriteRadioStationsSection`, promoted out of Home)
- Followed community stations

---

## 5. The liveness system

This is the "more active, vibrant, live" ask, expressed as concrete mechanisms.

**`LivePill`, a new shared component.** One implementation of the pulsing live badge, used on Home
rail cards, Live rows, the hero, the station page and the Live tab chip:

```csharp
// src/Aetherphone/Windows/Components/LivePill.cs
internal static class LivePill
{
    public static float Width(string label, float scale);
    public static void Draw(ImDrawListPtr drawList, Vector2 origin, string label, Vector4 accent,
                            float clock, float scale);
}
```

The dot pulses on a sine of the caller's clock (Music already keeps `clock` in `MusicApp.cs:194`).
This is an ambient opacity pulse, not a transition, so it does not violate the critically damped
motion rule that governs springs.

**Live first ordering.** Live stations sort above off-air ones everywhere they appear, by listener
count. Today they are interleaved in one unsorted list.

**Now playing everywhere.** The ICY title is the strongest possible "this is a person, live, right
now" signal and it is already fetched. Surface it on the Home rail card, the Live row, the hero and
the station page. `NowPlayingFor` (`Community.cs:691-699`) already prefers local ICY metadata over
the polled snapshot when you are the listener; keep that.

**App icon signal.** `MusicApp.BadgeCount` returns the live station count, with `BadgeAsDot` when the
user would rather not see a number. `BadgeCount` is read every frame on the home screen, so
`LiveCount` must stop being a full array scan per access (`CommunityRadioService.cs:38-54`) and
become a field written once per fetch.

**Faster liveness.** Two steps:

- Now: drop the active poll interval from 30s to 15s while the Live tab is foreground, and honour the
  `active` parameter that `Refresh(bool active = true)` currently ignores entirely.
- Later (backend work, separate repo): push a `radio.live` signal over the existing realtime
  connection on the live transition, the same way `social.ping` already rides it, and add a radio
  hook to `RealtimeSignalBus`. The server already detects the transition; it is what fires the
  `RadioLive = 21` notification. This turns the Live tab from polled to instant.

**Promote the service.** Move `CommunityRadioService` out of `MusicApp`'s constructor
(`MusicApp.cs:143`) into `PhoneServices` so its cache survives app close and can be read by the badge
and by anything else that wants live state.

---

## 6. Data layer: killing the blank screen

Four changes, all in `CommunityRadioService` plus one call site. This is the phase that ships first
and alone.

1. **Give the station page its own fetch.** Add `EnsureStation(string id)` backed by the already
   implemented and never called `RadioClient.StationAsync` (`GET /radio/stations/{id}`), with its own
   `StationLoading` and `StationError` state and a one entry cache. `TryFind` falls back to that cache
   when the directory scan misses.
2. **Cache the tapped DTO.** `OpenStationPage(station)` (`Community.cs:56-60`) currently stores only
   `station.Id` and discards the DTO it was handed. Store the DTO as the immediate render source, so
   every tap from Home, the directory and Search paints instantly and never depends on the snapshot.
3. **Do not try to page the directory.** `CommunityStationPage.NextCursor` is always null: the
   backend accepts no cursor and hardcodes `Take(30)`. Lookup by id is the only way to reach a
   station the directory omits, which is exactly what change 1 adds. Raising the cap is a backend
   change, out of scope here.
4. **Render every state.** `DrawStationPage` gets a loading skeleton (header block plus two grey
   rows), and a not-found `EmptyState` with an explicit Retry that clears `retryAfterTick` and calls
   `Refresh(true)`. When the transport returned null because the user is signed out, the message says
   so and offers Settings rather than Retry (`docs/networking.md:236`: a null return covers signed
   out, network error, non 2xx and rate limiting alike, so distinguish on
   `AethernetSession.IsSignedIn` at the call site).

Plus, on the Home side: `DrawCommunitySection` (`Community.cs:94-100`) must stop returning early on
an empty directory. It keeps its heading and renders the Live now rail's empty variant instead.

Cleanups in the same pass: delete dead `ForgetTracks()`, make `Refresh(bool active)` honour its
parameter, and fix `OpenCommunityWithTag` (`Community.cs:45-54`) to switch to the Live tab root and
set the filter rather than popping and conditionally pushing, which currently leaves a stale Search
underneath and boxes the enum every frame.

---

## 7. Convention cleanup carried by the refactor

While the files are open, and because these are the difference between a custom app and part of the
phone:

- Replace hardcoded 6/8/10/12/14/16/18px literals with `Metrics.Space` and `Metrics.Radius` tokens
  times `UiScale.Current`.
- Replace `ImGui.GetContentRegionAvail().X` with `ScrollLayout.StableContentWidth()` in every Music
  scroll body (`Home.cs:417`, `Community.cs:258,386,560,641` and the rest). This is a latent layout
  shake whenever the native scrollbar is in play.
- Route every empty list through `EmptyState.Draw` instead of hand-rolled centred `Typography` pairs.
- Keep the station cover height fixed rather than derived from available width, per the scrollbar
  feedback loop that already bit the Browse grid.

---

## 8. Localization

Every new user-visible string is a `LocString` in `src/Aetherphone/Core/Localization/L.cs` plus the
same key in all nine JSONs (`de, en, es, fr, ja, pt, ru, tr, zh`) in the same commit. Music has 102
keys today; this adds roughly 30 to 40.

New key groups: four tab labels; `LiveNow`, `OnAir`, `UpNext`, `Following`, `AllStations`,
`NoOneOnAir`, `NextUp`, `LastLive`, `LastPlayed`, `ShowAll`, `OnAirNow`, `NotifyWhenLive`,
`StationUnavailable`, `Retry`, `SignInToBrowse`, `LiveCountChip`, plus the relative past-time forms if
`TimeText` needs new ones. `L.Music.OffAir`, `L.Music.ListeningCount`, `L.Music.HostedBy`,
`L.Music.NextBroadcast` and `L.Music.StationFollowers` already exist and are reused.

Note the standing trap: English never loads `en.json` at runtime, so English copy is fixed in `L.cs`
and mirrored into `en.json`, and `Loc.T` must be resolved at draw time, never in a constructor.

---

## 9. Phasing

Each phase is independently shippable and independently revertible.

Status as of 2026-08-09: phases 0 to 4 are built. Deviations from the design above, all deliberate:
the Home community shelf kept live-first rows with the new live pill rather than becoming a
horizontal card rail; the Live tab got its four sections but not the full-bleed hero (the hero
treatment went to the station page, which is where the complaint was); the App Store was not migrated
onto the shared labelled tab bar, because that would change an unrelated app's pixels inside a Music
refactor; and the Metrics token sweep covers new code only, so legacy literals remain in the older
Music files. Phase 5 is untouched.

| Phase | Scope | Touches |
|---|---|---|
| **0. Unblank** (built 2026-08-09) | Section 6: station fetch by id, tapped DTO cache, loading/not-found/signed-out/retry states, Home section stops hiding, single-station play queue | `CommunityRadioService.cs`, `MusicApp.Community.cs`, `RadioClient.cs`, `AppRegistry.cs`. No visual redesign |
| **1. Shell** | Four tabs, per-tab routers, layout insets, mini player re-anchor, `BottomTabBar` labelled mode plus App Store migration, move categories to Radio, new Library tab | `MusicApp.cs`, `MusicApp.Home.cs`, `BottomTabBar.cs`, `AppStoreApp.cs` |
| **2. Live** | `LivePill`, Home live rail, Live tab hero and sections, live-first sorting, app icon live count, 15s foreground poll, service moved to `PhoneServices` | `MusicApp.Community.cs`, `CommunityRadioService.cs`, `PhoneServices.cs`, new `LivePill.cs` |
| **3. Station page** | Full bleed header, action row with accent play circle, On air now card, links as a `ChipRail`, Last played rewrite | `MusicApp.Community.cs` |
| **4. Polish** | Metrics tokens, `StableContentWidth`, `EmptyState` everywhere, Radio category tile restyle, Library sections | Music files broadly |
| **5. Backend** | `radio.live` realtime push and a `RealtimeSignalBus` radio hook | `FFXIV-Aethernet` repo plus a small client subscriber |

Phase 0 is worth shipping on its own this week: it is the reported bug, it is contained, and it does
not depend on any design decision above it.

---

## 10. Verification

**Build and tests**

```bash
dotnet build Aetherphone.sln -c Release
dotnet test src/Aetherphone.Tests/Aetherphone.Tests.csproj
```

Dalamud loads the dev plugin from `bin/Release`, so a Release build is the one that matters in game.

**Localization lockstep**: confirm every new `L.Music.*` key exists in all nine JSONs before commit.

**Manual matrix for the blank screen** (each one currently reproduces the bug):

1. Sign out, open Music, tap Community Radio. Expect a signed-out `EmptyState`, not a blank body.
2. With the API unreachable, open a station from Home. Expect a not-found state with Retry, not a
   blank body for 60 seconds.
3. Fire a `RadioLive` (type 21) notification and tap it cold, with Music closed. Expect a loading
   skeleton then the station, not a bare title bar.
4. Open a station, then let a background refresh land that no longer contains it. Expect the page to
   keep rendering from the cached DTO.
5. With more than one directory page on the server, deep link to a station on page two. Expect it to
   resolve.

**Manual matrix for the shell**

6. Scroll Home down, switch to Live, switch back. Expect Home still scrolled.
7. Push a station page in Live, switch to Radio and back. Expect the station page still on top.
8. Start playback and confirm the mini player sits above the tab bar with no overlap at 100 percent
   and at 200 percent UI scale.
9. Open the Now Playing sheet and confirm the tab bar is covered and inert.
10. Tap the active tab and confirm it returns to that tab's root and scrolls to top.

**Liveness**

11. Go live from a source client and confirm the Home live rail, the Live tab hero, the station page
    and the app icon badge all reflect it within one poll interval.
12. Go off air and confirm every surface degrades to the designed off-air state rather than an empty
    one.

---

## 11. Open decisions

| Decision | Recommendation |
|---|---|
| Four tabs or five (Songs as its own tab) | Four. Revisit if the Songs domain grows a browsing surface beyond search |
| Keep the station track history at all | Keep, demoted to five rows behind "Show all" and hidden for stations that have never broadcast. The data is cheap and "what did they play" is a real question, it just must not dominate the page |
| Extract dominant colour from station artwork for the page scrim | Not in this pass. Use the existing `ArtGradient` hue seed and the artwork scrim. Revisit once `ImageProcessor` has a cheap average-colour path |
| Live chat in the Live tab | Out of scope here. Discovery is the bottleneck, not interaction, and the prior analysis reached the same conclusion |
