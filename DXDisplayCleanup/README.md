# DX Companion

Private Android companion utility for DX Manager. It can inspect and clean up:

- Android's simulated-secondary-display setting (`overlay_display_devices`)
- Developer options **Stay awake** (`stay_on_while_plugged_in`)

The app provides separate actions for both settings and a combined action. Its
Quick Settings tile and compact 2 × 1 home-screen widget clean both by default;
their targets can be changed inside the app.

## Safety boundary

- Requests only `android.permission.WRITE_SECURE_SETTINGS`.
- Does not provide a shell, execute arbitrary commands, use the network, or
  collect data.
- The permission is granted only through DX Manager after the exact package and
  official signing certificate are verified.
- Every cleanup operation is verified by reading the affected setting again.

## Local build

Run the repository build helper from PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\scripts\Build-AndroidCleanup.ps1
```

The helper uses `JAVA_HOME` and `ANDROID_HOME` when set, or the ignored
repository-local `.build-tools\android` toolchain. JDK 17 and Android SDK
platform 36 are required. Never commit `signing.properties`, a keystore, or
passwords. Back up the release keystore separately; it is required for future
updates.

Package ID: `io.github.mazemei.dxdisplaycleanup`

The public signing certificate fingerprint is recorded in [SIGNING.md](SIGNING.md).
