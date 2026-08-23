# Changelog

세부 변경은 Git 이력을 사용하고 여기에는 큰 이정표만 적는다.

## Unreleased - experimental macOS port

- Moved the FPS guidance directly beneath the frame-rate presets instead of below the expert manual settings disclosure.
- FPS 안내를 전문가 수동 설정 아래에서 제거하고 프레임 프리셋 바로 아래로 옮겼습니다.
- Rebuilt app-presence and control-bar-position settings as compact contiguous segments with fixed widths and consistent trailing alignment.
- 앱 표시 위치와 화면 제어 바 위치를 고정 폭의 촘촘한 세그먼트로 다시 구성해 두 행 모두 오른쪽 끝에 일관되게 정렬했습니다.
- Replaced the draggable floating session remote with a full-width status bar docked inside the top or bottom of each DEX and mirror window, scoped its window ordering to the video window, and separated the volume label from its slider.
- 드래그식 플로팅 화면 리모컨을 제거하고 DEX·미러링 창 내부 상단 또는 하단에 붙는 전체 폭 상태 표시줄로 바꿨습니다. 영상 창 위에서만 표시되도록 순서를 제한하고 볼륨 라벨과 슬라이더 간격을 분리했습니다.
- Removed vertical alphabet rails while retaining localized section sorting, resized the app picker to fit, removed its redundant search launcher, flush-aligned launch mode, staged outbound files before explicit transfer, auto-loaded inbound files on tab entry, and tightened right-aligned Settings controls.
- 세로 알파벳 색인은 제거하되 로컬 섹션 정렬은 유지하고, 앱 검색 창을 화면 안에 맞게 줄였으며 중복 앱 검색 버튼을 제거했습니다. 보낼 파일은 명시적 전송 전까지 대기 목록에 쌓이고, 가져오기는 탭 진입 시 자동 갱신되며, 설정 컨트롤은 우측 정렬과 간격을 정리했습니다.
- Removed the redundant display-quality Save button, made FPS presets persist immediately, and matched the About heading to the bundled Flow Bridge app icon with clearer license line breaks.
- 중복된 화면 품질 저장 버튼을 제거하고 FPS 프리셋도 즉시 저장되도록 맞췄으며, 정보 화면 제목에 실제 Flow Bridge 아이콘을 사용하고 라이선스 문장을 개행했습니다.
- Removed the detail view's fixed width cap, rebalanced the app picker header/list/footer, flattened Home connection setup to one disclosure level, added Messages search clearance, and locale-sorted contacts by name.
- 상세 화면의 고정 폭 제한을 제거하고 앱 검색 창의 헤더·목록·푸터 균형을 조정했습니다. 홈 연결 설정은 한 단계로 평탄화하고 문자 검색창 여백과 연락처 이름 기준 로컬 정렬을 추가했습니다.
- Flattened Settings into named sections with single grouped row surfaces, consistent separators, left labels, and right-aligned controls while preserving every existing option.
- 설정 화면을 섹션 제목·단일 행 그룹·일관된 구분선·좌측 라벨·우측 컨트롤 구조로 단순화하고 기존 설정 기능은 모두 유지했습니다.
- Standardized the Calls/Messages, File Transfer, and Notifications switchers as full-width equal halves; restored the compact app launch-mode picker; and removed the empty Messages toolbar reservation.
- 통화·메시지, 파일 전송, 알림 전환을 전체 폭 50:50 공통 컴포넌트로 통일했습니다. 앱 실행 방식은 작은 우측 정렬 선택기로 복원하고 문자 화면의 빈 도구 영역을 제거했습니다.
- Moved per-page refresh into the status bar as a full-row icon and relative last-updated action, and removed the oversized header button.
- 화면별 새로고침을 상단 버튼에서 하단 상태 표시줄의 아이콘·상대 업데이트 시간 버튼으로 옮겼습니다.
- Removed redundant Quick Launch chevrons and stabilized the phone sidebar toolbar/search geometry across Calls and Messages with cleaner search padding.
- 앱 바로 실행의 불필요한 화살표를 제거하고 전화·문자 전환 시 좌측 도구·검색 영역 위치가 움직이지 않도록 고정했으며 검색창 여백을 정리했습니다.
- Replaced the oversized File Transfer and Notifications page bars with the same compact centered switcher used by Calls and Messages.
- 파일 전송·알림의 과도하게 넓은 탭을 제거하고 전화·문자와 같은 중앙형 전환 규격으로 통일했습니다.
- Right-aligned the app launch-mode control and widened its segments so the phone-mirroring label has comfortable spacing.
- 앱 실행 방식 전환을 우측 끝에 정렬하고 휴대폰 미러링 모드 문구의 세그먼트 내부 여백을 넓혔습니다.
- Restored Calls and Messages to a compact centered switcher while keeping a larger hit target and unmistakable selected state.
- 전화·문자 전환은 넓은 페이지 탭에서 분리해 중앙의 간결한 전환 컨트롤로 되돌리고 클릭 영역과 선택 상태만 명확하게 개선했습니다.
- Rebuilt Quick Launch selection around directly editable slots, repaired the app picker spacing and alphabet rail, labeled the launch-mode control, standardized primary in-page switchers, and aligned Mac import copy and icon color with Galaxy send.
- 바로 실행 슬롯 전체에서 앱을 직접 지정·변경하도록 고치고 앱 검색 창 여백과 색인 막대를 재구성했습니다. 실행 방식 라벨과 주요 화면 전환 탭 규격을 통일하고 Mac 가져오기의 불필요한 문구 및 아이콘 색상을 Galaxy 보내기와 맞췄습니다.
- Simplified Home to a titleless device surface with full-row connection expansion, made Galaxy Notifications the default, equalized app-location controls, cleaned About links/update actions, and fixed and reordered the phone dialer controls.
- 홈을 제목·썸네일 없는 기기 화면으로 단순화하고 연결 설정 행 전체를 클릭 가능하게 했습니다. Galaxy 알림을 기본 화면으로 바꾸고 앱 표시 위치 버튼 폭, 정보 화면 링크·업데이트 동작을 정리했으며 전화 다이얼 표시 버그와 도구 순서를 수정했습니다.
- Flattened Home device controls and sidebar status labels, moved launch progress into the selected screen action, fixed keyguard false positives and zero-brightness recovery, and made the larger session remote draggable with edge snapping and faster window tracking.
- 홈의 중첩 박스와 사이드바 기기 표시를 정리하고 화면 실행 진행 상태를 선택한 실행 영역으로 옮겼습니다. 잠금 오탐과 0 밝기 복원을 수정하고 영상 리모컨을 더 크게 조정해 드래그·가장자리 스냅·빠른 창 추적을 지원합니다.
- Redesigned Home screen-mode actions and right-aligned settings rows, corrected mDNS Wi-Fi labels, removed transport serials from the UI, split notification views, simplified Mac file import, added exact-time tooltips, and rejected corrupt contact-photo cache entries.
- 홈 화면 실행 버튼과 설정 행을 재디자인하고 mDNS Wi-Fi 표시를 바로잡았으며 transport serial 노출을 제거했습니다. 알림 화면을 분리하고 Mac 파일 가져오기를 단순화했으며 정확한 날짜·시간 툴팁과 손상된 연락처 사진 캐시 검증을 추가했습니다.
- Reworked spacing, card hierarchy, connection setup, phone message bubbles, and content width; made disconnected views cache-readable but action-safe; split file transfer into prominent send/receive modes; and moved log export into Diagnostics.
- 공통 여백·카드 위계·연결 설정·문자 말풍선·콘텐츠 폭을 정리하고, 연결 해제 시 캐시는 열람하되 기기 작업은 막도록 개선했으며, 파일 전송을 보내기·가져오기 전환 화면으로 분리하고 로그 저장을 진단으로 옮겼습니다.
- Fixed repeated Galaxy notifications being suppressed, surfaced macOS delivery errors, added lazy cached app icons across notifications/search/quick launch, and matched FPS button spacing to resolution presets.
- 반복 게시된 Galaxy 알림이 누락되던 문제를 수정하고 macOS 전달 오류를 표시하며 알림·앱 검색·바로 실행에 지연 로딩 앱 아이콘 캐시를 적용하고 FPS 버튼 간격을 해상도 프리셋과 통일했습니다.
- Cleaned legacy and Flow Bridge-owned DEX overlays on reconnect when no screen session is active, preferred the Galaxy-configured device name, and hid the redundant call-list picker label.
- 화면 세션이 없을 때 재연결 시 과거 및 Flow Bridge 소유 DEX 오버레이를 정리하고 Galaxy에 설정된 기기 이름을 우선 표시하며 불필요한 통화 목록 라벨을 숨겼습니다.
- Aligned the App Search action to the right side of the Quick Launch Assignments card header.
- 앱 검색 버튼을 앱 바로 실행 지정 카드 제목과 같은 줄의 오른쪽 끝으로 옮겼습니다.
- Moved DEX and phone-mirroring actions directly into the connected-device row and removed inactive accent tint from Calls and Messages lists.
- DEX와 휴대폰 미러링 실행 버튼을 연결된 기기 행 오른쪽으로 옮기고 전화·문자 목록의 비선택 강조색을 제거했습니다.
- Replaced the compact call controls with a native-style dial pad, direct phone-number entry, and a Galaxy handoff toast while removing redundant call-audio copy and hang-up controls.
- 좁고 복잡했던 통화 제어를 직접 번호 입력·원형 키패드·Galaxy 확인 토스트가 있는 다이얼로 교체하고 불필요한 통화 음성 안내와 끊기 버튼을 제거했습니다.
- Rebuilt Calls and Messages as a window-filling split view with independently scrolling lists, redesigned quick-launch assignment and sidebar device controls, and added live Galaxy notification browsing and dismissal.
- 전화·문자를 창 높이에 맞는 독립 스크롤 분할 화면으로 재구성하고 앱 바로 실행 지정·사이드바 기기 제어를 개선했으며, Galaxy 현재 알림 조회와 삭제를 추가했습니다.
- Persisted and restored both Galaxy brightness and automatic-brightness mode strictly around active screen sessions, including option-off, app quit, and crash-recovery paths.
- Galaxy 밝기와 자동 밝기 모드를 화면 세션 실행 중에만 변경하고 옵션 해제·앱 종료·비정상 종료 후 재실행에도 복원하도록 수정했습니다.
- Integrated screen launch controls into the connected-device card, clarified automatic brightness restoration with contextual help, added cached Galaxy contact photos with explicit row selection, and aligned display-quality controls.
- 연결된 기기 카드에 화면 실행 기능을 통합하고 밝기 자동 복원 도움말을 정리했으며, Galaxy 연락처 사진 캐시·명확한 행 선택 표시·화면 품질 설정 정렬을 추가했습니다.

