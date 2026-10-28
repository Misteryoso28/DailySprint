# DailySprint

DailySprint is an offline Windows desktop app for recording daily expenses, setting a daily spending target, and tracking habits over time. It is based on the DailySprint concept in the supplied starter code, with persistent, date-based records and local backup/restore.

## Install on Windows

1. Download and run `DailySprint-Setup.exe` from the release's `dist` folder.
2. Choose an install location and whether to add a desktop shortcut.
3. Open DailySprint from the Start menu or desktop.

The installer is per-user and does not require administrator permission. A portable `DailySprint.exe` is also included in `dist`. The app requires Windows 10 or later with the .NET Framework 4.x runtime.

## Features

- Add, edit, categorize, and remove expenses on any date.
- Set a daily spending target and select a display currency.
- Create habits, mark them complete by date, and see a consecutive-day completion streak.
- Review daily records and a seven-day summary.
- Export expenses and habit history to CSV.
- Export and restore a complete JSON backup.
- Keep data locally in `%APPDATA%\DailySprint\data.json`; automatic saves do not require an internet connection.
- Install and uninstall for the current Windows user without administrator access; uninstall keeps the saved data.

Expense entries are entered by the user. DailySprint does not connect to bank accounts, automatically import transactions, provide cloud synchronization, or convert between currencies. Keep a separate backup when moving to another computer; restoring a backup replaces the data on that computer.

## Build and test

The build script uses the C# compiler included with the .NET Framework developer tools or Visual Studio. From PowerShell in the repository:

```powershell
.\build.ps1 -RunTests
```

The script creates:

- `dist\DailySprint.exe` — the portable application.
- `dist\DailySprint-Setup.exe` — a per-user installer that adds Start menu and optional desktop shortcuts.
- `dist\DailySprint.CoreTests.exe` — core checks, when `-RunTests` is used.

The generated `dist` directory is excluded from Git.
