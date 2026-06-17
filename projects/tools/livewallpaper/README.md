# Live Wallpaper Engine

[![Openlearnia](https://img.shields.io/badge/Openlearnia-openlearnia.com-6ea8fe)](https://openlearnia.com)
[![GitHub](https://img.shields.io/badge/GitHub-openlearnia%2Flive--wallpaper-24292f)](https://github.com/openlearnia/live-wallpaper)

A Windows live wallpaper engine that renders video, image slideshows, and HTML/WebView2 wallpapers behind desktop icons.

Part of [Openlearnia](https://openlearnia.com) — source: [github.com/openlearnia/live-wallpaper](https://github.com/openlearnia/live-wallpaper).

## Features

- Desktop injection (legacy WorkerW and Windows 11 24H2+ raised-desktop model)
- Multi-monitor support with per-display wallpaper assignment
- Video wallpapers (MP4, WebM, and more via Media Foundation)
- Optional Mpv backend when `mpv-2.dll` is bundled next to the executable
- Image slideshow folders with fade transitions
- Local HTML wallpapers via WebView2 with `window.liveWallpaper` bridge
- Power profiles: Battery Saver (24 fps), Balanced (30 fps), Performance (60 fps), or Custom
- Max render height cap (1080p / 720p) to reduce GPU fill cost
- Library with thumbnails, search, sort, and favorites
- Global hotkeys: pause, next/previous wallpaper, show window (configurable in settings)
- Per-monitor playlists (rotate wallpapers on a timer)
- Pause rules: fullscreen, battery, idle, manual
- System tray controls (minimize to tray on close; optional start minimized)
- Settings persisted under `%AppData%\LiveWallpaper\`
- Explorer restart recovery and desktop Z-order maintenance
- File logging under `%AppData%\LiveWallpaper\logs\`

## Requirements

- Windows 10 (1809+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- WebView2 Runtime (usually preinstalled on Windows 11)

## Build

```powershell
dotnet build LiveWallpaper.sln -c Release
```

Exit the app before rebuilding if you see file-lock warnings.

## Run

```powershell
dotnet run --project src/LiveWallpaper.App/LiveWallpaper.App.csproj -c Release
```

Place wallpapers in `Documents\LiveWallpapers` or browse to another folder in the app.

## Publish (portable)

```powershell
dotnet publish src/LiveWallpaper.App/LiveWallpaper.App.csproj -c Release -r win-x64 --self-contained
```

Output: `src/LiveWallpaper.App/bin/Release/net8.0-windows/win-x64/publish/`

## MSI installer

Requires [WiX Toolset v3](https://wixtoolset.org/docs/wix3/).

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1
```

Output: `dist/LiveWallpaper-<version>-x64.msi` (version read from `LiveWallpaper.App.csproj`, currently **0.1.0**)

## Default hotkeys

Configurable under **Settings → Global hotkeys** in the app. Defaults:

| Action | Hotkey |
|--------|--------|
| Pause / Resume | Ctrl+Shift+P |
| Next wallpaper | Ctrl+Shift+Right |
| Previous wallpaper | Ctrl+Shift+Left |
| Show window | Ctrl+Shift+W |

## Versioning

Live Wallpaper follows [semantic versioning](https://semver.org/). While the product is in active development it stays on **0.y.z**; the first stable public release under Openlearnia will be **1.0.0**.

## Recent features

- **Hotkey settings UI** — edit pause, next, previous, and show-window bindings; hotkeys re-register on save
- **Remove from library** — context menu removes metadata only (files stay on disk)
- **Slideshow settings** — interval and shuffle when a slideshow item is selected
- **Start minimized to tray** — optional startup behavior
- **Version label** — shown in the main window title bar area

After load, pages receive `window.liveWallpaper`:

```javascript
window.liveWallpaper.maxFps   // number
window.liveWallpaper.paused   // boolean
window.liveWallpaper.postMessage(payload) // send to host
window.addEventListener('livewallpaper-ready', (e) => { ... });
```

See `assets/sample-web/index.html` for a minimal example.

## Video playback modes

- **Balanced / Performance (30+ fps):** native hardware playback
- **Battery Saver (24 fps):** throttled seek-based playback for lower power

## Project structure

```
src/
  LiveWallpaper.App/       WPF shell, tray icon, settings UI
  LiveWallpaper.Core/      Settings, pause rules, library service
  LiveWallpaper.Desktop/   Win32 desktop injection
  LiveWallpaper.Players/   Video, slideshow, web players
  LiveWallpaper.Tests/     Unit tests
assets/                    Sample wallpapers and icon
installer/                 WiX installer script
scripts/                   Icon and installer build helpers
```

## Settings location

`%AppData%\LiveWallpaper\settings.json`

## Known limitations

- Newest Windows Insider desktop changes may require injection updates
- Mpv backend requires bundling `mpv-2.dll` manually
- Full auto-update is not yet implemented

## License

MIT