- Remove duplicate phone refresh controls, center the Calls/Messages switch, clamp
  message previews, move app search into a dedicated assignment sheet, standardize
  DEX/mirroring names, and show secure-screen guidance only on detected sessions.
- 전화·문자 중복 새로고침을 제거하고 전환 탭을 중앙 정렬했으며 메시지 미리보기
  높이를 고정하고 앱 검색을 별도 지정 창으로 옮겼습니다. DEX·미러링 명칭을
  통일하고 보호 화면 안내는 감지된 영상 세션에만 표시합니다.
- Attach the session control bar to the top or bottom inside each video window,
  keep it centered while the window moves, and add compact collapse/expand states.
- 세션 제어 바를 영상 창 내부 상단·하단에 선택 배치하고 창 이동 시 중앙 정렬을
  유지하며 작은 손잡이로 접고 펼칠 수 있도록 개선했습니다.
- Add app quick-launch targets for desktop virtual windows or the mirrored phone,
  restore per-device desktop/mirror window geometry, replace floating controls with
  a titleless horizontal volume slider bar, simplify Home, and expand call details.
- 앱 바로 실행 위치를 데스크톱 독립 창·휴대폰 화면에서 선택하고, 기기별 데스크톱·
  미러링 창 위치와 크기를 복원하며, 플로팅 제어를 제목 없는 가로형 볼륨 슬라이더로
  개편하고 홈을 정리했으며 통화 상세 정보 화면을 추가했습니다.
