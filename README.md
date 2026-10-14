# FerryClip

<p align="center"><img src="branding/FerryClip-logo.png" width="80" height="80" alt="FerryClip logo"></p>

<p align="center"><strong>Clipboard sync between Windows and Android over your local network.</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-desktop-30343B?logo=windows&logoColor=white" alt="Windows desktop">
  <img src="https://img.shields.io/badge/Android-7%2B-138A82?logo=android&logoColor=white" alt="Android 7.0 and newer">
  <img src="https://img.shields.io/badge/Transport-TLS%201.3-C18B42" alt="TLS 1.3 transport">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg" alt="MIT License"></a>
</p>

FerryClip transfers clipboard text directly between a Windows PC and Android phone over a reachable local network, protected by mutually authenticated TLS 1.3. There is no cloud clipboard or relay. Android 7.0 (API 24) and newer are supported; Android 10 or newer is required for TLS 1.3 sync.

## Features

- Pair with a short-lived six-digit code generated on Windows.
- Save and manage multiple PCs independently.
- Recover connections over local Wi-Fi and hotspot networks, with manual IP entry when needed.
- Sync text from Windows to Android while connected; sending from Android requires a deliberate notification or Quick Settings action.
- Keep optional clipboard history locally, encrypted at rest, with privacy and retention settings. History is not shared between devices.
- Use the Windows system-tray popover or full main window to manage devices, history, settings, and help.

## Downloads

The current release is [FerryClip v1.0.0](https://github.com/Tanishktewetia/FerryClip/releases/tag/v1.0.0). Fixed-name downloads are available from the [latest release](https://github.com/Tanishktewetia/FerryClip/releases/latest):

- **Windows installer (recommended):** [FerryClip-Windows-Setup.exe](https://github.com/Tanishktewetia/FerryClip/releases/latest/download/FerryClip-Windows-Setup.exe)
- Windows portable: [FerryClip-Windows-Portable.zip](https://github.com/Tanishktewetia/FerryClip/releases/latest/download/FerryClip-Windows-Portable.zip)
- Android: [FerryClip-Android.apk](https://github.com/Tanishktewetia/FerryClip/releases/latest/download/FerryClip-Android.apk)
- Windows checksums: [SHA256SUMS-windows.txt](https://github.com/Tanishktewetia/FerryClip/releases/latest/download/SHA256SUMS-windows.txt)
- [All GitHub Releases](https://github.com/Tanishktewetia/FerryClip/releases) · [Project website](https://www.ferryclip.app)

The Windows installer and portable build are currently unsigned. SmartScreen may show an unknown-publisher warning: select **More info**, then **Run anyway** if you choose to proceed. The Android APK is signed with the project’s release key; Android may ask you to allow your browser or file manager to install unknown apps.

### Verify your download

Download `SHA256SUMS-windows.txt` alongside the Windows file. In PowerShell, run `Get-FileHash -Algorithm SHA256 .\FerryClip-Windows-Setup.exe` (or substitute the portable ZIP), then compare the resulting hash with the matching filename in the checksum file. A match detects file changes but does not independently prove who published the file.

To inspect the APK signer, run `apksigner verify --print-certs FerryClip-Android.apk` from Android SDK Build Tools. The command verifies APK signing and prints certificate details. The release certificate SHA-256 fingerprint is `81:fc:dc:7f:e4:2f:86:7f:5e:32:ef:43:5c:45:9c:d1:31:3a:18:a1:23:1e:de:0c:9e:b5:be:7c:2a:7c:ba:8d`; compare it with the reported signer certificate SHA-256 digest.

**Android upgrade note:** If you installed an earlier debug-signed FerryClip build, uninstall it once before installing v1.0.0. Android does not accept the release key as an update to the old debug key. Uninstalling removes saved pairing information and clipboard history.

## Code signing policy

The Windows binaries are currently unsigned. The project is applying for free code signing from SignPath Foundation and has not yet been approved. Author, Reviewer, and Approver: [Tanishk Tewetia](https://github.com/Tanishktewetia).

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. Clipboard sync and local discovery communicate with the user’s paired devices on their local network; opening project/help links or downloading releases is initiated by the user outside the app’s sync traffic.

<!-- After SignPath Foundation approval, add: Free code signing provided by SignPath.io, certificate by SignPath Foundation -->

## Uninstall

- **Windows installer:** Uninstall FerryClip from **Windows Settings → Apps**. The uninstaller asks whether to delete saved data; the default is **No**. Data folders are `%LOCALAPPDATA%\FerryClip` and `%APPDATA%\ClipSync`; they can contain settings, pairing information, encrypted clipboard history, and logs.
- **Windows portable:** Turn off **Start with Windows** in FerryClip first, then delete the portable files. To remove saved data too, delete the two folders above.
- **Android:** Uninstall FerryClip as usual from Android settings or the launcher. Android app data, including pairing and history, is removed on uninstall.

## Build from source

Requirements from the project configuration: **.NET 10 SDK**, **JDK 17**, **Android SDK Platform 34**, and PowerShell on Windows. Gradle Wrapper 8.11.1 and Android Gradle Plugin 8.7.3 are configured in the repository. Node.js is only needed for the static website.

```powershell
# Build the Windows exe, portable ZIP, and Inno Setup installer
.\scripts\build-windows-installer.ps1

# Build the locally signed Android APKs (requires your local release key)
.\scripts\build-android.ps1

# Optional: build and preview the static website
node site/build.cjs
node site/serve.cjs
```

Android build outputs include both `FerryClip-<versionName>.apk` and the fixed-name `FerryClip-Android.apk` under `dist/`. Windows outputs are also written to `dist/`. CI builds Windows release artifacts and Android debug builds; it does not access the local Android signing key.

## Privacy and troubleshooting

Clipboard data transfers directly over the local network using TLS 1.3. Android history uses an Android Keystore-protected key; Windows history uses current-user Windows Data Protection. History is local to each device. Pausing sync stops transfer in both directions. Android background recovery can depend on notification and battery settings.

For diagnostics, use **Advanced → Diagnostics** on Android or **Open Log Folder** from the Windows tray menu. When reporting an issue, include app versions, network mode, test steps, and expected versus actual behavior. Do not include clipboard contents or other private data.

## Contributing and issues

Bug reports, questions, and contributions are welcome. Please [open an issue](https://github.com/Tanishktewetia/FerryClip/issues) with reproduction steps and relevant diagnostic details.

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

FerryClip is licensed under the [MIT License](LICENSE). Third-party components and their license texts are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

The FerryClip name and logo are not licensed for use in modified or unofficial versions.
