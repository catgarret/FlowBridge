# Technical Notes

## 주요 구조

`Program.cs`가 설정을 읽고 서비스를 조립한다.

- `PathService`: OS/설정별 ADB 선택
- `AdbService`: target serial을 포함한 ADB 실행
- `WirelessAdbService`: USB 준비, TCP/IP, 페어링, 재연결
- `VirtualDisplayService`: DeX overlay와 ID 탐색
- `DexOrchestrator`: DeX 실행/정리
- `ScrcpyService`: DeX Scrcpy
- `SingleWindowService`: 단일창 3개
- `ScrcpyLaunchCoordinator`: Scrcpy 시작 직렬화
- `ScreenOffService`: 남은 세션의 화면 OFF 재적용
- `KeyMappingService`: Scrcpy 활성 상태 키 보정
- `CaptureCoordinator`: F8 캡처
- `FileTransferCoordinator`: scrcpy 파일 드롭 IPC, 전역 FIFO 전송과 상태 관리

## ADB

모든 명령은 선택한 `adb.exe`의 절대 경로로 실행한다.

1. 수동 모드면 지정 ADB 사용
2. Windows 10 미만이면 `tools\adb\legacy\adb.exe`
3. Windows 10 이상이면 선택한 Scrcpy 폴더의 `adb.exe` 사용
4. Scrcpy 폴더의 ADB가 없거나 실행되지 않으면 legacy ADB 사용

설정, 무선 연결, wake-up과 화면 상태를 포함한 DX Manager 자체 명령은 선택한
실제 ADB의 절대 경로로 실행한다. 관리형 파일 전송을 켠 DeX·단일창 scrcpy
프로세스만 `ADB` 환경 변수에 `tools\adb-proxy\DXMAdbProxy.exe`를 지정한다.

`adb version`의 첫 줄인 `Android Debug Bridge version 1.0.41`은 여러
platform-tools 버전이 공통으로 출력하는 프로토콜 문구다. 설정, 환경 점검과
로그에는 정규식으로 다음 `Version ...` 줄을 파싱한 실제 빌드 값을 표시한다.

일부 Windows 7 환경에서는 `adb start-server` 직후 ADB 프로세스가 반복
종료되거나 USB transport를 정상적으로 잡지 못할 수 있다. DX Manager는
기본적으로 ADB로 먼저 깨우고, 실패 시 설정에 따라 Scrcpy 기반 wake-up을
사용해 실제 push/shell/stream 경로까지 열어 ADB 연결을 초기화한다.

현재는 ADB 상태와 기기 이름을 즉시 확인한 뒤, DeX/단일창 실제 시작 명령
직전에 `ConnectedStartDelayMs`를 적용한다. 범위는 0~60초, 기본값은 1초다.
화면 OFF 재적용용 Scrcpy에는 이 대기를 적용하지 않는다.

## 관리형 파일 전송

기본값은 켜짐이며 설정 스키마의 `Features.ManagedFileTransferEnabled`에
저장한다. 설정 변경은 새로 시작하는 DeX·단일창 세션부터 적용한다. 끄거나
도우미가 없으면 실제 ADB를 scrcpy에 전달해 순정 파일 드롭 동작으로
되돌린다.

`DXMAdbProxy.exe`는 세션별 임의 named pipe, 토큰, 세션 ID, target serial,
세션 시작 시 고정한 휴대폰 대상 폴더와 실제 ADB 경로를 환경 변수로 받는다.
일반 ADB 명령은 인자와 표준 입출력을 유지해 실제 ADB로 전달하며, 해당 대상
폴더로 보내는 파일 또는 폴더 `push`만 DX Manager에 요청한다. scrcpy-server
push, shell과 단독 APK 설치는 가로채지 않는다.

`FileTransferCoordinator`는 다음 순서를 사용한다.

1. 요청 토큰, 세션, 고정된 기기 serial과 세션 대상 폴더를 검증한다.
2. 프로세스 제한시간과 분리된 전송 전용 프로세스로 전역 FIFO 큐를 처리한다.
   큐에 들어간 요청은 terminal 상태까지 별도 registry에도 유지해 dequeue와
   active 등록 사이의 짧은 순간에도 취소·세션 종료 요청을 놓치지 않는다.