- Add selectable Dock and menu-bar presence modes plus an independent launch-window
  preference, including a true menu-bar-only background mode for login startup.
- Dock·메뉴 막대 표시 위치와 시작 시 메인 창 열기를 각각 선택할 수 있게 하고,
  로그인 자동 실행에 적합한 메뉴 막대 전용 백그라운드 모드를 추가했습니다.
- Keep the device-maximum quality preset visibly selected and show the detected
  native resolution until another preset or manual value is chosen.
- 기기 최대 화질 프리셋에 조회된 실제 해상도를 표시하고 다른 값으로 변경할 때까지
  선택 상태가 유지되도록 개선했습니다.
- Redesign Home around a prominent live connection summary with secondary device
  setup collapsed, and replace the phone page with a searchable desktop three-pane
  calls, contacts, message threads, conversation, dial-pad, and composer layout.
- 홈을 실시간 연결 상태 중심으로 재구성하고 추가 기기 설정을 접었으며, 전화·문자를
  검색 가능한 3열 통화·주소록·대화 목록·대화창·다이얼패드·작성 화면으로 개편했습니다.
- Add 720p, 1080p, and device-native resolution presets plus 30/60/120 FPS
  controls while retaining manual expert settings, and clarify overlay lifetime.
- 720p·1080p·기기 실제 최대 해상도와 30·60·120 FPS 선택을 추가하고 전문가용
  수동 설정은 유지했으며 데스크톱 오버레이의 유지 범위를 명확히 안내합니다.
- Add editable and removable device aliases keyed by stable physical identity so
  names survive USB, Wi-Fi, mDNS, and IP address changes.
- USB·Wi-Fi·mDNS 및 IP 변경 후에도 유지되는 물리 기기 기준 별칭의 등록·수정·삭제를
  추가했습니다.
- Accept Finder file and folder drops across the entire app and route file-only
  Command-V pastes to the transfer page without intercepting active text editing.
- 앱 전체에서 Finder 파일·폴더 드롭을 받고, 텍스트 입력 중에는 가로채지 않으면서
  파일이 복사된 Command-V만 파일 전송 화면으로 보내도록 개선했습니다.
- Add verified screen-launch states, single active main display, lock guidance,
  volume controls, reversible phone dimming, balanced connection cards, searchable
  app quick launch with Command-1/2/3 favorites, and live contacts/calls/SMS browsing.
- 실제 영상 창을 확인하는 실행 상태, 주 화면 단일 실행, 잠금 안내, 볼륨 제어,
  종료 시 복원되는 밝기 최소화, 균형 잡힌 연결 카드, 검색 가능한 앱 바로 실행과
  Command-1/2/3 즐겨찾기, 실시간 주소록·통화기록·SMS 탐색을 추가했습니다.
- Add Finder-style Command-C/Command-V file exchange, Mac-to-Galaxy drop targets,
  Galaxy Download browsing, phone-to-Mac file promises for drag-out, and explicit
  save actions for files and folders.
- Add a persisted option to turn off the physical phone screen when mirroring
  starts, protected-screen guidance without bypassing Android FLAG_SECURE, an
  in-app license/about page, and GitHub Releases update checks.
- Replace the rounded-background icon source with the project owner's square
  Frame 70 vector so macOS applies its own icon mask without white clipped corners.
- Add one-click USB-to-wireless setup, mDNS pairing and connect endpoint discovery,
  code-only Android 11+ pairing, automatic endpoint saving, and a manual fallback
  reserved for isolated networks and Tailscale addresses.
- Make macOS Command the explicit scrcpy shortcut modifier for reliable two-way
  text copy and paste, add clipboard guidance to Home, and rename user-facing
  DeX/DX Companion wording to Flow Bridge Desktop Mode and Display Recovery Tool.
- Replace the application icon with the project owner's final Flow Bridge vector
  artwork and regenerate the complete macOS icon set from the canonical SVG.
- Rename the macOS app to Flow Bridge, add Korean and English localization,
  migrate existing settings, and add complete attribution, license, trademark,
  privacy, build, and distribution documentation for the public repository.
- Redesign the macOS interface around a persistent sidebar with focused Home,
  app-window, transfer, notification, settings, and diagnostics pages; add clear
  launch cards, connection state, contextual descriptions, and a fixed status bar.
- Add a Samsung Flow symbol-based macOS icon, a phone/SMS handoff page, and a
  launcher-app picker that presents friendly names instead of requiring package IDs.
- Add a native SwiftUI macOS MVP for ADB device discovery, wireless connection,
  deterministic DeX overlay creation and cleanup, scrcpy launch, app-specific
  virtual displays, persistent display settings, tests, and ad-hoc app packaging.
- Bundle checksum-verified official scrcpy 4.1 static releases and ADB for both
  Apple silicon and Intel so the packaged app has no Homebrew dependency.
- Add three independent app-window slots, installed-package browsing, file and
  folder transfer, bundled license notices, and universal macOS ZIP packaging.
