# Mass SCD Creator

Windows WPF application for creating or refreshing FFXIV `.scd` files from common audio formats, with optional Penumbra playlist export.

## What It Does

`Mass SCD Creator` supports three workflows:

1. `Single file`
   Create one `.scd` from one audio file.
2. `Audio folder`
   Convert every supported audio file in a folder into `.scd`, optionally including subfolders.
3. `Refresh SCD library`
   Repair or rebuild existing `.scd` files in place, either by keeping the current structure or matching them to a template.

The app can also:

- convert audio to OGG Vorbis through `ffmpeg`
- rebuild loop and marker metadata
- preserve or replace SCD structure depending on mode
- copy finished `.scd` files into a Penumbra mod
- create or append a Penumbra playlist JSON

## Supported Input Formats

The app currently accepts:

- `.mp3`
- `.flac`
- `.ogg`
- `.m4a`
- `.wav`
- `.aac`
- `.wma`
- `.opus`
- `.aiff`
- `.aif`
- `.mp4`
- `.m4b`

Output is always Vorbis-based `.scd`.

## Build

Requirements:

- Windows
- .NET SDK 8
- `ffmpeg.exe` available in `PATH`, downloaded by the app into `Tools\ffmpeg`, or selected manually in the UI

Build:

```powershell
cd E:\GitHub\MassSCDCreator
dotnet restore .\MassSCDCreator.slnx
dotnet build .\MassSCDCreator.slnx -c Release
```

Run from source:

```powershell
dotnet run --project .\MassSCDCreator\MassSCDCreator.csproj -c Debug
```

## Publish

Default release target is Windows `win-x64`, framework-dependent.

Publish manually:

```powershell
dotnet publish .\MassSCDCreator\MassSCDCreator.csproj -c Release -r win-x64 --self-contained false
```

Or use the included profile:

```powershell
dotnet publish .\MassSCDCreator\MassSCDCreator.csproj -c Release -p:PublishProfile=FolderProfile
```

Expected output:

- published executable: `MassSCDCreator.exe`
- publish folder: `MassSCDCreator\bin\Release\net8.0-windows\win-x64\publish\`

## FFmpeg Behavior

The app does not ship the FFmpeg binaries inside the repository, but it can work in three ways:

1. use `ffmpeg.exe` found in `Tools\ffmpeg`
2. use `ffmpeg.exe` found in `PATH`
3. use a manually selected `ffmpeg.exe`

On startup, the app can offer to download FFmpeg into `Tools\ffmpeg` if nothing usable is found.

## Penumbra Export

After generating `.scd` files, the app can optionally export them into an existing Penumbra mod.

Supported flows:

- create a new `group_XXX_*.json` playlist
- append new tracks to an existing `group_XXX_*.json` playlist

Export behavior:

- copies generated `.scd` files into a relative folder inside the selected mod root
- writes `Files` mappings for one or more game paths
- creates a new `Single` group or appends options to an existing `Single` group

Append mode notes:

- select the existing playlist JSON explicitly
- append mode currently supports only Penumbra `Single` groups
- duplicate track names are auto-renamed to keep the resulting JSON valid

## Workflow Summary

### Single File

1. Choose `Single file`.
2. Pick one supported audio file.
3. Pick the output `.scd` path.
4. Choose a built-in template or a custom `template.scd`.
5. Adjust audio settings if needed.
6. Start processing.

### Audio Folder

1. Choose `Audio folder`.
2. Pick a source folder.
3. Pick an output folder.
4. Enable recursive search if needed.
5. Choose a built-in template or a custom `template.scd`.
6. Start processing.

### Refresh SCD Library

1. Choose `Refresh SCD library`.
2. Pick a folder with existing `.scd` files.
3. Decide whether to:
   - refresh structure and metadata only
   - rebuild audio only
   - rebuild both structure and audio
4. Optionally enable recursive search.
5. Start processing.

## Audio Settings

The current shipping preset is intentionally simple:

- Vorbis quality `q7`
- stereo output
- `44.1 kHz`

Advanced mode also supports:

- explicit Vorbis VBR quality
- explicit nominal bitrate target

Loop handling:

- when enabled, the app writes a full-track loop
- `LoopStart = 0`
- `LoopEnd` is rebuilt from the new track
- marker and track timeline data are rewritten to match

## SCD Logic

The implementation rebuilds SCD files by following the same practical model used in VFXEditor-derived research:

- read SCD header and section tables
- validate that the template contains exactly one non-empty audio entry
- preserve non-audio sections from the chosen structure source
- rebuild the Vorbis audio entry
- rewrite offsets, loop metadata, marker timing, and track playback timeline
- update stored play time metadata

This repository also contains `_verify` helper projects used to sanity-check SCD behavior during development. They are verification tooling, not part of the shipping application.

## Project Structure

- `MassSCDCreator`
  - WPF UI, view-models, services, and localization resources
- `_verify`
  - developer verification runners for SCD-related checks
- `tmp`
  - non-shipping research/reference material

## Validation

Useful local checks:

```powershell
dotnet build .\MassSCDCreator.slnx -c Release
dotnet publish .\MassSCDCreator\MassSCDCreator.csproj -c Release -r win-x64 --self-contained false
```