3. 단일 파일은 `/sdcard/.dxm-file-GUID.part` ASCII 임시 이름으로 `adb push`한
   뒤 UTF-8/Base64 이름을 복원한다. 대상 폴더가 없으면 먼저 생성한다.
4. 폴더는 로컬에서 재귀 열거하고 정션·심볼릭 링크 등 재분석 지점을
   건너뛴다. 대상 폴더 안의 숨은 `.dxm-dir-GUID.part` staging 폴더에 파일을
   하나씩 전송하고 하위 폴더와 빈 폴더를 만든다.
5. 폴더 전체가 성공한 뒤에만 staging 폴더를 최종 최상위 이름으로 이동한다.
   취소나 실패 시 보이지 않는 staging과 현재 임시 파일을 정리한다.
6. Android의 UTF-8 255바이트 경로 구성요소 제한을 확인하고 같은 이름과
   충돌하면 파일은 `(1)`, `(2)`, 폴더는 `폴더 (1)` 방식으로 원자적으로
   이름을 정한다.
7. 최종 이름으로 이동하기 직전에 `/data/local/tmp/.dxm-commit-GUID.result`
   표식을 `P(준비)` 상태로 기록하고 이동이 끝나면 `C(완료)`로 바꾼다. ADB
   응답이나 제한시간이 이동 직후 끊겨도 임시 경로와 표식을 제한된 시간 동안
   재조회해 실제 커밋 결과를 복구한다. 결과가 아직 불명확하면 일반 실패 정리가
   증거를 먼저 지우지 않으며, 다음 유휴 첫 세션 준비 때 DX Manager 전용
   `.part`와 남은 표식을 정리한다.
8. 큰 폴더의 하위 디렉터리 생성 스크립트는 ADB 표준입력 전용 background
   writer로 보내므로 pipe write가 막혀도 취소와 제한시간 감시는 계속된다.

상태창은 현재 항목과 다음 대기 항목 4개, 원본 크기, 경과 시간과 누적
완료·실패·대기 수를 표시한다. 지원하는 모든 Windows/ADB 조합에서 신뢰할 수
있는 전송 바이트를 얻지 못하므로 퍼센트와 남은 시간은 표시하지 않는다.
사용자가 취소하거나 원본 scrcpy 창이 종료되면 해당 세션의 현재·대기 ADB
push와 shell을 중단하고 임시 데이터를 정리하며, 같은 드롭에서 이어지는
요청도 짧은 취소 구간 동안 거부한다. 최종 commit 구간에는 취소를 잠시 막는다.

## 무선 ADB

- 승인된 USB 장치가 정확히 하나일 때 준비한다.
- IP가 비어 있으면 `adb -s SERIAL shell ip route`의 `wlan` 경로에서
  `src` IPv4 주소를 우선 찾는다.
- `adb -s SERIAL tcpip PORT` 후 USB를 꽂아둔 채 `adb connect`한다.
- 성공 시 target serial을 `IP:PORT`로 바꾼다.
- 자동 재연결은 최소 5초 간격이다.
- 페어링 포트와 실제 연결 포트는 다를 수 있다.
- 페어링 코드는 저장하거나 로그에 남기지 않는다.

## v1 기기 고정

`DeviceMonitorService`는 처음 선택한 기기를 앱 수명 동안 고정한다.
기기 식별은 `ro.serialno`, `ro.boot.serialno`, 마지막 수단으로 Android ID를
사용한다. 따라서 같은 휴대폰의 USB serial과 무선 `IP:PORT`가 달라도 같은
기기로 판정할 수 있다.

고정된 기기가 사라지면 연결된 다른 휴대폰을 선택하지 않고 Disconnected를
발행한다. 원래 휴대폰이 돌아오면 기존 연결 대기와 자동 실행 흐름을 다시
적용한다. 무선 주소가 사라졌다가 다시 나타나면 캐시를 폐기하고 실제 기기
식별값을 다시 읽어 다른 휴대폰의 주소 재사용을 방지한다.