- Add Android 11+ wireless pairing, automatic device refresh, device diagnostics,
  power/wake/sleep controls, interactive region capture, and menu-bar controls.
- Add per-device display settings, reusable per-app profiles, cancellable queued
  transfer status, session-log export, and checksum-verified DX Companion
  installation, permission grant, and removal for the selected device.
- Add process-scoped scrcpy keyboard correction, Enter/Shift+Enter switching,
  floating mini control bars that follow scrcpy windows, window-only capture,
  configurable menu-bar auto-hide, and login-item registration.
- Pull the installed Companion APK back from the selected phone and compare its
  complete SHA-256 before and after protected permission grants.
- Add remembered wireless reconnection, ordinary phone-screen mirroring, duplicate
  USB/LAN/mDNS transport merging, and separately configurable Galaxy call, SMS,
  and application notifications in macOS Notification Center.
- ADB 기기 검색, 무선 연결, DeX overlay의 결정적 생성·정리, scrcpy 실행,
  앱 단일 가상화면, 화면 설정 저장, 테스트와 임시 서명 앱 패키징을 지원하는
  SwiftUI 기반 macOS MVP를 추가했습니다.
- 체크섬을 검증한 공식 scrcpy 4.1 정적 빌드와 ADB를 Apple Silicon·Intel용으로
  모두 포함해 패키지 앱의 Homebrew 의존성을 제거했습니다.
- 독립 앱 단일창 슬롯 3개, 설치 앱 패키지 목록, 파일·폴더 전송, 번들 라이선스
  고지와 macOS Universal ZIP 패키징을 추가했습니다.
- Android 11 이상 무선 페어링, 기기 자동 갱신, 기기 진단, 전원·화면 켜기·끄기,
  대화형 영역 캡처와 메뉴 막대 제어를 추가했습니다.
- 기기별 화면 설정, 재사용 앱 프로필, 취소 가능한 전송 대기열·상태, 세션 로그
  저장과 선택 기기의 DX Companion 해시 검증 설치·권한 부여·삭제를 추가했습니다.
- scrcpy 프로세스 한정 키보드 보정, Enter/Shift+Enter 전환, scrcpy 창을 따라가는
  미니 컨트롤바, 해당 창만 캡처, 메뉴 막대 자동 숨김과 로그인 자동 실행을 추가했습니다.
- 보호 권한 부여 전후에 선택 휴대폰의 설치 APK를 다시 가져와 전체 SHA-256이
  공식 APK와 일치하는지 검증하도록 보강했습니다.
- 무선 연결 주소 기억·자동 재연결, DeX 없는 일반 휴대폰 화면 미러링, 동일 기기의
  USB·LAN·mDNS 전송 병합과 전화·문자·애플리케이션별 macOS 알림 전달을 추가했습니다.
- macOS 화면을 홈·앱 창·파일 전송·알림·설정·진단 사이드바 구조로 개편하고,
  실행 카드, 연결 상태, 화면별 안내와 고정 상태 표시줄을 추가했습니다.
- Samsung Flow 심볼 기반 macOS 아이콘, 전화·문자 전달 화면과 패키지명을 직접
  입력할 필요가 없는 일반 앱 이름 선택기를 추가했습니다.
- macOS 앱 이름을 Flow Bridge로 변경하고 한국어·영어 현지화, 기존 설정 마이그레이션,
  공개 저장소용 출처·라이선스·상표·개인정보·빌드·배포 문서를 추가했습니다.
- 프로젝트 소유자가 제공한 Flow Bridge 최종 벡터 아이콘으로 교체하고 원본 SVG에서
  전체 macOS 아이콘 세트를 다시 생성했습니다.
- 양방향 텍스트 복사·붙여넣기의 scrcpy 단축키를 macOS Command로 명시 고정하고 홈에
  사용법을 추가했으며, 사용자 화면의 DeX·DX Companion 표현을 Flow Bridge 데스크톱
  모드와 화면 복구 도구로 변경했습니다.
- USB 연결을 한 번에 무선으로 전환하고 mDNS로 페어링·연결 주소를 자동 검색해
  6자리 코드만으로 연결하며 주소 저장·자동 재연결까지 처리하도록 개선했습니다.
  격리 네트워크와 Tailscale 주소용 수동 입력은 고급 항목으로 이동했습니다.
- Finder 방식 `⌘C/⌘V` 파일 송수신, Mac→Galaxy 드롭 영역, Galaxy Download 탐색,
  Galaxy→Mac 드래그 파일 프라미스와 파일·폴더 저장 기능을 추가했습니다.
- 미러링 시작 시 실제 휴대폰 화면을 끄는 저장 옵션, Android 보안 화면을 우회하지
  않는 안내, 앱 내 라이선스·정보 화면과 GitHub Releases 업데이트 확인을 추가했습니다.
- macOS 자체 아이콘 마스크가 적용되도록 둥근 배경을 제거한 최종 Frame 70 벡터로
  교체해 아이콘 모서리의 흰색 잘림을 수정했습니다.

## 2026-08 - v2.0.0

- 구조 감사에서 더 이상 구독되지 않던 v1 단일 기기 연결·분리 이벤트 처리기를
  제거해 다중 기기 레지스트리와 중복되던 정리·자동 시작 경로를 없앰
- `MainForm`의 기기 탭 UI, 연결 수명주기, 연결 상태 보조 로직을 별도 partial로
  분리하고 설정 루트와 설정 DTO·열거형도 별도 파일로 분리
- 한 파일에 모여 있던 선택·텍스트·숫자·단축키·드롭다운 사용자 컨트롤을
  기본 입력, 값 입력, 드롭다운 팝업 파일로 분리
