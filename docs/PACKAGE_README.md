# DX Manager

Portable Windows package for managing a Samsung DeX virtual display and up to
three independent scrcpy app windows. This release bundles scrcpy 4.1.

## English

### Requirements

- 64-bit Windows 7 SP1, 8.1, 10, or 11
- .NET Framework 4.6.2 or later
- A Samsung device that supports Samsung DeX
- Android Developer options and USB debugging enabled
- A data-capable USB cable for the initial authorization

The currently verified phone baseline is Android 16 with One UI 8.x on a
DeX-capable Galaxy device. One UI 7.x and earlier have not been confirmed to
work reliably and may show a black DeX window.

Windows 7 and 8.1 may require Universal CRT updates for the bundled legacy
ADB. Wireless ADB also requires the PC and phone to communicate on the same
local network.

Windows 7 SP1 does not include .NET Framework 4.6.2 by default. Install it from
the [official Microsoft download page](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462)
if necessary; an offline installer is available there. If .NET Framework 4.7.2
or 4.8 is already installed, no downgrade or separate 4.6.2 installation is
required.

### Quick start

1. Extract the entire ZIP to a user-writable folder.
2. Enable Developer options and USB debugging on the phone.
3. Connect the phone by USB and approve the RSA debugging prompt.
4. Run `DXManager.exe`.
5. Wait until the connected phone is shown, then select **Start DeX**.

Do not copy only `DXManager.exe`. Keep the adjacent `tools`, DLL, license,
language, and documentation files together.

### Default settings

- DeX and single windows: 1600 x 900, 150 DPI, 8 Mbps, 60 FPS
- Delay after device detection: 1 second
- Start minimized to tray: Off
- Start DeX automatically on connection: Off
- Automatic hiding: Off
- Ignore direct Shift+Space input: Off
- Scroll Lock Enter/Shift+Enter switching: Off
- Stay awake while a managed window is running: On
- DX Manager Unicode-compatible file transfer: On

### Drag-and-drop file transfer

Drop one or more files onto a DeX or single-app window to copy them to the
phone's `Download` folder. The default DX Manager transfer uses a Windows
7/11-compatible path that preserves Korean and other Unicode file names. Its
status window shows the current file, progress,
completed/failed/waiting counts, and a cancel action. Existing names are kept;
a new file is saved as `name (1).ext`, `name (2).ext`, and so on.

To use scrcpy's original behavior instead, turn off **Use DX Manager file
transfer (Unicode-compatible)** under **Settings > Paths / ADB > Programs and
storage paths**. The change applies to newly opened DeX and single-app windows.

### Default shortcuts

- `F8`: Enter capture mode while a scrcpy window is active
- `F8` again: Capture the scrcpy client area
- Mouse drag: Capture a selected PC screen region
- `Esc`: Cancel capture mode
- `Left Alt+F8`: Exit DX Manager
- `Scroll Lock`: Toggle Enter/Shift+Enter mode when that option is enabled

### Documentation

- [English user guide](docs/USER_GUIDE_EN.md)
- [English FAQ](docs/FAQ_EN.md)
- [Korean user guide](docs/USER_GUIDE_KO.md)
- [Korean FAQ](docs/FAQ_KO.md)
- [Third-party notices](licenses/THIRD_PARTY_NOTICES.md)
- [DX Manager MIT License](LICENSE)

Project page: https://github.com/maze-mei/DX-Manager

---

## 한국어

Samsung DeX 가상 디스플레이와 앱별 scrcpy 단일창을 최대 3개까지 관리하는
Windows용 포터블 프로그램입니다. 이 릴리스는 scrcpy 4.1을 포함합니다.

### 요구 사항

- 64비트 Windows 7 SP1, 8.1, 10 또는 11
- .NET Framework 4.6.2 이상
- Samsung DeX를 지원하는 삼성 기기
- Android 개발자 옵션과 USB 디버깅 활성화
- 최초 인증을 위한 데이터 전송 가능 USB 케이블

