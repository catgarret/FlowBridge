# Flow Bridge for macOS

This is a native SwiftUI port based in part on the MIT-licensed DX Manager
workflow. It discovers USB
and wireless ADB devices, creates and removes the Samsung DeX overlay display,
launches scrcpy for that display, and opens an Android app in a separate scrcpy
virtual display.

## Requirements

- macOS 13 or later (Apple silicon and Intel are both Swift targets; Apple
  silicon is the currently compiled environment)
- A Samsung Galaxy device that supports DeX
- USB debugging enabled and authorized
- No separately installed runtime dependency. The packaged app contains the
  official static macOS scrcpy 4.1 builds and their matching ADB binaries for
  Apple silicon and Intel Macs.

## Build and run

```sh
swift test
swift run DXManagerMac
```

To create an ad-hoc signed application bundle:

```sh
sh scripts/package-macos.sh
open "dist/Flow Bridge.app"
```

## Current scope

Implemented: USB and wireless device discovery, remembered-endpoint automatic
reconnection (including reachable Tailscale or LAN addresses), configurable
resolution/DPI/bitrate/FPS, DeX overlay creation with before/after display-ID
comparison, scrcpy launch, three app-specific virtual-display slots, installed-app
package browsing, ordinary phone-screen mirroring without DeX, file/folder transfer
to Android Download, settings persistence,
session/overlay cleanup, Android 11+ wireless pairing, device diagnostics, power
key/screen wake/sleep commands, interactive region capture, automatic device
refresh, and a menu-bar controller.
Per-device display settings, reusable app profiles, cancellable queued transfer
status, session-log export, and checksum-verified DX Companion installation,
permission grant, and removal are also implemented.
Process-scoped right-Shift correction, Enter/Shift+Enter switching, floating
scrcpy mini control bars, scrcpy-window capture, configurable auto-hide, and
login-item registration are implemented using macOS APIs.
New Galaxy call, SMS, and application notifications can be forwarded separately
to macOS Notification Center while ADB is connected. The first snapshot is used
only as a baseline and Flow Bridge does not persist notification content.
The phone page can open the Galaxy dialer with a number and hand off a recipient
and draft to the Galaxy messaging app. The final call or message remains a visible
user action on the phone; ADB does not transport cellular call audio to macOS.
When the screen-dimming option is enabled, Flow Bridge keeps the physical panel
powered so the mirror stream stays connected, applies the lowest brightness and
Galaxy Extra dim, and restores the previous state when the session ends.

The distributed build is ad-hoc signed. Developer ID signing and Apple
notarization require the repository owner's private Apple signing identity.

# macOS용 Flow Bridge

핵심 실행 흐름을 SwiftUI로 옮긴 macOS 네이티브 포트입니다. USB·무선 ADB 기기
검색, Samsung DeX overlay 생성·제거, 해당 디스플레이의 scrcpy 실행, Android 앱
단일 가상화면 실행을 지원합니다.

배포 앱에는 공식 scrcpy 4.1 정적 빌드와 ADB가 Apple Silicon·Intel용으로 모두
포함되므로 Homebrew나 별도 런타임 설치가 필요하지 않습니다. USB·무선 자동 검색,
연결 주소 기억 및 자동 재연결, DeX 없이 일반 휴대폰 화면 미러링, 전화·문자·
애플리케이션별 macOS 알림 전달을 지원합니다. 알림은 ADB가 연결된 동안 새로 발생한
항목만 전달하며 Flow Bridge가 알림 내용을 별도 저장하지 않습니다.
화면 어둡게 옵션은 실제 디스플레이 전원을 끄지 않고 밝기 최저와 Galaxy
더 어둡게(Extra dim)를 적용해 미러링 연결을 유지하며, 세션 종료 시 기존 상태를
복원합니다.
