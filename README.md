# 👑 Kingshot Finders Fortune Bot

A Windows app that plays the **Finders Fortune** mini game of Kingshot for you.
The game shows six item names at the bottom of the screen. The bot reads them with OCR, finds the matching item in the picture by using a map file, and clicks it. This repeats until all items are found.

## Requirements

- Windows 10 (version 2004) or newer
- An English OCR language pack. It is installed by default on English systems. Otherwise add it under *Settings > Time & language > Language & region > Add a language*, and enable the *Optical character recognition* feature.
- The game must be visible on screen (for example in an emulator or a window). It must not be moved or resized after the game area is selected.

The release is self-contained, so no .NET installation is needed.

## Installation

1. Download the latest `Kingshot-Finders-Fortune-Bot-<version>-win-x64.zip` from the [releases page](../../releases).
2. Unzip it. Keep the `maps` folder next to the exe.
3. Start `KingshotFindersFortuneBot.exe`.

## Usage

1. **Select map file.** Pick the map that is shown in the game. The maps in the `maps` folder are shown as a grid with their pictures. *Open another map file...* opens any other `.txt` map file.
2. **Select game area.** Drag a rectangle over the whole game screen, from the top left corner to the bottom right corner. `Esc`, or a click without dragging, cancels. The bot scales all positions to this rectangle, so it works with any screen size and aspect ratio.
3. Open the Finders Fortune screen in the game and press **Ctrl+Alt+K** to start. Press it again to pause.

While it runs:

- The grid in the window shows the six items that were read. Known items are green, unknown ones red. The item that was just clicked flashes.
- The log at the bottom shows every click and every item that is not in the map.
- Names that OCR reads slightly wrong are still matched (fuzzy matching).
- After 10 scans in a row without a known item, the bot pauses itself (for example at the end of a round).

## Creating a map

The `maps` folder contains maps for the existing boards. For a new or changed board, use **Create Map**. The positions come from [kingshotmastery.com](https://kingshotmastery.com/tools/finders-fortune).

1. Click *Open website and copy script*. The browser opens the page and a script is copied to the clipboard.
2. Open the map you want in the browser. Click the address bar, type `javascript:` yourself, paste the script after it and press Enter. Browsers remove `javascript:` from pasted text, so it has to be typed.
3. The script shows a spinner while it analyzes the item images. It then copies the HTML of the board to the clipboard. If the browser blocks the copy, a box with a *Copy and close* button appears. When it is done, a message is shown.
4. Click *Next: read HTML and save map* in the app. A Save As dialog opens with the name of the map. Save the map into the `maps` folder. The map image is saved next to it with the same name, and the map is loaded right away.

The click position of every item is a point inside the real shape of the item, not the middle of its bounding box. So irregular items such as letters or footprints are hit reliably.

### Map file format

Lines starting with `#` and empty lines are ignored.

```text
0.0844 0.0614 0.0808 0.173   # left top right bottom margin of the board picture, as fractions of the game area
Key;0.7273;0.78              # name;x;y as fractions of the board picture
```

The margin line describes where the board picture sits inside the game screen. The item positions are fractions of the board picture, so a map is independent of the screen size.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build src/KingshotFindersFortuneBot
dotnet run --project src/KingshotFindersFortuneBot
```

A single-file release build:

```powershell
dotnet publish src/KingshotFindersFortuneBot -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Releases

A GitHub Action ([build.yml](.github/workflows/build.yml)) builds the app on every push and pull request. The version is read from `<Version>` in [KingshotFindersFortuneBot.csproj](src/KingshotFindersFortuneBot/KingshotFindersFortuneBot.csproj).

- **Pull requests** only build the app. No release is created.
- **Pushes to `main`** also create a GitHub release `v<version>`. The release has a self-contained single-file exe, zipped with the `maps` folder, and generated release notes.

To publish a new version, increase `<Version>` in the project file and merge the change into `main`. If a release for that version already exists, the action skips the release and shows a notice.
## License

[The Unlicense](LICENSE) (public domain).
