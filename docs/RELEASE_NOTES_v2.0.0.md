# DX Manager v2.0.0

## English

DX Manager v2.0.0 adds simultaneous multi-device management. Each connected
physical Galaxy phone now has its own runtime, settings, DeX and Single-Window
sessions, connection policy, Companion session, and bidirectional file-transfer
state.

### Highlights

- Manage multiple physical Galaxy phones at the same time.
- Run independent DeX and Single-Window sessions on different phones without
  replacing another phone's active session.
- Keep DeX, three Single-Window slots, app profiles, startup settings, and
  additional scrcpy arguments separate for each phone.
- Merge USB and wireless ADB connections that belong to the same physical
  phone while keeping different phones isolated.
- Save USB/Wi-Fi mode, address, port, and automatic reconnection independently
  for every phone.
- Enforce the selected USB or Wi-Fi transport instead of silently falling back
  to the other connection.
- Start DeX automatically for every eligible connected phone when automatic
  start is enabled.
- Show the device selector only after multiple phones are detected, preserve
  friendly phone names after disconnection, and prefer newer Galaxy models
  when several phones are already connected at startup.
- Include the phone display name in every DeX and Single-Window scrcpy title.
- Store phone-to-PC transfers in a separate phone-name subfolder below the
  configured PC destination.
- Keep the Settings window open and refresh it for the newly selected phone.
- Show Android, SDK, One UI, security-patch and transport information for the
  currently selected phone under **Settings > Diagnostics**.
- Save a privacy-redacted diagnostic report with environment, selected-device,
  connected-device and recent warning/error information.

### DX Companion 2.0.0

- Bundle signed DX Companion 2.0.0 (versionCode 6). Installation remains an
  explicit user action under **Settings > Diagnostics > DX Companion**.
- Use an authenticated pre-established Companion session for optional cleanup
  during Windows shutdown without starting a new ADB process.
- Keep the virtual display after a temporary transport loss and cancel pending
  cleanup when the same authenticated session reconnects.
- Offer disconnect cleanup choices in this order: immediately, 1 minute,
  5 minutes, 10 minutes, 30 minutes, or never. The default is 5 minutes.
- Verify the bundled APK hash, official signing certificate, package, version,
  and protected permission before using privileged recovery features.

### Architecture and reliability

- Remove the process-wide ADB target and pass an explicit captured serial to
  every device command.
- Bind DeX, Single-Window, scrcpy, screen-power, Companion, and transfer
  services to one physical-device runtime.
- Scope disconnect cleanup, transfer cancellation, ADB reverse mappings, and
  process ownership to the correct phone.
- Separate normal application exit from Windows session shutdown. Normal exit
  performs full ADB cleanup; Windows shutdown blocks new helper processes and
  uses only an already authenticated Companion connection when available.
- Split large UI, connection-lifecycle, settings-model, and custom-control
  files into clearer feature-focused source files without changing the public
  workflow.

### Compatibility

- 64-bit Windows 7 SP1, 8.1, 10, or 11
- .NET Framework 4.6.2 or later
- Bundled scrcpy 4.1
- Samsung Galaxy device with DeX support; Android 16 / One UI 8.x remains the
  currently verified baseline

Windows-shutdown cleanup is best effort and requires an already authenticated
DX Companion 2.0.0 guardian session. Normal DX Manager exit remains the
recommended cleanup path, especially on Windows 7.

The public ZIP is portable. Extract the complete folder and do not copy only
`DXManager.exe`. DX Companion is included but is never installed without the
user pressing the install button.

### SHA-256

- `DX-Manager-v2.0.0-win-x64.zip`:
  `CA78A306F61235708DBDDE0541C06E071C646DC0F8B47BC8F2487E5518E8AF86`
- `DXManager.exe`:
  `D7EF8F373F1B42A2A4518D06D2BF5956352788614F31598D605BD833E589B6DC`
- Bundled `DX-Companion.apk`:
  `7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`

The Microsoft Defender false-positive submission for this exact
`DXManager.exe` build was reviewed before publication, and the detection was
removed. The source and reproducible package contents remain available in this
repository for independent inspection.

---

## 한국어

DX Manager v2.0.0은 여러 물리 Galaxy 휴대폰을 동시에 관리하는 기능을
추가했습니다. 연결된 휴대폰마다 런타임, 설정, DeX·단일창 세션, 연결 정책,
Companion과 양방향 파일 전송 상태를 독립적으로 유지합니다.

### 주요 변경

- 여러 물리 Galaxy 휴대폰을 동시에 관리합니다.
- 서로 다른 휴대폰에서 DeX와 단일창을 독립적으로 실행하며 다른 휴대폰의
  실행 세션을 교체하지 않습니다.
- 휴대폰마다 DeX, 단일창 3개, 앱 프로필, 시작 설정과 추가 scrcpy 인자를
  별도로 저장합니다.