기기가 보이지 않는 동안에도 ADB target serial은 고정값을 유지한다. 선택
서비스가 `targetWhenUnavailable`을 사용하므로 감시 주기마다 target을 비웠다가
다시 넣지 않는다. 백그라운드 장치 조회만 계속하고 연결 상태 이벤트는 실제
변경 시 한 번만 발행한다.

## DeX display ID

DeX는 `overlay_display_devices`로 만든다. 생성 전후 `dumpsys display`
스냅샷의 차집합을 구하고, 후보가 여러 개면 해상도/DPI로 좁힌다.
여전히 모호하면 실패시키고 후보를 로그에 남긴다. 최대 ID fallback은 없다.

기존 overlay가 있으면 문자열 전체가 아니라 실제 너비, 높이, DPI 숫자를
설정과 비교한다. 모두 같으면 ID를 등록해 재사용하고, 하나라도 다르면
`settings delete global overlay_display_devices`로 설정 항목 자체를 삭제한 뒤
다시 만든다. 정상 종료 시에도 생성 주체와 무관하게 같은 delete 명령으로
관리 기기의 overlay를 정리한다.

## Android 가상화면 정리 앱

`DXDisplayCleanup`은 `io.github.mazemei.dxdisplaycleanup` 패키지의 선택형 Android
앱이다. 공개 이름은 DX Companion이다. 복구 기능은 `Settings.Global`의
`overlay_display_devices`를 읽고 삭제하며 절전모드 해제를 끌 수 있다. 일반
앱에서 승인할 수 없는 `WRITE_SECURE_SETTINGS`를 ADB로 한 번 부여해야 한다.
삭제는 Settings provider의 `DELETE_global` call을 사용하고 다시 읽어 실제
제거 여부를 검증한다.

앱은 인터넷 권한, 임의 shell과 상시 background service를 포함하지 않는다.
파일은 사용자가 Android 공유 메뉴나 시스템 폴더 선택기에서 명시적으로 고른
항목만 기기별 ADB reverse 수신 세션으로 전송한다. 메인 화면의 버튼 외에 Quick Settings `TileService`와
`AppWidgetProvider`를 제공한다. 활성은 컬러 DX 아이콘, 비활성은 흑백,
권한 없음/오류는 경고 상태로 구분한다.

`overlay_display_devices`는 Android 전역 설정 하나이므로 생성 주체를 구분할
수 없다. 정리 앱은 현재 값을 삭제한 뒤 다시 읽어 비활성을 확인하며, 앱을
삭제했다가 다시 설치하면 package 권한도 사라져 ADB 권한 부여가 다시 필요하다.

DX Manager는 정확한 package ID만 믿지 않는다. 배포 폴더의 APK는 설치 전에
고정된 파일 SHA-256과 APK Signature Scheme v2 인증서를 검사한다. 휴대폰에
설치한 뒤 `base.apk`를 임시 폴더로 pull하고 단일 X.509 인증서, package와
versionCode를 다시 확인한다. 연결 serial을 고정한 채 권한 부여 직전과 직후에
재검증하고 사후 검증이 실패하면 즉시 `pm revoke`한다. 임시 APK는 검사 후
삭제한다. 설치·업데이트·재설치·삭제는 사용자가 누른 현재 선택 기기에만 적용한다.

휴대폰 폴더 탐색은 `/storage/emulated/0` 아래의 디렉터리 목록을 NUL 구분
UTF-8 바이트로 만들고 Base64로 받아 한글·Unicode와 공백을 보존한다. UI와
설정에는 같은 저장소의 사용자 표기인 `/sdcard/...`를 사용한다.

## 단일창

단일창은 DeX overlay가 아니라 Scrcpy가 직접 만든다.

```text
--new-display=WIDTHxHEIGHT/DPI
--start-app=PACKAGE
--window-title "APP NAME"
```

슬롯마다 프로세스와 설정을 따로 관리한다. 강제 종료는
`--start-app=+PACKAGE`이며 기본값은 꺼짐이다. 앱 이름과 패키지를 함께
저장해 명령에는 패키지, UI와 창 제목에는 이름을 쓴다.

## Scrcpy와 화면 상태

옵션은 각 프로필 설정에 따라 붙는다.

