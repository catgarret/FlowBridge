# Flow Bridge 0.1.0

## English

Flow Bridge is a native macOS bridge for Samsung Galaxy devices. This first
public release bundles universal Apple silicon and Intel builds of ADB and
scrcpy, with no Homebrew dependency.

### Highlights

- DEX mode and phone mirroring with remembered window geometry
- Responsive controls with Back, Home, Recent Apps, volume, power, screenshot,
  and a compact More menu
- USB, wireless debugging, mDNS, LAN, and reachable Tailscale connections
- Galaxy contacts, recent calls, SMS conversations, direct SMS send, and cached
  contact photos
- Galaxy notification browsing and optional macOS Notification Center delivery
- App search, cached icons, three keyboard quick-launch slots, and DEX/mirror targets
- Queued outbound transfers plus folder browsing, thumbnails, multi-selection,
  and batch downloads from Galaxy
- Korean and English interface

### Important notes

- This build is ad-hoc signed and not notarized. macOS may require **Open** from
  the Finder context menu on first launch.
- Allow Flow Bridge under **System Settings → Notifications** for Mac banners.
- Call audio remains on the Galaxy or its connected Bluetooth device.
- Android-protected screens remain black and must be completed on the phone.

## 한국어

Flow Bridge는 Samsung Galaxy 기기를 Mac에서 연결·미러링·관리하는 네이티브
macOS 앱입니다. 첫 공개 버전에는 Apple Silicon과 Intel용 ADB·scrcpy가 모두
포함되어 Homebrew를 별도로 설치할 필요가 없습니다.

### 주요 기능

- 창 위치와 크기를 기억하는 DEX 모드 및 휴대폰 미러링
- 뒤로·홈·최근 앱·볼륨·전원·스크린샷과 좁은 창용 더보기를 갖춘 화면 제어 바
- USB·무선 디버깅·mDNS·LAN 및 접근 가능한 Tailscale 연결
- Galaxy 주소록·최근 통화·문자 대화·직접 SMS 전송 및 연락처 사진 캐시
- Galaxy 알림 확인과 선택 가능한 macOS 알림 센터 전달
- 앱 검색·아이콘 캐시·단축키 3개·DEX/미러링 실행 위치 지정
- 보내기 대기열과 Galaxy 폴더 탐색·썸네일·복수 선택·일괄 다운로드
- 한국어·영어 인터페이스

### 확인 사항

- 현재 빌드는 임시 서명이며 Apple 공증을 받지 않았습니다. 최초 실행 시 Finder의
  우클릭 **열기**가 필요할 수 있습니다.
- Mac 배너를 받으려면 **시스템 설정 → 알림**에서 Flow Bridge를 허용해야 합니다.
- 통화 음성은 Galaxy 또는 Galaxy에 연결된 Bluetooth 기기에서 처리됩니다.
- Android 보호 화면은 검게 표시되므로 휴대폰에서 직접 진행해야 합니다.
