# DX Companion

Private Android companion utility for DX Manager. It can inspect and clean up:

- Android's simulated-secondary-display setting (`overlay_display_devices`)
- Developer options **Stay awake** (`stay_on_while_plugged_in`)

The app provides separate actions for both settings and a combined action. Its
Quick Settings tile and compact 2 × 1 home-screen widget clean both by default;
their targets can be changed inside the app.

DX Companion 1.4.1 can also keep an authenticated loopback session with a
verified DX Manager instance. When Windows announces a real session shutdown,
DX Manager sends the cleanup request over that already-open session instead of
starting a new ADB process. A broken USB/Wi-Fi session does not clean up
immediately: the default grace period is five minutes, and the user can select
immediate cleanup, one, five, ten or thirty minutes, or disable delayed cleanup.

## Safety boundary

- Uses `WRITE_SECURE_SETTINGS` only for the two documented recovery settings.
- Does not provide a shell, execute arbitrary commands, contact cloud services,
  or collect data. Its network use is limited to authenticated local sessions
  created through an ADB reverse tunnel by a verified DX Manager instance.
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