- 물리 휴대폰 identity와 USB·무선 ADB transport를 분리한 모델 추가
- 동일 휴대폰의 여러 transport 병합, 서로 다른 휴대폰 분리 및 transport 선택 규칙 추가
- 상태 스냅샷·변경 이벤트와 알려진 serial의 identity·표시 이름 보존 레지스트리 추가
- `AdbService.TargetSerial`과 프로세스 전역 `ANDROID_SERIAL`을 제거하고 모든
  기기별 ADB·Scrcpy 명령을 명시적 transport serial 방식으로 전환
- DeX·단일창 시작과 종료 정리, 화면 전원, 캡처, Companion, 앱 목록 등에서
  작업 시작 시 캡처한 serial 또는 세션 serial만 사용
- 파일 전송 취소와 Companion detach가 요청한 기기에서만 수행되도록 범위 고정
- 물리 기기별 DeX·단일창·Companion·파일 전송·화면 전원 상태를 보존하는
  런타임 세션 레지스트리 추가
- Scrcpy·DeX·단일창·화면 OFF·양방향 전송 서비스를 물리 기기별 독립 묶음으로
  생성하고 한 런타임에 1:1로 결속
- 같은 휴대폰의 USB·무선 전환은 런타임과 서비스 결속을 유지하고, 다른 휴대폰의
  상태·전송 큐·정리 작업이 섞이지 않도록 분리
- 연결 해제 뒤 정리 증거를 보존하며 의미 없는 감시 주기 갱신은 상태 이벤트에서 제외
- 기존 v1 런타임에 연결하기 전 핵심 규칙을 고정하는 독립 회귀 테스트 추가
- 메인 화면에 물리 기기 선택 UI를 추가하고 기기별 독립 DeX·단일창·Companion·양방향
  파일 전송 서비스를 선택하면서 비선택 기기의 실행 세션은 유지
- 실제 휴대폰 두 대에서 DeX와 단일창 동시 실행, 탭별 상태 복원, PC→휴대폰 전송,
  각 기기의 개별 연결 해제 시 다른 기기 세션 유지 확인
- 런타임 등록과 탭 선택에는 기기 이름·serial·연결 방식을 기록하고 기기별
  Scrcpy·화면 전원·파일 전송 로그에는 serial 접두사를 추가
- Scrcpy 서버 전송 성공 stderr를 INFO로 분류하고 정상 설정 상태의 반복 경고를
  최초 1회 INFO로 줄여 다중 기기 로그의 대상을 명확하게 구분
- 물리 기기 identity별로 DeX·단일창 3개·앱 프로필·마지막 성공 설정을 분리해
  한 기기 탭의 해상도·DPI·실행 옵션이 다른 기기로 따라가지 않도록 변경
- 느린 기기의 DX Companion 설치를 최대 20초 동안 재확인하고, 실제 공식 앱이
  설치됐으면 ADB 설치 명령의 시간 초과만으로 실패 처리하지 않도록 보강
- 실제 휴대폰 두 대에서 양방향 파일 전송, Companion 진단, 연결 해제 격리와
  전체 정상 종료 후 overlay·ADB reverse·소유 프로세스 정리를 확인
- 무선 ADB 설정을 물리 기기 identity별 프로필로 분리하고 설정의 연결 페이지에
  대상 휴대폰 선택기를 추가
- USB로 무선 준비할 때 선택한 휴대폰의 USB serial만 사용하고, 해당 휴대폰에서
  감지한 IP 또는 그 휴대폰에 저장된 주소로만 연결하도록 변경
- 무선 주소 연결 뒤 실제 기기 identity를 다시 확인해 다른 휴대폰의 IP면 저장과
  전환을 거부
- 휴대폰마다 USB·무선 모드, IP, 포트와 자동 재연결을 독립 저장하고 여러 무선
  프로필의 자동 재연결을 함께 처리
- 한 휴대폰은 무선, 다른 휴대폰은 USB인 구성과 두 휴대폰 모두 무선인 구성을
  지원하도록 실행 transport 선택을 기기별 저장값과 연결
- 기기별 USB·무선 선택을 강제 정책으로 변경해 반대 transport로 자동 전환하지
  않고, 설정 라디오와 실제 감지 연결 상태를 분리해 표시
- 선택한 transport가 사라지면 해당 기기의 Scrcpy 세션·전송·ADB reverse를
  정리하고, 다시 나타날 때까지 USB 또는 무선 대기 상태를 표시
- 연결 시 DeX 자동 시작을 현재 선택 탭에 한정하지 않고, 시작 전에 연결돼 있던
  기기와 실행 중 새로 연결된 기기 모두 각자의 런타임과 저장 설정으로 독립 실행
- 기기 탭은 현재 연결 기기가 한 대뿐이면 숨기고, 실행 중 두 번째 기기가
  확인되면 표시한 뒤 한 기기가 분리돼도 해당 실행 동안 유지
- 기기 선택 UI를 왼쪽 사이드바의 별도 영역으로 이동하고, 프로그램 시작 시 이미
  연결된 기기는 최신 Galaxy 모델을 위에 배치하며 실행 중 순차 연결은 최초 연결 순서를 유지
- 연결이 끊긴 기기 항목도 마지막으로 확인한 휴대폰 이름을 유지해 serial 번호로
  바뀌지 않도록 표시 이름을 기기 컨텍스트에 보존
- Windows 세션 종료에서는 `WM_QUERYENDSESSION` 단계부터 새 ADB·보조 프로세스
  실행을 즉시 차단하고, 이미 연결된 DX Companion 세션에만 overlay·절전모드
  복원 요청을 전송
- Companion이 없거나 서명·권한 검증을 통과하지 못한 기기는 Windows 종료 중
  새 `adb.exe`를 실행하지 않고 기기 정리를 건너뛰어 네이티브 ADB 오류창을 방지
