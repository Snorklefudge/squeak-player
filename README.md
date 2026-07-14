# Squeak

[![Latest release](https://img.shields.io/github/v/release/Snorklefudge/squeak-player)](https://github.com/Snorklefudge/squeak-player/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Snorklefudge/squeak-player/total)](https://github.com/Snorklefudge/squeak-player/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A minimal, YouTube-style video player for Windows, built on [LibVLCSharp](https://github.com/videolan/libvlcsharp) (WPF). It plays anything VLC can, but wraps it in a clean, borderless, hover-to-reveal interface instead of the classic VLC chrome.

> The UI is in **English by default** and auto-switches to **Polish** on Polish Windows. You can also pick the language from the right-click menu.

## Download

1. Grab the latest **`Squeak-Setup-x.y.z.exe`** from the [**Releases page**](https://github.com/Snorklefudge/squeak-player/releases/latest).
2. Run it — it installs Squeak with a Start-menu shortcut and, if your PC doesn't already have the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0), installs that automatically.

That's it — no VLC install needed.

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
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — the installer downloads and installs it automatically if it's missing.
- To run/build from source: [.NET 8 SDK](https://dotnet.microsoft.com/download)

The `VideoLAN.LibVLC.Windows` NuGet package bundles the libvlc binaries automatically — you do **not** need VLC installed.

## Run from source

```powershell
git clone https://github.com/<your-user>/squeak-player.git
cd squeak-player
dotnet run --project SqueakPlayer.csproj
```

## Build a release

Produce a framework-dependent build (smaller download; relies on the .NET 8 Desktop Runtime, which the installer provides):

```powershell
dotnet publish SqueakPlayer.csproj -c Release -r win-x64 --self-contained false -o publish
```

The `publish` folder then contains `Squeak.exe` and the bundled libvlc.

## Build the installer

The installer is defined with [Inno Setup](https://jrsoftware.org/isdl.php) 6.1+ (free). It installs the .NET 8 Desktop Runtime automatically if the target machine doesn't have it.

1. Publish the app (command above) so the `publish` folder exists.
2. Install Inno Setup.
3. Open `installer/Squeak.iss` in Inno Setup and click **Compile** (or run `iscc installer/Squeak.iss`).
4. The resulting `Squeak-Setup-x.y.z.exe` lands in `dist/`.

## Automated releases

Pushing a version tag builds the installer and publishes a GitHub Release automatically (see `.github/workflows/release.yml`):

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Credits & license

- Squeak's own code is released under the [MIT License](LICENSE).
- Playback is powered by **[VLC](https://www.videolan.org/vlc/) / libVLC** by the VideoLAN project, via **LibVLCSharp**. libVLC is licensed under the **LGPL v2.1+**; its binaries are redistributed unmodified.
