# Mass SCD Creator

Mass SCD Creator is a Windows app for turning common audio files into FFXIV `.scd` files.

It is meant to be simple: pick your audio, choose where the `.scd` files should go, and let the app handle the conversion.

## What You Can Do

- Convert one audio file into one `.scd`
- Convert a whole folder of audio files in one run
- Refresh an existing `.scd` library without rebuilding everything by hand
- Optionally export the result into a Penumbra mod
- Optionally create or append a Penumbra playlist JSON

## Supported Audio Formats

The app accepts the most common formats, including:

- `.mp3`
- `.flac`
- `.ogg`
- `.wav`
- `.m4a`
- `.aac`
- `.opus`
- `.wma`
- `.aiff`
- `.aif`
- `.mp4`
- `.m4b`

Output is always `.scd`.

## Download And Run

1. Open the [Releases](https://github.com/AEBus/MassSCDCreator/releases) page.
2. Download the latest `.zip` file for Windows.
3. Extract it to any folder.
4. Run `MassSCDCreator.exe`.

## First Launch

The app needs `ffmpeg` for audio conversion.

If `ffmpeg` is not already available, the app can help you set it up. In normal use, this is a one-time step and then you can forget it exists, which is how tools should behave when they want to keep their friends.

## Typical Workflows

### Convert One File

1. Choose `Single file`.
2. Select your audio file.
3. Choose the output `.scd` path.
4. Start processing.

### Convert A Folder

1. Choose `Audio folder`.
2. Select the folder with your music.
3. Choose the output folder.
4. Start processing.

### Refresh Existing SCD Files

1. Choose `Refresh SCD library`.
2. Select the folder with existing `.scd` files.
3. Choose how you want them refreshed.
4. Start processing.

## Penumbra Support

After conversion, you can export the finished files into a Penumbra mod.

The app can:

- copy generated `.scd` files into a selected mod
- create a new playlist JSON
- append tracks to an existing playlist JSON

If you do not use Penumbra, you can ignore this part completely.

## Good To Know

- The app is for Windows.
- It is focused on practical batch conversion, not on making you manually fight every file one by one.
- If a track uses looping, the app rebuilds the loop data for the new output.

## Troubleshooting

If something does not work as expected:

- make sure the source audio file actually plays
- make sure the output folder is writable
- make sure `ffmpeg` is available or let the app set it up
- try again with a simple `.wav` or `.mp3` file first

## For Developers

If you want to build the app from source:

```powershell
dotnet restore .\MassSCDCreator.slnx
dotnet build .\MassSCDCreator.slnx -c Release
```

To publish a release build manually:

```powershell
dotnet publish .\MassSCDCreator\MassSCDCreator.csproj -c Release -r win-x64 --self-contained false
```
