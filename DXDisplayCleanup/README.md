# DX Display Cleaner

Android companion utility for DX Manager. It reads and removes only the
`overlay_display_devices` global setting used by Android's simulated secondary
display feature.

## Safety boundary

- Requests only `android.permission.WRITE_SECURE_SETTINGS`.
- Does not provide a shell, execute arbitrary commands, use the network, or
  collect data.
- The permission must be granted once through a verified DX Manager build or
  ADB. Android cannot show a normal runtime-permission dialog for it.
- Cleanup is verified by reading the setting again after deletion.

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

Grant command:

```text
adb shell pm grant io.github.mazemei.dxdisplaycleanup android.permission.WRITE_SECURE_SETTINGS
```
