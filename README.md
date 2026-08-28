# Tim Cook Guard

Tim Cook Guard is a playful Windows tray utility that can watch for activity after you step away. If the configured corner gesture is missed, it fills every monitor with Tim Cook until `cook` is typed.

## Features

- Silent manual arming with `Ctrl+Alt+T`
- Optional automatic arming after a configurable idle period
- Three-second top-right then top-left mouse gesture
- Full-screen takeover on every monitor
- Type `cook` to dismiss
- Embedded "wow" reaction for incorrect four-letter codes
- Optional webcam photo, short video, and Discord webhook upload
- Optional forced shutdown after the deadline (off by default)
- Tray icon, control panel, startup support, and per-user installer
- Windows lock-aware behavior: automatic mode resets on lock; manual arming remains armed

## Install

Download `TimCookGuard-Setup.exe` from the latest release and run it. The installer does not require administrator access. It installs for the current Windows user, adds a Start Menu shortcut, enables startup, and registers an uninstaller in Windows Apps & Features.

The installer and app are currently unsigned, so Windows SmartScreen may show an unrecognized-app warning.

## Camera requirements

Photo and video evidence use Python with OpenCV. Install Python 3, then run:

```powershell
py -m pip install opencv-python-headless
```

Camera features, video recording, Discord uploads, automatic arming, and forced shutdown are all configurable in the control panel. Forced shutdown is off by default because it can discard unsaved work.

## Build

On 64-bit Windows with .NET Framework 4.x installed:

```powershell
.\build.ps1
.\build-installer.ps1
```

Build output is written to `dist`.

## Privacy and safety

Captured evidence is saved to the current user's Downloads folder. Nothing is uploaded unless a Discord webhook is configured and webhook delivery is enabled. Use camera recording only where you have permission to do so.

This project is an independent parody utility. It is not affiliated with or endorsed by Tim Cook or Apple Inc. The bundled portrait is an original stylized illustration generated for this project.
