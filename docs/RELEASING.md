# FerryClip release procedure

This project creates a **draft** GitHub Release for version tags. The release workflow builds the Windows executable from source, creates the portable ZIP and Inno Setup installer, and attaches Windows checksums. It does not build or sign the Android APK and it does not publish the draft.

## Prepare the release

1. Choose the release version. Update `<Version>` in `windows/ClipSync.Windows/ClipSync.Windows.csproj` and `versionName` / `versionCode` in `android/app/build.gradle.kts` so they agree. Keep the Windows version in `x.y.z` form; the tag must be `v` plus that version.
2. Build and sign Android locally using your private `android/keystore.properties` and keystore. Never upload either file or put them in GitHub Actions. From PowerShell at the repo root, run:

   ```powershell
   .\scripts\build-android.ps1
   ```

   The script places both `FerryClip-<versionName>.apk` and a copy named `FerryClip-Android.apk` in `dist/`. Upload the fixed-name `FerryClip-Android.apk` to the GitHub Release; keep the versioned APK as a local build artifact. Verify the APK locally with `apksigner verify --print-certs "dist/FerryClip-Android.apk"` and record the signer certificate SHA-256 fingerprint. Calculate the file hash with `Get-FileHash -Algorithm SHA256 -LiteralPath "dist/FerryClip-Android.apk"`.
3. Commit and merge the version changes to `main`. Create and push the matching tag after the commit is on `main`:

   ```powershell
   $version = '1.0.0'
   git tag -a "v$version" -m "FerryClip v$version"
   git push origin "v$version"
   ```

   Set `$version` to the chosen value, for example `1.0.0`. The tag workflow fails early if its version does not exactly match the Windows csproj version.
4. Wait for the **Windows Release** workflow to finish. It builds the Windows executable from the tagged source, packages the portable ZIP and installer, creates `SHA256SUMS-windows.txt`, and opens a **draft, not pre-release** GitHub Release. The workflow uploads a downloadable Actions artifact as well.
5. Open the draft release. Add `FerryClip-Android.apk` and fill in the APK filename, SHA-256, and signing-certificate fingerprint placeholders in the release notes. Confirm the APK signature and checksums before continuing.
6. Review the three Windows assets and the Android details, then publish the draft manually. A `workflow_dispatch` run builds and uploads workflow artifacts but does not create a GitHub Release.

## Local Windows build

- `scripts/build-windows.ps1` builds the self-contained executable and portable ZIP.
- `scripts/build-windows-installer.ps1` builds those outputs and compiles `FerryClip-Windows-Setup.exe` with Inno Setup.
- CI uses the scripts' `-ExeOnly`, `-PortableOnly`, and `-InstallerOnly` modes to keep executable creation, portable packaging, and installer compilation as distinct stages. No SignPath signing is configured yet.