- 같은 물리 휴대폰의 USB와 무선 ADB 연결은 하나로 합치고, 서로 다른
  휴대폰의 상태는 섞이지 않게 분리합니다.
- USB·Wi-Fi 모드, 주소, 포트와 자동 재연결을 휴대폰별로 저장합니다.
- 사용자가 선택한 USB 또는 Wi-Fi transport만 사용하고 반대 연결로 임의
  전환하지 않습니다.
- 연결 시 DeX 자동 시작을 켜면 조건을 만족하는 각 휴대폰에서 독립적으로
  시작합니다.
- 여러 휴대폰이 확인된 경우에만 기기 선택 영역을 표시하고, 연결이 끊긴
  뒤에도 휴대폰 이름을 유지합니다. 시작 시 여러 대가 이미 연결돼 있으면
  최신 Galaxy 모델을 우선 배치합니다.
- 모든 DeX·단일창 scrcpy 제목에 휴대폰 표시 이름을 포함합니다.
- 휴대폰에서 PC로 받은 파일을 설정한 폴더 아래 휴대폰 이름별 하위 폴더에
  나누어 저장합니다.
- 메인 기기 선택을 바꿔도 설정 창을 닫지 않고 새 대상 정보로 갱신합니다.
- **설정 > 진단**에서 현재 선택된 휴대폰의 Android·SDK·One UI·보안 패치와
  연결 방식을 표시합니다.
- 환경, 선택 기기, 연결 기기와 최근 경고·오류에서 민감 정보를 가린 진단
  보고서를 텍스트 파일로 저장합니다.

### DX Companion 2.0.0

- 서명된 DX Companion 2.0.0(versionCode 6)을 포함합니다. 설치는 여전히
  **설정 > 진단 > DX Companion**에서 사용자가 직접 눌러야만 시작됩니다.
- Windows 종료 시 새 ADB 프로세스를 시작하지 않고, 미리 인증해 연결해 둔
  Companion 세션이 있을 때만 선택적으로 기기 정리를 요청합니다.
- 일시적인 transport 연결 손실에는 가상화면을 유지하고, 같은 인증 세션이
  다시 연결되면 예약된 정리를 취소합니다.
- 연결 해제 후 정리 시간을 즉시, 1분, 5분, 10분, 30분, 자동 정리 안 함
  순서로 제공합니다. 기본값은 5분입니다.
- 권한 기능을 사용하기 전에 번들 APK 해시, 공식 서명, package, 버전과 보호
  권한을 검증합니다.

### 구조와 안정성

- 프로세스 전역 ADB 대상을 제거하고 모든 기기 명령에 작업 시작 시 캡처한
  명시적 serial을 전달합니다.
- DeX·단일창·scrcpy·화면 전원·Companion·전송 서비스를 하나의 물리 기기
  런타임에 결속합니다.
- 연결 해제 정리, 전송 취소, ADB reverse와 프로세스 소유권이 정확한
  휴대폰에만 적용되도록 범위를 고정합니다.
- 일반 프로그램 종료와 Windows 세션 종료를 분리했습니다. 일반 종료는 전체
  ADB 정리를 수행하고, Windows 종료는 새 보조 프로세스를 막은 뒤 이미
  인증된 Companion 연결이 있을 때만 사용합니다.
- 큰 UI·연결 수명주기·설정 모델·사용자 컨트롤 파일을 기능 단위로 분리해
  공개 동작을 유지하면서 유지보수 구조를 개선했습니다.

### 호환성

- 64비트 Windows 7 SP1, 8.1, 10 또는 11
- .NET Framework 4.6.2 이상
- 번들 scrcpy 4.1
- Samsung DeX 지원 Galaxy 기기. 현재 확인 기준은 Android 16 / One UI 8.x

Windows 종료 시 정리는 이미 인증된 DX Companion 2.0.0 guardian 세션이 있을
때만 최선 노력 방식으로 동작합니다. 특히 Windows 7에서는 DX Manager 정상
종료를 권장합니다.

공개 ZIP은 설치가 필요 없는 포터블 패키지입니다. 폴더 전체의 압축을 풀고
`DXManager.exe`만 따로 복사하지 마십시오. DX Companion은 포함되지만 사용자가
설치 버튼을 누르지 않으면 자동으로 설치되지 않습니다.

### SHA-256

- `DX-Manager-v2.0.0-win-x64.zip`:
  `CA78A306F61235708DBDDE0541C06E071C646DC0F8B47BC8F2487E5518E8AF86`
- `DXManager.exe`:
  `D7EF8F373F1B42A2A4518D06D2BF5956352788614F31598D605BD833E589B6DC`
- 번들 `DX-Companion.apk`:
  `7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`

이 `DXManager.exe` 빌드에 대한 Microsoft Defender 오탐 신고는 게시 전에
검토가 끝나 탐지가 해제되었습니다. 소스와 재현 가능한 패키지 구성은 독립적인
확인을 위해 저장소에 공개되어 있습니다.
