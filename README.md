# Squeak

A minimal, YouTube-style video player for Windows, built on [LibVLCSharp](https://github.com/videolan/libvlcsharp) (WPF). It plays anything VLC can, but wraps it in a clean, borderless, hover-to-reveal interface instead of the classic VLC chrome.

> The UI is in **English by default** and auto-switches to **Polish** on Polish Windows. You can also pick the language from the right-click menu.

## Features

- **Hover-to-reveal controls** — top bar and bottom control bar slide in on mouse movement and auto-hide after a couple of seconds.
- **Click anywhere to play/pause**, with a center flash animation.
- **Scrubbable timeline** — click and drag anywhere on the bar to scrub the video live, with a time preview on hover.
- **Volume** — slider + mute button, on-screen volume/seek feedback.
- **Previous / next** file in the folder, plus **autoplay** the next file when one ends.
- **Audio track & subtitle** selection (right-click menu).
- **Skip intro** button when a file has chapters.
- **Custom borderless window** — pin-on-top, minimize, close, drag to move, double-click / `F` for fullscreen (hides the taskbar).
- **Remembers** volume, always-on-top, language, and each file's playback position between sessions.
- **Drag & drop** a video onto the window to play it.

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Space` / click | Play / pause |
| `←` / `→` | Seek ∓5 s |
| `↑` / `↓` | Volume ±5% |
| `M` | Mute |
| `T` | Pin on top |
| `F` | Fullscreen |
| `Esc` | Exit fullscreen |
| `Ctrl+O` | Open file |

## Requirements

- Windows 10 / 11 (x64)
- To run from source: [.NET 8 SDK](https://dotnet.microsoft.com/download)

The `VideoLAN.LibVLC.Windows` NuGet package bundles the libvlc binaries automatically — you do **not** need VLC installed.

## Run from source

```powershell
git clone https://github.com/<your-user>/squeak-player.git
cd squeak-player
dotnet run --project SqueakPlayer.csproj
```

## Build a release

Produce a self-contained build (bundles the .NET runtime + libvlc, so end users need nothing installed):

```powershell
dotnet publish SqueakPlayer.csproj -c Release -r win-x64 --self-contained true -o publish
```

The `publish` folder then contains `Squeak.exe` and everything it needs.

## Build the installer

The installer is defined with [Inno Setup](https://jrsoftware.org/isdl.php) (free).

1. Publish the app (command above) so the `publish` folder exists.
2. Install Inno Setup.
3. Open `installer/Squeak.iss` in Inno Setup and click **Compile** (or run `iscc installer/Squeak.iss`).
4. The resulting `Squeak-Setup-x.y.z.exe` lands in `dist/`.

## Credits & license

- Squeak's own code is released under the [MIT License](LICENSE).
- Playback is powered by **[VLC](https://www.videolan.org/vlc/) / libVLC** by the VideoLAN project, via **LibVLCSharp**. libVLC is licensed under the **LGPL v2.1+**; its binaries are redistributed unmodified.
