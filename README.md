# ControlDeck

A custom WPF kiosk app for repurposing a spare touchscreen monitor as a second-display control
deck for a Windows PC — shortcuts, live system metrics, streaming services, and media/audio
controls, all swipeable by touch or mouse. 

## Features

- **Shortcuts pages** — a JSON-configurable grid of app launchers (run a command, or trigger a
  system action: lock, sleep, show desktop, print screen). Auto-paginates as entries are added.
  The first page also shows a live hardware metrics row (CPU/GPU load & temp, RAM, disk, network,
  uptime).
- **Streaming page** — a JSON-configurable picker grid of streaming services that opens into an
  embedded browser (WebView2) with native ad-block and popup-block filtering.
- **Wallpaper page** — a clock over a gradient background, or a custom image if you drop one in.
- **Shared media/control widget**, embedded at the bottom of every shortcuts page:
  - Now-playing title/artist/art, pulled from whatever's playing system-wide
  - Play/pause/skip transport controls
  - Volume slider + mute, auto-retargeting when you switch the active output device
  - Microphone mute toggle
  - Output device switcher — change Windows' default playback device from the kiosk itself
- **Touch and mouse swipe** between pages, with edge-reveal navigation arrows and a hidden
  close button that appears near the top edge.
- Runs borderless/topmost, positioned on whichever monitor you designate as the kiosk display.

## Requirements

- Windows 10 (1903+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Administrator rights at runtime (see below)

## Building and running

```
dotnet build ControlDeck.sln
dotnet run --project src\ControlDeck
```

The app manifest requests `requireAdministrator` — needed for `LibreHardwareMonitorLib` to read
full sensor data (it loads a small kernel driver). Expect a UAC prompt.

**Dev note:** `dotnet run`/launching the built `.exe` directly goes through the apphost, which
enforces the manifest's elevation requirement and will prompt for UAC (or fail non-interactively).
Running the DLL directly via the shared host — `dotnet bin\Debug\net8.0-windows10.0.19041.0\ControlDeck.dll` —
bypasses that gate, which is useful for quick unelevated smoke tests but won't have full sensor
access.

## Configuration

On first run, ControlDeck writes one editable config file: `%LOCALAPPDATA%\ControlDeck\config.json`,
with three top-level sections:

- `AppLaunchers` — the shortcuts grid entries
- `StreamingServices` — the streaming service picker entries
- `DisplayDeviceName` / `DisplayNumber` — which monitor to run on (see below)

`wallpaper.jpg` (optional, not auto-generated, and not part of `config.json`) can still be dropped
into the same folder to replace the default gradient on the Wallpaper page.

Edit `config.json` directly and restart the app to pick up changes — no rebuild required. Each
section falls back independently to its own built-in defaults if it's missing or empty, and if the
whole file fails to parse, every section falls back for that session without overwriting what's on
disk.

The built-in `AppLaunchers`/`StreamingServices` defaults themselves live in
[`src/ControlDeck/Assets/defaults.json`](src/ControlDeck/Assets/defaults.json), embedded into the
built app rather than hardcoded in C# — that file is what gets copied into a fresh `config.json`
the first time the app runs with no config present, and what a section falls back to if it's
missing or empty. Edit it and rebuild to change what ships out of the box.

If you're updating from a version that used the old separate `app-launchers.json` /
`streaming-services.json` / `display.json` files, ControlDeck migrates them into `config.json`
automatically the first time it runs and removes the old files — nothing to do by hand.

### Choosing the kiosk display

On first run, `config.json` includes a `DetectedDisplays` list showing every currently connected
monitor — its Windows device name, a parsed display number, resolution, and whether it's the
primary. Copy the `DisplayNumber` of the monitor you want ControlDeck to run on into the top-level
`DisplayNumber` field (or the exact `DeviceName` into `DisplayDeviceName`, which is checked first
if both are set), then restart:

```json
{
  "AppLaunchers": [ ... ],
  "StreamingServices": [ ... ],
  "DisplayDeviceName": null,
  "DisplayNumber": 1,
  "DetectedDisplays": [ ... ]
}
```

If neither is set, or the configured monitor isn't currently connected, ControlDeck falls back to
the first non-primary display, or the primary display if that's the only one available.
`DetectedDisplays` itself is only a reference, written once, and isn't read back — it won't update
on its own if you reconnect monitors in a different order later. To refresh it, delete the whole
`config.json` (this also resets `AppLaunchers`/`StreamingServices` to defaults, so copy anything
you've customized there first) and relaunch.

## Project layout

```
src/ControlDeck/
  App.xaml(.cs)              Application-wide styles/resources, startup
  MainWindow.xaml(.cs)       Hosts the swipeable page deck
  Assets/
    defaults.json            Built-in AppLaunchers/StreamingServices defaults, embedded at build time
  Controls/
    SwipeContainer           Touch/mouse page-swipe host, page dots, edge-reveal nav arrows
    MediaWidget              Shared now-playing/transport/volume/mic/output-device controls
  Views/
    ShortcutsPage            App launcher grid + metrics row
    StreamingPage            Streaming service picker + embedded WebView2 browser
    WallpaperPage            Clock + background
  Services/
    ControlDeckConfig               config.json — shortcuts, streaming services, display selection
    AppLauncherService              Launching shortcuts (commands + system actions)
    AdBlockList                    WebView2 ad/tracker request filtering
    SystemActionsService           Lock/sleep/show desktop/print screen
    AudioService / MicrophoneService / AudioOutputService   System volume, mic mute, output switching
    HardwareMonitorService         CPU/GPU/RAM/disk/network sensors
    MediaSessionService            System-wide now-playing/transport control
    KioskWindowPlacementService    Positions the window on the target monitor, using ControlDeckConfig
```

## Not yet set up

These are manual, hardware-dependent steps outside the app itself:

- **Auto-start at logon** — register a Task Scheduler entry (run with highest privileges, trigger
  at logon) so the app launches elevated without a UAC prompt each login, rather than a Startup
  folder shortcut.
