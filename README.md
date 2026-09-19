# StegaSuite for Windows

**Language:** [فارسی](README_FA.md) | **English**

[![Release](https://img.shields.io/github/v/release/Alvandcode/StegaSuite-Windows)](https://github.com/Alvandcode/StegaSuite-Windows/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](https://github.com/Alvandcode/StegaSuite-Windows/releases)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Release build](https://github.com/Alvandcode/StegaSuite-Windows/actions/workflows/release.yml/badge.svg)](https://github.com/Alvandcode/StegaSuite-Windows/actions)

Hide any file inside any file — secure steganography for Windows 10/11 (x64),
with AES-256-GCM encryption. Windows port of
[StegaSuite for Android](https://github.com/Alvandcode/StegaSuite).

## Download

Get the latest build from
[Releases](https://github.com/Alvandcode/StegaSuite-Windows/releases):

- **StegaSuite-Setup-2.0.0.zip** — installer (self-contained, no .NET needed).
  Unzip and run `Install.bat`.
- **StegaSuite.exe** — portable (needs
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download)).

> Releases are built automatically by GitHub Actions on every version tag
> (`git tag vX.Y.Z && git push origin vX.Y.Z`).

## Features

- Hide files in **PNG, BMP, WAV** and generic files (LSB steganography)
- **AES-256-GCM + PBKDF2-HMAC-SHA256** encryption (optional password)
- Original filename restored on extract; **fully offline** (except Google sign-in)
- **5 languages** (Persian, Arabic, English, Russian, Chinese — real RTL)
  and **9 themes** (4 dark + 5 light), remembered per user and per machine
- Local per-user accounts (PBKDF2-hashed), encrypted **backup/restore**
  (`*.stegabak`) and real **Google sign-in** (OAuth2 + PKCE)
- Responsive DPI-aware WPF interface with a fullscreen login screen

## Compatibility with the Android app

Same wire format (`SGP2`/`SGF1`/`SGA1`/`STGS`) — files hidden on one side
extract on the other.

> Note: this port fixes a hide/extract asymmetry in generic-file mode that is
> still present in the Android app; full cross-compatibility for that mode
> needs the same one-line fix there.

## Build from source

```bat
dotnet build StegaSuite.Wpf.csproj -c Release
dotnet publish StegaSuite.Wpf.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o publish
```

Tests (engine round-trips + headless UI self-test over
3 sizes × 5 languages × 9 themes × 6 pages):

```bat
dotnet run --project Tests\Tests.csproj -c Release
dotnet run --project StegaSuite.Wpf.csproj -c Release -- --selftest
```

## Enable Google sign-in in your build

The button needs a Desktop-type OAuth client:

1. Create one in Google Cloud Console.
2. Put its Client ID and Client Secret in `GoogleSecrets.cs` and rebuild.
3. ⚠️ Never commit real values to a public repo.

## Project structure

```
App.xaml(.cs)            entry point + button styles
MainWindow.xaml(.cs)     all 6 pages + 5-language dictionary
SelfTest.cs              headless layout verification (--selftest)
Themes/                  9 XAML themes (swapped live via DynamicResource)
Core/                    engine shared with Android (crypto + 3 stego motors)
Tests/                   engine round-trip tests
Installer/               dependency-free setup (Install.bat/Install.ps1)
Assets/                  app icon + artwork
```

## Support the project

If StegaSuite is useful to you:

- ⭐ Star it on GitHub:
  [Alvandcode/StegaSuite-Windows](https://github.com/Alvandcode/StegaSuite-Windows)
- 💬 Telegram channel: [t.me/a_c_official](https://t.me/a_c_official)
- 🌐 Website & tutorial: [alvandcode.github.io/StegaSuite](https://alvandcode.github.io/StegaSuite/)
- 💎 TON wallet for donations:
  `UQCB9rzvwmq0FJDaBkHVdBgbfZPb06FWdKco3woAHH6AXuUt`

## License

MIT — see [LICENSE](LICENSE).