- DX Companion 2.0.0에 인증된 loopback 감시 세션과 물리 연결 손실 유예 정리를
  추가; guardian 소켓만 끊어지면 유지하고 설정된 USB/Wi-Fi까지 사라진 경우에만
  기본 5분 뒤 정리, 즉시·1분·5분·10분·30분 또는 자동 정리 안 함 선택 가능
- 사용자가 DX Manager를 정상 종료할 때는 기기 설정 복원과 overlay 정리를 먼저
  완료하고 `adb kill-server` 뒤 새 프로세스 실행을 차단하도록 종료 경로 분리
- 정상 종료와 Windows 종료의 메시지 루프가 끝난 뒤 선택 ADB·번들 scrcpy와 ADB·
  전송 프록시의 절대 경로만 최종 검사해, 분리된 ADB 서버를 포함한 잔존 프로세스가
  설치 폴더 삭제를 막지 않도록 정리
- 이름이 같은 다른 경로의 프로세스는 종료하지 않는 경로 격리 회귀 테스트를 추가하고
  .NET Framework 4.6.2 x64 Release 빌드와 총 36개 다중 기기 테스트 통과
- 메인 기기 탭을 바꿔도 설정 창을 유지한 채 대상 정보를 새로 고침
- 휴대폰→PC 수신 파일을 공통 경로 아래 휴대폰 표시 이름별 하위 폴더로 분리
- DeX·단일창 scrcpy 창 제목에 휴대폰 표시 이름을 추가해 동시 실행 창을 구분
- .NET Framework 4.6.2 x64 Release 빌드와 35개 다중 기기 회귀 테스트 통과
- DX Companion 2.0.0(versionCode 6)의 단위 테스트 7개·Release lint·v2 서명·번들 해시와
  Windows x64 Release 빌드·다중 기기 회귀 테스트 39개를 최종 재검증
- 여러 설정 저장 요청이 같은 `settings.json.tmp`를 먼저 이동할 수 있던 경합을
  제거하고, 저장별 고유 임시 파일·프로세스 간 파일 잠금·동시 저장 회귀 테스트 추가
- 진단 페이지에 현재 선택된 기기의 Android·SDK·One UI·보안 패치·연결 방식과
  참고용 호환성 판정을 표시하고 수동 새로 고침 제공
- 환경, 선택 기기 런타임, 연결 기기 요약과 최근 경고·오류에서 기기 이름·serial·
  IP·토큰·로컬 경로를 가린 진단 보고서 저장 추가
- 진단 보고서의 PC→휴대폰 상태를 실제 의미에 맞게 활성 전송 세션 수와 대기
  항목 수로 구분하고, ADB 버전 출력에서는 설치 경로를 제외
- 일반 Debug·Release 빌드도 최신 서명 Companion APK를 출력 폴더에 동기화하고,
  원본 APK가 없으면 오래된 출력 APK를 제거해 잘못된 번들 해시 경고를 방지
- RC 검증용 Windows 종료 모의 테스트를 최종 사용자 UI에서 제거
- 공개 후보 ZIP에서 PDB·로그·스크린샷·사용자 설정·서명키를 제외하고 필수
  Scrcpy·ADB·ADB 프록시·DX Companion·라이선스 파일이 모두 포함됐는지 검사
- .NET 4.6.2 targeting pack이 설치되지 않은 개발 PC에서도 검증된 참조
  어셈블리 경로를 명시해 동일한 Release 패키지를 만들 수 있도록 스크립트 보강

## 2026-08 - v1.3.0

- 전용 `SC1F2`뿐 아니라 `VK_HANGUL + extended scan 0x38`로 보고되는 일부
  한국어 노트북 한영키를 지원하고, 브라질·유럽 키보드의 AltGr는 보정 대상에서
  제외
- DX Companion에서 갤러리·내 파일 공유 또는 앱의 폴더 선택을 통해 휴대폰
  파일·폴더를 현재 ADB 연결로 PC에 전송
- 휴대폰별 ADB reverse와 세션 토큰으로 수신 대상을 고정하고, 같은 이름은
  `이름 (1)` 형식으로 안전하게 저장
- DX Companion UI, 좌우 스와이프 탭 전환, 2 × 1 위젯과 안내 문구 개선
- 서명된 DX Companion 1.3.0 APK를 포터블 ZIP의 `tools\companion`에 포함
  - 자동 설치하지 않고 진단 페이지에서 사용자가 현재 선택된 기기에만
    설치·업데이트·재설치 또는 삭제
  - 설치 전 번들 APK SHA-256과 v2 서명 인증서, 설치 후 package·버전·서명과
    권한을 다시 검증
  - 삭제 전 해당 기기의 파일 수신 세션과 ADB reverse를 먼저 정리

## 2026-07 - v1.2.0

- 다음 기능 확장에 대비해 `MainForm`을 장치·세션, 화면 전원, 실행 설정,
  앱 목록, 모드 전환, 레이아웃, 종료 처리 등의 feature partial로 분리
- `SettingsForm`을 페이지 구성, 설정값 처리, 연결, 휴대폰 폴더 탐색,
  DX Companion, 테마와 공통 UI 처리로 분리
- 파일 전송 코디네이터를 IPC, 큐 처리, ADB 실행, 원격 파일 조작,
  진행 상태와 내부 세션 상태로 분리
- 폴더 탐색, 경로 검증, 전송 항목 정렬과 크기 계산을 부작용 없는
  `FileTransferPlanner`로 분리하고 실행 결과 모델을 별도 파일로 이동