현재 정상 동작을 확인한 휴대폰 기준은 Android 16 / One UI 8.x의 DeX 지원
Galaxy 기기입니다. One UI 7.x 이하에서는 원활한 동작을 확인하지 못했으며
DeX 창이 검게 표시될 수 있습니다.

Windows 7과 8.1에서는 동봉된 레거시 ADB를 위해 Universal CRT 업데이트가
필요할 수 있습니다. 무선 ADB는 PC와 휴대폰이 같은 로컬 네트워크에서 직접
통신할 수 있어야 합니다.

Windows 7 SP1에는 .NET Framework 4.6.2가 기본 포함되지 않습니다. 필요한
경우 [Microsoft 공식 다운로드
페이지](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462)에서
설치하십시오. 같은 페이지에서 오프라인 설치 파일도 받을 수 있습니다.
4.7.2 또는 4.8이 이미 설치되어 있다면 4.6.2로 낮추거나 추가 설치할 필요가
없습니다.

### 빠른 시작

1. ZIP 전체를 쓰기 가능한 폴더에 압축 해제합니다.
2. 휴대폰에서 개발자 옵션과 USB 디버깅을 활성화합니다.
3. USB로 연결한 뒤 휴대폰의 RSA 디버깅 허용 창을 승인합니다.
4. `DXManager.exe`를 실행합니다.
5. 연결된 휴대폰이 표시되면 **DeX 시작**을 선택합니다.

`DXManager.exe`만 따로 복사하면 동작하지 않습니다. 같은 폴더의 `tools`, DLL,
라이선스, 언어 및 문서 파일을 함께 유지하십시오.

### 기본 설정

- DeX와 단일창: 1600 x 900, 150 DPI, 8 Mbps, 60 FPS
- 기기 인식 후 시작 대기: 1초
- 트레이로 최소화하여 시작: 꺼짐
- 기기 연결 시 DeX 자동 시작: 꺼짐
- 자동 숨김: 꺼짐
- 직접 Shift+Space 입력 무시: 꺼짐
- Scroll Lock Enter/Shift+Enter 전환: 꺼짐
- 관리 창 실행 중 잠자기 방지: 켜짐
- DX Manager 한글·Unicode 호환 파일 전송: 켜짐

### 드래그 앤 드롭 파일 전송

DeX 또는 단일창에 파일을 놓으면 휴대폰의 `Download` 폴더로 복사합니다.
기본 DX Manager 전송은 Windows 7/11 호환 경로로 한글을 포함한 Unicode
파일명을 보존합니다. 상태창에서 현재 파일, 진행률,
완료·실패·대기 수를 확인하고 전송을 취소할 수 있습니다. 같은 이름이 있으면
기존 파일을 유지하고 `이름 (1).확장자`, `이름 (2).확장자` 순서로 저장합니다.

scrcpy 순정 방식을 사용하려면 **설정 > 경로 / ADB > 프로그램 및 저장 경로**의
**DX Manager 파일 전송 사용 (한글/Unicode 호환)**을 끄십시오. 변경은 새로
여는 DeX와 단일창부터 적용됩니다.

### 기본 단축키

- `F8`: scrcpy 창 활성 상태에서 캡처 모드 진입
- `F8` 다시 누르기: scrcpy 창 영역 캡처
- 마우스 드래그: PC 화면의 선택 영역 캡처
- `Esc`: 캡처 취소
- `Left Alt+F8`: DX Manager 종료
- `Scroll Lock`: 해당 옵션 사용 시 Enter/Shift+Enter 모드 전환

### 문서

- [English user guide](docs/USER_GUIDE_EN.md)
- [English FAQ](docs/FAQ_EN.md)
- [한국어 사용 설명서](docs/USER_GUIDE_KO.md)
- [한국어 자주 묻는 질문](docs/FAQ_KO.md)
- [제3자 고지](licenses/THIRD_PARTY_NOTICES.md)
- [DX Manager MIT 라이선스](LICENSE)

프로젝트 페이지: https://github.com/maze-mei/DX-Manager
