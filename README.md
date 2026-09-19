# StegaSuite for Windows

**Language:** [فارسی](README_FA.md) | **English**

[![Release](https://img.shields.io/github/v/release/Alvandcode/StegaSuite-Windows)](https://github.com/Alvandcode/StegaSuite-Windows/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](https://github.com/Alvandcode/StegaSuite-Windows/releases)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Release build](https://github.com/Alvandcode/StegaSuite-Windows/actions/workflows/release.yml/badge.svg)](https://github.com/Alvandcode/StegaSuite-Windows/actions)

Hide any file inside any file — secure steganography for Windows 10/11 (x64),
with AES-256-GCM encryption. Windows port of
[StegaSuite for Android](https://github.com/Alvandcode/StegaSuite).

## ⬇️ Download

| Edition | Link | Notes |
|---------|------|-------|
| 🪟 **Windows (this repo)** | [Windows Releases](https://github.com/Alvandcode/StegaSuite-Windows/releases) | `StegaSuite-Install-2.0.0.zip` (self-contained installer) or `StegaSuite-Portable-2.0.0.exe` (portable) |
| 🤖 **Android (sister repo)** | [Android Releases](https://github.com/Alvandcode/StegaSuite/releases) | `StegaSuite-v{version}.apk` — grab the mobile build if you use Android |

> Both editions share the same wire format (`SGP2`/`SGF1`/`SGA1`), so files hidden
> on Windows extract on Android and vice versa.

---

## What is StegaSuite?

**StegaSuite** hides any file inside another file using **steganography**.
Unlike plain encryption (e.g. passworded ZIP) which advertises that something is
encrypted, the output file looks **completely normal** — nobody viewing the
image/audio even knows a secret is inside.

```
📷 Carrier + 📄 Payload + 🔑 Password (optional) → 🔒 Normal-looking output
```

Full visual tutorial (Android UI, same logic):
[alvandcode.github.io/StegaSuite/tutorial.html](https://alvandcode.github.io/StegaSuite/tutorial.html)

## Features

- Hide files in **PNG, BMP, WAV** and generic files (LSB steganography)
- **AES-256-GCM + PBKDF2-HMAC-SHA256** encryption (optional password; CRC32 otherwise)
- Auto format detection via **Magic Bytes** + live **capacity** readout
- Original filename restored on extract; **fully offline** (except Google sign-in)
- Local per-user accounts (PBKDF2-hashed) + encrypted **backup/restore** (`*.stegabak`) + one-time recovery code
- Real **Google sign-in** (OAuth2 + PKCE via system browser + loopback redirect)
- **Operation history** window (hide/extract log)
- **5 languages** (Persian, Arabic, English, Russian, Chinese — real RTL) and **9 themes** (4 dark + 5 light), remembered per user
- Drag & drop + live log console + status pill
- Responsive DPI-aware WPF UI (default 900×760, min 740×620, safe at 125%/150% scaling)

---

## Install & run

### Option 1: Portable (fast)

1. Download `StegaSuite-Portable-2.0.0.exe` from
   [Releases](https://github.com/Alvandcode/StegaSuite-Windows/releases).
2. Install [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download) once.
3. Run the EXE — no install, no admin needed.

### Option 2: Installer (self-contained)

1. Download `StegaSuite-Install-2.0.0.zip` and unzip it.
2. Double-click `Install.bat` (with admin → machine install into Program Files;
   without admin → per-user install).
3. Start-menu/desktop shortcuts are created and it registers in Add/Remove Programs.
4. Uninstall from there. User data (`%AppData%\StegaSuite`) is kept.

> Releases are built automatically by GitHub Actions on every version tag
> (`git tag vX.Y.Z && git push origin vX.Y.Z`).

Supported OS: Windows 10 (1809+) and Windows 11, x64 only.

---

## Step 0: Login / account

The app opens on a fullscreen login screen:

1. **Sign up:** username + password + confirm → "Create account".
2. Save the **one-time recovery code** immediately! It + the backup file recovers a forgotten password.
3. **Log in:** username + password → "Enter the app". Or the **Google** button (system browser, PKCE + loopback).
4. **Forgot password:** from login → "Forgot password" → give `*.stegabak` + recovery code → set a new password.
5. **Backup:** ⚙️ Settings → export backup (`*.stegabak`, AES-256-GCM encrypted). Language/theme are stored per user.

---

## Tutorial: Hide a file (step by step)

### Step 1: Carrier file

The file that will carry the secret. It still looks normal afterwards.

1. In the capsule sidebar click **🏠 Home** (default).
2. In the "Carrier file" card click **Browse...** — or **drag & drop** a file into the field.
3. Details appear: format type, name, size, and **capacity**.
4. The status pill + log console confirm the result.

> ⚠️ **Lossy warning:** JPEG/WebP/MP3/MP4 carriers may corrupt hidden data.
> For reliable results use **PNG, BMP, or WAV**.

### Step 2: Payload file

Any file, any extension: docs, images, audio, video, PDF, ZIP, ...

1. In the second card click **Browse...** (drag & drop works too).
2. Name and size are shown.
3. If payload > capacity you'll get an error — pick a bigger carrier or ZIP the payload.

### Step 3: Password (optional, recommended)

1. Type a password in the "Password" field (👁️ toggles visibility).
2. With password → data is encrypted with **AES-256-GCM** before hiding (defense in depth).
3. Empty → stored with CRC32 only (hidden but not encrypted).

> ⚠️ Don't forget the password — there is no recovery for the hidden payload itself.

### Step 4: Hide & Save

1. Click **"Hide & Save"**.
2. "Working..." appears (usually a few seconds).
3. A Save dialog opens — suggested name carries `(st)`, e.g. `photo(st).png`.
4. Path is shown in status + log and the op is recorded in **History**.

---

## Tutorial: Extract a hidden file

1. In the sidebar click **🔓 Extract**.
2. Pick the carrier containing the secret (drag & drop OK).
3. Enter the password if one was set.
4. Click **"Extract Hidden File"**.
5. The file is saved with its **original name + extension**
   (except V1-legacy files → `recovered_file`).

> 💡 Detection uses Magic Bytes, not the extension — renaming the carrier doesn't break extraction.

### Other sidebar tabs

- ℹ️ **About** — intro + 📖 full tutorial button
- ✉️ **Contact** — contact links
- ❤️ **Support** — GitHub star + TON address
- ⚙️ **Settings** — language (5), theme (9), account, backup/restore, logout

---

## Supported formats

### Best carriers (recommended)

| Format | Ext | Method | Notes |
|--------|-----|--------|-------|
| **PNG** | `.png` | pixel LSB | best — no quality loss |
| **BMP** | `.bmp` | pixel LSB | excellent, large |
| **WAV** | `.wav` | audio-sample LSB | inaudible change |

### Generic carriers (use with care)

| Group | Formats |
|-------|---------|
| Images | TIFF, WebP, GIF, SVG |
| Audio | MP3, FLAC, OGG, M4A, AAC |
| Video | MP4, AVI, MKV, MOV, WebM |
| Docs | PDF, DOCX, XLSX, PPTX, TXT |
| Archives | ZIP, RAR, 7Z, TAR, GZ |
| Code | JS, PY, TS, HTML, CSS |

> Lossy formats (esp. JPEG/WebP) may destroy data. Always do a test extract if you must use them.

### Payload: unlimited

Docs, images, audio, video, archives, code — any file, any extension.

---

## Encryption details

### Layer 1: LSB steganography

Least-significant bits of pixels/samples/bytes. Max color shift 1/255 — invisible.

### Layer 2: AES-256-GCM (if password set)

| Parameter | Value |
|-----------|-------|
| Algorithm | AES/GCM/NoPadding |
| Key size | 256 bits |
| KDF | PBKDF2WithHmacSHA256 |
| Iterations | 600,000 |
| Salt | 16 bytes (SecureRandom) |
| Nonce (IV) | 12 bytes (SecureRandom) |
| GCM tag | 128 bits |

### Output structure

```
┌──────────────────────────────────────────────┐
│ Magic: SGP2 / SGF1 / SGA1      (4 bytes)     │
│ Payload Length                 (8 bytes)     │
│ ┌──────────────────────────────────────────┐ │
│ │ Encrypted/CRC'd Payload                  │ │
│ │  Filename Length (4 bytes)               │ │
│ │  Filename UTF-8 (≤1024 bytes)            │ │
│ │  File Content (N bytes)                  │ │
│ └──────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
```

---

## Capacity

| Carrier | Formula | Example |
|---------|---------|---------|
| PNG/BMP | `(width × height × 3) ÷ 8` | 1920×1080 ≈ 777 KB |
| WAV | `(samples × channels) ÷ 8` | 10s @44.1kHz ≈ 259 KB |
| Generic | `(file size − safe offset) ÷ 8` | 1 MB file ≈ 122 KB |

> Subtract ~50 bytes for header + crypto overhead (salt/nonce/tag).
> Rule of thumb: 12 MP photo ≈ 4.3 MB; 1 min WAV ≈ 1.5 MB.

---

## Limits & security notes

| Limit | Fix |
|-------|-----|
| Payload max 50 MB | ZIP it or split |
| Filename max 1024 bytes (~340 Persian chars) | shorter name |
| JPEG/WebP may corrupt | use PNG/BMP/WAV |
| Editing carrier afterwards (crop/resize/filter) destroys data | don't touch output |
| Telegram/email may recompress | ZIP before sending |

- Use a strong password (letters+digits+symbols), stored safely.
- Keep backups of outputs + account `*.stegabak`.
- V1-legacy files extract as `recovered_file` (no original name) — expected.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Wrong password or corrupted file" | wrong password / edited carrier | recheck password; use pristine carrier |
| "Corrupted file" | lossyedited carrier | PNG/BMP/WAV + untouched output |
| Not enough capacity | carrier too small | bigger carrier / compress payload |
| Output won't open | wrong default app | open with proper viewer/player |
| Google login fails | no Desktop OAuth client | put Client ID/Secret in `GoogleSecrets.cs`, rebuild |

---

## Compatibility with the Android app

Same wire format (`SGP2`/`SGF1`/`SGA1`/`STGS`) — files hidden on one side
extract on the other (for PNG/BMP/WAV).

> Note: this port fixes a hide/extract asymmetry in generic-file mode that is
> still present in the Android app; full cross-compatibility for that mode
> needs the same one-line fix there.

🤖 Get the Android build here:
[Android Releases](https://github.com/Alvandcode/StegaSuite/releases) —
repo: [Alvandcode/StegaSuite](https://github.com/Alvandcode/StegaSuite)

---

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
App.xaml(.cs)            entry point + button styles (+ --selftest)
MainWindow.xaml(.cs)     all 6 pages + 5-language dictionary
SelfTest.cs              headless layout verification
HistoryStore.cs / HistoryWindow.xaml(.cs)  history
AuthStore.cs / GoogleAuth.cs / GoogleSecrets.cs  accounts + Google
Themes/                  9 XAML themes (swapped live via DynamicResource)
Core/                    engine shared with Android (crypto + 3 stego motors)
Tests/                   engine round-trip tests
Installer/               dependency-free setup (Install.bat/Install.ps1)
Assets/                  app icon + artwork
```

---

## Support the project

If StegaSuite is useful to you:

- ⭐ Star it on GitHub:
  [Alvandcode/StegaSuite-Windows](https://github.com/Alvandcode/StegaSuite-Windows)
- 💬 Telegram channel: [t.me/a_c_official](https://t.me/a_c_official)
- 🌐 Website & tutorial: [alvandcode.github.io/StegaSuite](https://alvandcode.github.io/StegaSuite/) —
  [step-by-step tutorial](https://alvandcode.github.io/StegaSuite/tutorial.html)
- 💎 TON wallet for donations:
  `UQCB9rzvwmq0FJDaBkHVdBgbfZPb06FWdKco3woAHH6AXuUt`

## License

MIT — see [LICENSE](LICENSE).
