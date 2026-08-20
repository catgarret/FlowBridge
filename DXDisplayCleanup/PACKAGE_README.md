# DX Companion 2.0.0

DX Companion is the optional Android companion bundled with DX Manager. It
provides recovery actions, authenticated Windows-shutdown protection, and
phone-to-PC file and folder transfer.

## Install and grant permission

Use **DX Manager > Settings > Diagnostics > DX Companion** to install or update
the bundled APK on the currently selected phone, then grant the protected
cleanup permission. DX Manager verifies the exact APK hash, official signing
certificate, package ID, and version before installation and verifies the
installed app again before granting permission. Installation never starts
automatically.

The app can remove Android's simulated secondary display and turn off Developer
options **Stay awake**. The main screen offers separate and combined cleanup;
the Quick Settings tile and compact 2 x 1 home-screen widget use the targets
selected in the app.

Files can be sent from Gallery or My Files through Android's **Send to DX
Manager** share target. Because One UI does not expose folders through the
normal share sheet, use **Send folder > Select folder** inside DX Companion.
DX Manager must be running with that phone connected.

When the authenticated DX Manager guardian session is lost together with the
selected USB/Wi-Fi transport, Companion can clean recovery state immediately,
after 1, 5, 10, or 30 minutes, or never. The default is 5 minutes; reconnecting
the same authenticated session cancels pending cleanup.

The cleanup action removes Android's single global simulated-display setting,
so it cannot distinguish a DX Manager display from one manually selected in
Developer options. Uninstalling the app removes its protected permission.

The app uses local-network permissions only for its authenticated connection
to DX Manager and does not contain analytics, cloud transfer, or an arbitrary
shell feature. Verify the official signing fingerprint in `SIGNING.md`.

---

# DX Companion 2.0.0

DX Companion은 DX Manager에 포함되는 선택형 Android 보조 앱입니다. 남은
가상화면·절전모드 해제 복구, 인증된 Windows 종료 보호와 휴대폰에서 PC로
파일·폴더 전송을 제공합니다.

## 설치 및 권한 부여

**DX Manager > 설정 > 진단 > DX Companion**에서 현재 선택된 휴대폰에 번들
APK를 설치하거나 업데이트한 뒤 보호된 정리 권한을 부여하십시오. DX Manager는
설치 전에 정확한 APK 해시·공식 서명·package·버전을 확인하고, 권한 부여 전에
설치된 앱을 다시 검증합니다. 설치는 자동으로 시작되지 않습니다.

앱에서는 Android 보조 디스플레이 시뮬레이션 제거와 개발자 옵션의 **절전모드
해제** 끄기를 개별 또는 동시에 실행할 수 있습니다. 빠른 설정 타일과 2 x 1 홈
화면 위젯은 앱에서 선택한 정리 대상을 사용합니다.

파일은 갤러리나 내 파일의 공유 메뉴에서 **DX Manager로 보내기**를 선택합니다.
One UI 공유 메뉴는 폴더를 전달하지 않으므로 폴더는 DX Companion의 **폴더
보내기 > 폴더 선택**을 사용합니다. DX Manager가 실행 중이고 해당 휴대폰이
연결돼 있어야 합니다.

인증된 DX Manager guardian 세션과 선택한 USB·Wi-Fi transport가 함께 끊기면
즉시, 1분, 5분, 10분, 30분 뒤 정리하거나 자동으로 정리하지 않도록 선택할 수
있습니다. 기본값은 5분이며 같은 인증 세션이 다시 연결되면 예약 정리를
취소합니다.

정리 기능은 Android의 단일 전역 보조 디스플레이 설정을 제거하므로 DX Manager가
만든 화면과 개발자 옵션에서 직접 선택한 화면을 구분할 수 없습니다. 앱을
삭제하면 보호 권한도 사라집니다.

로컬 네트워크 권한은 DX Manager와 인증된 연결에만 사용하며 분석 수집, 클라우드
전송과 임의 shell 기능은 없습니다. 공식 서명 지문은 `SIGNING.md`에서 확인할 수
있습니다.
