# Mass SCD Creator

**Put your own music into Final Fantasy XIV.**

FFXIV stores its sound in a format called `.scd`. To hear your own track in the game, it has to be converted
into that format and then placed in a [Penumbra](https://github.com/xivdev/Penumbra) mod, which tells the game
to play your file instead of the original one.

Mass SCD Creator does both parts. Give it an MP3 and it hands you a working Penumbra mod entry. Give it a
folder of two hundred tracks and it does the same for all of them in one go.

It is a Windows app. It does not touch the game, and it does not need the game to be running.

---

## Getting started

1. Open the [Releases](https://github.com/AEBus/MassSCDCreator/releases) page.
2. Download the latest `.zip` and extract it anywhere you like.
3. Run `MassSCDCreator.exe`.

On the first run the app may offer to download `ffmpeg` for you. Say yes - it is a one-time step, it goes into
the app's own `Tools` folder, and after that you can forget it exists. (`ffmpeg` is what does the actual audio
conversion. You only need it if you are converting something that is not already an OGG file.)

## Your first track

Say you want `battle-theme.mp3` to replace a piece of music in the game.

1. Leave the mode on **Single file** and pick your MP3.
2. Open **Penumbra** and tick **Export to Penumbra**.
3. Choose the mod folder you want the track to go into.
4. Fill in the **game path** - the file inside the game that your track replaces, for example
   `music/ffxiv/bgm_town_01.scd`. If the mod already contains music, the app reads the paths it is already
   using and offers them in a dropdown, so usually you can just pick one.
5. Press **Start**.

That is it. The `.scd` is built, copied into the mod, and added to the mod's playlist. Enable the mod in
Penumbra and you will hear your track in place of the original.

## The three modes

| Mode | What it does |
| --- | --- |
| **Single file** | One audio file in, one `.scd` out. |
| **Audio folder** | Every supported file in a folder, in one run. Tick `Include subfolders` to go deeper. |
| **Refresh SCD library** | Takes `.scd` files you already have and rebuilds them in place - useful if they were made with older settings or if the loop data is wrong. |

## Where the files end up

By default the `.scd` files go **straight into your Penumbra mod and nowhere else**. No stray copies are left
on your disk. That is why the Penumbra export has to be switched on.

If you would rather keep the files yourself - to archive them, or to use them outside Penumbra - tick
**Also save the SCD files to a folder** and choose where. With that on, the Penumbra export becomes optional,
and you can use the app without Penumbra at all.

You need one of the two. If neither is set the files would have nowhere to go, so **Start** stays greyed out
and tells you why.

`Refresh SCD library` is the exception: it rewrites your existing files where they already are, so it never
asks for an output location.

## Sound quality

Three profiles, pick one:

| Profile | What happens |
| --- | --- |
| **Recommended** | Re-encodes through ffmpeg at VBR quality 7. Good for almost everything. |
| **Custom** | The same, but you choose the VBR quality (1-10) or a fixed bitrate. |
| **Original OGG** | No re-encoding at all. Your OGG file is packaged into the `.scd` exactly as it is. |

**Recommended** and **Custom** always produce stereo at 44.1 kHz - that is what the game expects.
**Original OGG** keeps whatever your source file has, since nothing is re-encoded.

**Original OGG** only accepts `.ogg` input. In folder mode, other audio files are skipped and listed in the
log so you can see what was left out.

### Loudness

By default the app evens out the volume of every track to roughly -14 LUFS, so a quiet track and a loud one do
not jump at you when the game switches between them.

If you would rather keep each file exactly as loud as it already is, untick **Normalize loudness**. This only
applies where ffmpeg is used, so **Original OGG** is never touched either way.

Whichever you choose, the log states the settings at the start of every run, so you can always check what was
actually applied.

### Looping

Game music usually loops. Leave **Loop the whole track** on and the app writes loop data covering the whole
file. Turn it off for one-shot sounds that should play once and stop.

### Why it plays in stereo

An `.scd` is more than the audio inside it. The header, track, layout and attribute data around it tell the
game how to play the sound, and getting those wrong is what makes a converted track come out in mono even
though the audio itself is stereo.

The **Recommended template** is not an approximation of the format. Its values were lifted from real game
`.scd` files that do play in stereo, picked out after going through a lot of them. That is what the template
is for, and why it is the default.

Use **Custom template.scd** only if you have a specific file whose structure you need to match.

## Supported input formats

`.mp3`, `.flac`, `.ogg`, `.wav`, `.m4a`, `.aac`, `.opus`, `.wma`, `.aiff`, `.aif`, `.mp4`, `.m4b`

Output is always `.scd`.

## Working with Penumbra mods

The app can create a new playlist in a mod, or add tracks to a playlist that is already there.

When you pick **Append to an existing playlist**, the app reads the mod and lists the playlists it finds,
with the number of tracks in each. Groups that hold no music are left out, so you are only offered places
where a track actually belongs. Picking one fills in the folder those tracks already live in, which you can
still change if you want the new files somewhere else.

This works the same for both mod layouts, so you do not have to know or care which one you have:

- **Older mods (v3)** keep each playlist in its own `group_*.json` file.
- **Newer mods (v4)**, introduced with Penumbra 1.7, keep every playlist inside the mod's `meta.json`.

A few things worth knowing, because they concern your files:

- **An old mod stays old.** The app will never convert a v3 mod to the v4 layout. That is Penumbra's job, and
  it will do it itself when you update. If you are deliberately staying on an older Penumbra, nothing here
  will pull the rug out from under you.
- **Your file comes back the way it arrived.** Adding a track only adds a track. Everything else in the file
  is left alone - including settings the app itself does not understand, and details like indentation and
  line endings. A one-track change stays a one-track change.
- **A backup is made before every edit**, named `<file>.massscdcreator-<timestamp>.bak`. The three most recent
  are kept and older ones are cleaned up. Backups made by Penumbra itself are never touched.

## If something goes wrong

The **Operation log** at the bottom of the window records every step, including exactly which encoder settings
were used. It is the first place to look.

A few common ones:

- **Start is greyed out.** The line next to it says what is missing - usually no input file yet, or neither an
  output folder nor a Penumbra export.
- **Nothing was converted in folder mode.** If you are on **Original OGG**, only `.ogg` files are processed;
  the log lists everything that was skipped.
- **ffmpeg errors.** Let the app install ffmpeg for you, or point it at your own `ffmpeg.exe` in the audio
  section.
- **The track does not play in game.** Check the game path - that is what decides which original sound your
  file replaces. Also make sure the mod is enabled in Penumbra and sits in a collection that is actually
  applied.
- **Odd results from one specific file.** Try a plain `.wav` or `.mp3` first to rule out the source file.

## For developers

```powershell
dotnet restore .\MassSCDCreator.slnx
dotnet build .\MassSCDCreator.slnx -c Release
```

Publish a release build:

```powershell
dotnet publish .\MassSCDCreator\MassSCDCreator.csproj -c Release -r win-x64 --self-contained false
```