- 각 DeX·단일창 scrcpy 창을 따라다니는 선택형 미니 컨트롤바 추가
  - 휴대폰 화면 끄기/켜기, 전원 버튼, 전체 화면, 1:1 창 크기, 캡처,
    DX Manager 열기를 마우스로 실행
  - 각 버튼의 기능과 scrcpy 단축키를 툴팁으로 표시하고 접기/펴기 지원
  - 연결된 scrcpy 창의 활성화·최소화·앞뒤 순서를 따라 다른 세션 위로
    잘못 떠오르지 않도록 HWND/PID 단위로 관리
- 단일창 앱별 프로필 추가
  - 선택 앱의 해상도, DPI, 비트레이트, FPS, 실행 옵션과 추가 인자를 저장
  - 어느 단일창 슬롯에서 앱을 선택해도 같은 앱 프로필을 자동 적용
  - 현재 설정으로 덮어쓰기와 프로필 삭제 지원
- 휴대폰 저장 폴더 탐색창에서 목록 위 마우스 휠이 뒤의 설정창이 아닌
  폴더 목록을 스크롤하도록 수정
- 사용자 지정 해상도 입력을 Android overlay 한계에 맞는 네 자리 값으로
  제한하고 한국어·영어 화면의 정렬을 보완
- 선택형 Android 복구 도구 **DX Companion** 기능 확장
  - 남은 가상화면 제거와 개발자 옵션의 **절전모드 해제** 끄기를 각각 지원
  - 빠른 설정 타일과 2 × 1 위젯은 기본적으로 두 항목을 함께 정리하며,
    앱 설정에서 실행할 항목을 선택 가능
  - 실제 Galaxy 기기에서 앱 본체, 타일, 위젯과 DX Manager의 서명 검증
    권한 부여 흐름을 확인
- 이 버전의 DX Companion APK는 DX Manager 공개 ZIP에 포함하지 않고 별도로 제공

## 2026-07 - v1.1.0

- 번들 scrcpy를 4.1로 갱신하고 SDL 3.4.12, FFmpeg 62.28.102/62.12.102/
  60.26.102, libusb 1.0.30 구성과 제3자 고지를 동기화
- DeX·단일창 scrcpy 창에 놓은 파일과 폴더 전체를 설정한 휴대폰 폴더로 보내는
  DX Manager 관리형 파일 전송 추가(기본 `/sdcard/Download/`)
- Windows 7과 Windows 11에서 한글을 포함한 Unicode 파일명을 보존하도록
  ASCII 임시 이름 push 후 휴대폰에서 최종 이름을 복원
- 독립적으로 이동할 수 있는 전송 상태창에 현재 항목과 다음 4개, 파일 크기,
  경과 시간, 완료·실패·대기 수와 취소 기능을 제공하고 오해를 줄 수 있는
  퍼센트와 남은 시간은 제거
- 폴더 구조와 빈 폴더를 staging에 완성한 뒤 한 번에 공개하고 재분석 지점은
  건너뛰며, 같은 파일·폴더 이름은 `(1)`, `(2)` 접미사로 충돌 회피
- 최종 이름 반영 중 취소·ADB 응답 중단과 큐 등록 경합을 방어하고 기기 측
  P/C 커밋 표식으로 이동 완료 여부를 재확인하며, 다음 유휴 세션에서 중단된
  대용량 전송의 `.part` 잔여물을 정리
- 모든 non-terminal 요청 registry, 비동기 ADB script 입력과 세션 시작 시 대상
  폴더 snapshot으로 dequeue 취소·stdin 정지·설정 저장 경합을 방어
- 관리형 전송은 기본값으로 켜고 설정에서 끄면 새로 여는 DeX·단일창부터
  scrcpy 순정 파일 드롭 동작을 사용하도록 구성
- scrcpy 전용 ADB proxy는 세션 대상 폴더의 파일·폴더 push만 관리 큐로
  전달하고 그 밖의 shell, 서버 push, 단독 APK 설치와 DX Manager 내부 ADB
  명령은 실제 ADB로 유지
- 설정, 환경 점검과 로그의 ADB 버전을 공통 `1.0.41` 문구 대신 실제
  `Version ...` platform-tools 빌드 값으로 표시
- Android 16 / One UI 8.x를 현재 확인 기준으로 명시하고 One UI 7.x 이하의
  검은 DeX 창 가능성을 사용자 문서에 안내
- overlay 정리는 문자열 `none` 저장 대신 `settings delete global
  overlay_display_devices`로 설정 항목 자체를 삭제하도록 변경
- 파일 전송 중 기기 연결이 해제되면 중단 대상 수와 기기를 명시적으로 기록
- 진단 페이지에서 별도 DX Companion의 package와 공식 v2 서명 인증서를
  확인한 뒤 `WRITE_SECURE_SETTINGS` 권한을 부여하고 결과를 재검증
- DX Companion Android 앱에 가상화면 상태 조회·삭제 후 재검증, 빠른
  설정 타일, 홈 화면 위젯과 한국어/영어 UI 제공
- 캡처와 드롭 파일의 휴대폰 저장 폴더에 Unicode ADB 폴더 찾아보기 추가
- 보안·DRM 앱 제한, 다중 디스플레이를 지원하지 않는 앱의 휴대폰 화면 실행과
  HID 마우스 캡처 해제 방법을 한영 FAQ에 추가

## 2026-07 - v1 배포 안정화

- 한 번 실행할 때 처음 선택한 휴대폰 한 대만 관리하도록 고정하고, 같은
  휴대폰의 USB/무선 ADB 전환만 허용
- 고정 휴대폰 분리 후 감시 주기마다 ADB target serial이 해제·재등록되던
  상태 반복 제거
- 연결 감지와 기기 이름 확인 후 실제 Scrcpy 시작 전에 적용되는 0~60초
  시작 대기 옵션 추가(기본 1초)
- DeX overlay의 실제 너비, 높이, DPI가 설정과 모두 같을 때만 재사용하고,
  하나라도 다르면 `none`으로 제거 후 재생성
