# Bundled sounds

Drop ringtone and notification audio here. Every `.mp3` and `.wav` file in this
folder ships with the plugin and appears in Settings under both **Ringtone**
(plays on incoming calls) and **Notification Sound** (plays on notifications).

The phone plays its own audio only. Game system sounds are never used, so this
folder plus the user's imported files are the entire sound catalog: with no
files here the only option left is **Silent**.

Notes:

- The first file in alphabetical order is the phone's default sound, used for
  fresh installs and whenever a saved choice no longer resolves.
- Playback uses Windows Media Foundation, so `.mp3` and `.wav` are safe choices.
- A file's display name is its file name with `_`/`-` turned into spaces
  (`soft_bell.mp3` shows as "soft bell").
- Ringtones loop until the call is answered or missed, so keep them seamless.
  Notification sounds play once.
- Ship only audio you have the rights to distribute, and add attribution to
  `THIRD-PARTY-NOTICES.md` when a file requires it.
- Users can add their own files from Settings ("Import from PC"); those are
  copied into the plugin config directory's `Sounds` folder and are not bundled.