- HID 키보드 `-K`
- HID 마우스 `-M`
- 화면 끄기 `-S --no-power-on`
- Scrcpy 4.0 이상 잠자기 방지 `--keep-active`
- Scrcpy 3.3.4 잠자기 방지 `-w`
- Scrcpy 4.0 이상 단일창 동적 크기 `--flex-display`

시작 시 `scrcpy --version`을 실행해 Scrcpy와 SDL 주 버전을 저장한다.
Scrcpy 3.3.4에서는 지원하지 않는 `--keep-active`와 `--flex-display`를
전달하지 않는다. Scrcpy 4.x/SDL3 출력은 UTF-8, 3.3.4/SDL2와 구형 ADB의
로컬 경로 출력은 Windows 기본 코드 페이지로 읽는다.

기준 번들은 Scrcpy 4.1/SDL 3.4.12다. 4.0에서 확인된 오른쪽 Shift 전달
문제를 위해 SDL3 기반 4.x 클라이언트에는 현재 호환 치환을 적용한다.

한 창이 끝나도 다른 세션이 화면 OFF를 원하면 `ScreenOffService`가 보이지
않는 Scrcpy를 다음 옵션으로 직렬 실행한다.

```text
--no-video --no-audio --no-window -S --no-power-on --no-cleanup
```

`Device display turned off`를 확인한 뒤 종료한다. 메인 Scrcpy와 동시에
시작하지 않는다. 모든 세션 종료, 프로그램 종료, 연결 해제 시 화면과
stay-awake 상태를 복구한다.

## 키 입력

- LowLevelKeyboardHook은 Scrcpy 활성 상태에서만 보정한다.
- 한영키는 `scanCode == 0xF2`로 감지한다.
- SendInput scan-code Left Shift+Space를 우선 사용한다.
- SC1F2 KeyUp이 오지 않는 환경을 위해 주입 작업 완료 후 반복 방지 상태를
  반드시 해제한다.
- Enter 변환 기능의 시작 상태는 일반 Enter다.
- Scroll Lock으로 일반 Enter/Shift+Enter를 전환한다.
- SendInput 실패 시 ADB keycombination fallback이 있다.
- 오른쪽 Alt는 한영키와 분리한다.
- SDL3 기반 Scrcpy에서는 물리 오른쪽 Shift(`vk=0xA1`, `scan=0x36`)를
  차단하고 SendInput 왼쪽 Shift scan code로 치환한다.
- 오른쪽 Shift 치환은 Scrcpy 창이 활성화된 동안에만 적용하며 SDL2와
  다른 Windows 앱에는 적용하지 않는다.

Win32 `INPUT` 구조체 정렬을 임의로 바꾸면 32비트 Windows에서 SendInput이
실패할 수 있다.

## 캡처, 숨김, 설정

F8 캡처는 Scrcpy를 전면으로 가져오고 오버레이를 표시한다. 캡처 직전
오버레이를 숨기고 약 100ms 기다린다. 전체 캡처는 client area만 찍는다.

자동 숨김은 설정 시간 동안 입력이 없으면 Scrcpy와 모든 UI를 숨겨 트레이로
보낸다. 이후 입력만으로 자동 복구하지 않는다.
Scrcpy 프로세스가 종료되는 순간과 타이머가 겹쳐도 프로세스 참조를 한 번만
고정해 읽으며, 타이머 예외는 UI 스레드 밖으로 전파하지 않고 로그만 남긴다.

- 설정: 실행 폴더의 `config\settings.json`
- 현재 스키마: 20
- DPI 입력 범위는 120~640이며 120 미만은 입력 확정 시 편집 전 값으로 복원한다.
- 사용자 지정 해상도는 프리셋과 별도로 보존하며 가로·세로 상한은 각각 4096
- 성공적으로 자동 실행한 앱은 최근 20개까지 공통 목록으로 보존
- 로그는 현재 실행 세션만 유지하고 수동으로 저장
- 경로/추가 인자 필드는 커스텀 프레임 안의 네이티브 TextBox를 사용해
  드래그 선택, 긴 문자열 가로 스크롤, `Ctrl+Z`와 IME를 지원