- 정상 종료 시 overlay 생성 주체와 관계없이 대상 기기의 overlay를 정리
- 연결 해제와 재연결 중 DeX/단일창, 화면 OFF, stay-awake 정리 강화
- SC1F2 KeyUp이 없는 키보드에서도 한영 보정이 반복되도록 상태 해제 보강
- DPI 최솟값 120 강제와 입력 확정 시점 안내, 숫자 화살표 커서 개선
- DPI 최솟값과 사용자 지정 해상도 4096 상한 위반 시 편집 전 값 복원
- 전체 설정 초기화 시 현재 선택한 DeX/단일창 프로필만 이전 실행 옵션으로
  되돌아가던 UI 덮어쓰기 수정
- 자동 실행·자동 숨김·트레이 시작과 선택 키 보정을 끈 상태를 v1 기본값으로
  정리하고 DeX/단일창 공통 기본 화질 값을 통일
- 개발용 `bin\Release`와 분리된 `dist\DX Manager` 포터블 폴더 및 버전별
  x64 ZIP 패키징 자동화 추가
- 패키징 전에 실행 중인 앱과 번들 ADB 서버를 확인하고 Debug/Release의
  로그·스크린샷 테스트 파일을 자동 정리
- 무선/USB 전환과 Scrcpy 종료가 자동 숨김 감시와 겹칠 때의 프로세스 경합 방어
- 설정 항목을 기본/고급으로 재정리하고 환경 점검 표 간격과 스크롤 수정
- MIT 라이선스, 제작자/GitHub 링크, 파일 속성과 번들 구성요소별 제3자
  라이선스 원문/고지를 배포 형태로 정리
- README, 사용 설명서와 FAQ에 한국어/영어 스크린샷 배치
- 한국어/영어 자주 묻는 질문을 독립 문서로 분리하고 README와 설명서에서 연결
- GitHub 저장소 public 전환과 `DX Manager v1.0.0` 첫 공개 Release 게시
- 배포 ZIP에는 영어 다음 한국어 순서의 별도 HTML 없는 포터블 README 사용

## 2026-07 - 입력 및 사용성 안정화

- DX Manager 자체 소스 라이선스를 MIT로 확정
- Windows 10 이상에서는 선택한 Scrcpy 폴더의 ADB를 사용하고, 실행할 수
  없을 때 레거시 ADB로 대체하도록 선택 구조를 단순화
- 중복으로 동봉하던 별도 modern ADB 제거
- 출력 리디렉션 없이 실행하는 Scrcpy Wake-up의 인코딩 설정 오류 수정
- 연결이 끊긴 기기의 overlay 정리는 오류로 처리하지 않고 재연결까지 보류
- Scrcpy 4.0/SDL3의 오른쪽 Shift 입력 회귀를 왼쪽 Shift 치환으로 우회
- Scrcpy 버전을 감지해 3.3.4에는 `-w`, 4.0에는 `--keep-active` 적용
- 미지원 버전에서는 단일창 `--flex-display`를 자동 제외
- 경로와 일반 텍스트 필드에 네이티브 편집 엔진을 적용해 드래그 선택,
  가로 스크롤과 `Ctrl+Z` 복원
- 구형 ADB/Scrcpy의 한국어 경로 출력 인코딩 보정
- Windows 7 설정창의 마우스 휠 스크롤 보정
- 성공적으로 자동 실행한 앱을 최근 20개까지 공통 저장
- 제3자 라이선스 고지를 `licenses` 폴더로 이동

## 2026-07 - 라이트/다크 카드 UI

- `dx_manager_light_dark_design.html` 기반 메인 화면 재설계
- 보라색 강조색, 알약형 사이드바, 카드형 화면 설정/실행 옵션
- 체크박스를 토글 스위치로 변경
- 화면 설정 필드를 기본 WinForms 입력기 없는 완전 커스텀 컨트롤로 교체
- 커스텀 드롭다운, 숫자 입력, 선택·커서·키보드 동작 구현
- 원형 상태 링과 라이트/다크 색상 팔레트
- Windows 테마 자동 감지 및 라이트/다크 수동 선택
- 스크롤 없는 고정 레이아웃과 사이드바/실행 옵션 하단선 정렬
- Scrcpy 축약 옵션 표기 `-w`, `-x`

## 2026-07 - 무선 ADB

- USB TCP/IP 준비와 Wi-Fi IP 자동 감지
- 무선 연결/해제, 자동 재연결
- Android 11 페어링
- 모든 ADB/Scrcpy에 target serial 적용

## 2026-07 - DX Manager와 다국어 UI

- 제품명과 실행 파일을 DX Manager로 변경
- Windows 언어 자동 감지, 한국어/영어 수동 선택
- `.resx` 기반 UI 리소스
- 메인 실행 버튼 단순화와 변경사항 적용 링크
- 사이드바 설정 및 설정창 진단 탭
- 키보드 포커스 표시와 입력값 전체 선택
- 캡처 저장 토스트와 설정 저장 인라인 알림

## 2026-06 - Scrcpy 4.0

- 번들 Scrcpy 4.0
- 단일창 동적 크기 변경
- Windows 7 SP1~11 기본 실행 확인

## 2026-06 - 단일창

- `--new-display` 기반 슬롯 3개
- 슬롯별 화면, 앱, 제목과 옵션 저장
- DeX/단일창 동시 실행과 다중 화면 전원 안정화

## 2026-06 - DeX 안정화와 UI

- 생성 전후 비교 display ID 선택
- 사용자 지정 해상도와 앱 이름 저장
- scan code 한영/Enter 보정
- F8 캡처, 트레이 자동 숨김, 세션 로그
- .NET Framework 4.6.2와 Windows 7 지원
