# DX Manager v1.3.0

## English

DX Manager v1.3.0 adds optional DX Companion integration, phone-to-PC file
transfer, and broader Korean keyboard compatibility.

### What's new

- Send files from Android's Share menu to the connected PC through DX
  Companion.
- Send complete folders with DX Companion's built-in folder picker.
- Preserve original names and resolve Windows name conflicts as `name (1)`.
- Bundle the signed DX Companion 1.3.0 APK under `tools\companion`.
- Install, update, reinstall, grant permission to, or uninstall DX Companion
  from **Settings > Diagnostics** on the currently selected phone.
- Verify the bundled APK hash and official v2 signing certificate before
  installation, then verify the installed package, version, certificate, and
  permission again.
- Improve the Companion UI, swipe navigation, compact 2 x 1 widget, recovery
  actions, and live PC receiver readiness status.
- Recognize Korean laptop Hangul keys reported as `VK_HANGUL` with an extended
  Alt-position scan code while leaving Brazilian and European AltGr input
  untouched.

DX Companion is never installed automatically. Its install and permission
actions always require an explicit user command and apply only to the phone
currently selected by DX Manager.

### Package verification

- Windows executables: x64, .NET Framework 4.6.2, version 1.3.0
- Bundled scrcpy: 4.1
- DX Companion APK SHA-256:
  `3876D4B7F0CCE6EC3C6CE9F930959757ED32668B3BDAE1D34F744A894039A452`
- Windows ZIP SHA-256:
  `EFE33245862E1D1B57603DB7E64DFB02CED38CB0751B56155CC44F4D7C504848`

## 한국어

DX Manager v1.3.0은 선택형 DX Companion 연동, 휴대폰에서 PC로 파일 전송과
한국어 키보드 호환성 개선을 추가했습니다.

### 새로운 기능

- Android 공유 메뉴에서 DX Companion을 선택해 연결된 PC로 파일을 전송합니다.
- DX Companion의 폴더 선택 기능으로 폴더 전체를 전송합니다.
- 원래 파일명을 유지하고 Windows에 같은 이름이 있으면 `이름 (1)` 형식으로
  충돌을 피합니다.
- 서명된 DX Companion 1.3.0 APK를 `tools\companion`에 포함합니다.
- **설정 > 진단**에서 현재 선택된 휴대폰에 DX Companion을 설치·업데이트·
  재설치하거나 권한을 부여하고 삭제할 수 있습니다.
- 설치 전 번들 APK 해시와 공식 v2 서명을 확인하고, 설치 뒤 package·버전·
  서명과 권한을 다시 검증합니다.
- Companion UI, 좌우 스와이프, 2 x 1 위젯, 복구 기능과 PC 수신 준비 상태
  표시를 개선했습니다.
- `VK_HANGUL`과 Alt 위치의 extended scan code로 보고되는 일부 노트북 한영키를
  지원하면서 브라질·유럽 키보드의 AltGr 입력은 그대로 통과시킵니다.

DX Companion은 자동으로 설치되지 않습니다. 설치와 권한 작업은 사용자가
직접 실행해야 하며 DX Manager가 현재 선택한 휴대폰에만 적용됩니다.

### 패키지 검증

- Windows 실행 파일: x64, .NET Framework 4.6.2, 버전 1.3.0
- 포함된 scrcpy: 4.1
- DX Companion APK SHA-256:
  `3876D4B7F0CCE6EC3C6CE9F930959757ED32668B3BDAE1D34F744A894039A452`
- Windows ZIP SHA-256:
  `EFE33245862E1D1B57603DB7E64DFB02CED38CB0751B56155CC44F4D7C504848`
