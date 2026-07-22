# Session Handoff

마지막 갱신: 2026-07-22

## Git

- 저장소: `E:\vs\dex system`
- 브랜치: `fix/audit-hardening-20260711`
- 마지막 공개 커밋: 이 문서를 포함한 최신 `main` (`git log -1`로 확인)
- 현재 작업: v1.1.0 Scrcpy 4.1 및 관리형 Unicode 파일 전송 구현·회귀 확인

v1.1.0 작업 전 문서 커밋은 `f9d96fa`, 복구 태그는
`pre-v1.1.0-20260721`이다. 공개 배포와 push는 사용자 실기 확인 뒤 진행한다.

새 세션에서는 실제 `git status --short --branch`와 `git log`를 다시 확인한다.
현재 작업이 커밋되지 않았다면 사용자 변경과 함께 그대로 보존한다.

## 현재 구현 상태

- Windows 11 USB/무선 DeX와 단일창 3개 동시 실행 확인
- 64비트 Windows 7 SP1/.NET Framework 4.6.2 유선 핵심 흐름 확인
- Scrcpy 4.1과 Scrcpy 폴더의 ADB를 기본 사용, 필요 시 legacy ADB fallback
- 연결 상태와 기기 이름 확인 후 실제 시작 명령 직전에 0~60초 대기
- 연결 해제/재연결 시 세션, 화면 OFF, stay-awake 정리
- DeX overlay 너비/높이/DPI 일치 시 재사용, 불일치 시 제거 후 재생성
- 정상 종료 시 관리 기기의 overlay를 생성 주체와 관계없이 제거
- SC1F2 KeyUp이 없는 환경에서도 한영 보정 반복 동작
- Scrcpy 4.0/SDL3에서 재현한 근거에 따라 SDL3 기반 4.x 오른쪽 Shift를
  왼쪽 Shift로 치환
- DPI 120 미만 입력 거부와 입력 확정 시 안내
- 무선/USB 전환 중 Scrcpy 종료와 자동 숨김 타이머 경합 방어
- 기본/고급 설정 재정리와 환경 점검 표 레이아웃 수정
- 제작자/GitHub 링크, MIT 라이선스, 제3자 고지와 파일 속성 완료
- README/설명서/FAQ용 한국어 10장·영어 8장 스크린샷 배치 완료
- 한국어/영어 FAQ 19문항을 독립 문서로 작성하고 README와 설명서에서 연결
- 사용자 지정 해상도 가로·세로 4096 상한과 DPI 120 하한 위반 시 이전 값 복원
- 초기화 직후 현재 선택한 모드의 실행 옵션이 다시 덮어써지지 않도록 UI 재동기화
- 공개 패키지를 `dist\DX Manager`와 버전별 x64 ZIP으로 만드는 스크립트 추가
- 자동 실행, 트레이 시작, 자동 숨김과 선택 키 보정을 끈 v1 기본값으로 정리
- 저장소 샘플 설정을 `AppSettings.CreateDefault()`와 정확히 일치시켜 개인 설정 제거
- scrcpy, ADB, SDL3, FFmpeg, libusb, dav1d, zlib과 MinGW 런타임 고지 및 라이선스 원문 포함
- GitHub 메인 README와 분리된 HTML 없는 포터블 패키지 전용 README 추가
- DeX·단일창 Scrcpy 파일·폴더 드롭 중 세션 대상 폴더 push만 처리하는
  `DXMAdbProxy.exe`와 전역 FIFO 관리형 전송 추가
- ASCII 임시 이름과 Base64 UTF-8 최종 이름 복원으로 Windows 7 SP1~11의
  한글·Unicode 파일명을 보존하고 충돌 시 `(1)`, `(2)` 접미사 사용
- 현재 항목과 다음 4개, 크기·경과 시간·완료·실패·대기 수를 보여주는 독립
  이동 상태창, 취소와 세션 종료 연동 추가(퍼센트·남은 시간은 표시하지 않음)
- 휴대폰 대상 폴더 설정, 폴더 구조·빈 폴더 보존, staging 최종 반영과
  재분석 지점 건너뛰기, 최종 이동 커밋 표식과 응답 중단 복구 추가
- 요청 registry, background ADB script 입력, 대상 폴더 snapshot과 유휴 첫
  세션의 중단 `.part` 정리로 취소·타임아웃·설정 저장·강제 분리 경합 보강
- 관리형 전송 기본값은 켜짐이며 설정에서 끄면 새 DeX·단일창부터 Scrcpy
  순정 파일 드롭 사용
- ADB 공통 `1.0.41` 문구 대신 실제 `Version ...` 빌드 값을 설정·진단에 표시
- 현재 휴대폰 확인 기준을 Android 16 / One UI 8.x로 명시하고 One UI 7.x
  이하의 검은 DeX 창 가능성을 문서화

2026-07-22 .NET Framework 4.6.2 참조 어셈블리로 v1.1.0 x64 Debug와
Release를 경고 0, 오류 0으로 재빌드했다. 실제 Android 16 기기에
한글·Unicode 이름을 관리형 경로로 전송해 원래 이름과 크기를 확인했으며,
proxy의 일반 ADB 명령 stdout/stderr/종료 코드 전달도 확인했다.
`DX-Manager-v1.1.0-win-x64.zip`은 53개 파일이며 PDB, 사용자 설정, 로그,
테스트 스크린샷과 `.gitkeep`이 없고 Scrcpy 4.1 및 `DXMAdbProxy.exe`가
포함된 것을 확인했다. 최종 ZIP SHA-256은
`57341CC63473E2286D4BCD43C0BF707D5EA104FD1C96A75F489ADF14F6CE6A40`이다.
Windows 7 SP1~11의 폴더 전체 전송, 사용자 대상 경로,
취소 및 독립 상태창 회귀는 사용자 실기 테스트 뒤 완료 처리한다.

2026-07-17 .NET Framework 4.6.2 참조 어셈블리로 x64 Release 재빌드가
경고 0, 오류 0으로 통과했다. `DX-Manager-v1.0.0-win-x64.zip` 56개 항목을
검사해 사용자 `settings.json`, config 폴더, PDB, 로그, 테스트 스크린샷,
임시 파일과 `.gitkeep`이 포함되지 않은 것을 확인했다.

2026-07-17 GitHub 저장소를 public으로 전환하고 `v1.0.0` 태그와
`DX Manager v1.0.0` Release를 게시했다. Release 자산은
`DX-Manager-v1.0.0-win-x64.zip` 하나이며 공개 API에서 업로드 상태와 크기를
재확인했다.

2026-07-13 현재 .NET Framework 4.6.2 참조 어셈블리로 x64 Debug/Release
빌드가 모두 경고 0, 오류 0으로 통과했다.

2026-07-15 변경 후에도 같은 .NET Framework 4.6.2 참조 어셈블리로 x64
Debug/Release 재빌드가 모두 경고 0, 오류 0으로 통과했다. 패키징 스크립트로
`dist\DX Manager`와 `DX-Manager-v1.0.0-win-x64.zip`을 생성하고, PDB·런타임
설정·로그·스크린샷 및 `.gitkeep`이 포함되지 않은 것을 확인했다.

2026-07-14 연결 해제 로그에서 고정 기기가 없을 때 target serial을 비운 뒤
즉시 복구하는 1초 주기 상태 반복을 확인했다. 선택 서비스가 기기 없음
상태에서도 고정 serial을 유지하도록 수정하고 별도 복구 처리를 제거했다.
연결 해제 상태의 조용한 감시와 같은 휴대폰 재연결을 실기 재확인했다.

2026-07-14 휴대폰 두 대를 USB로 연결한 경우와 두 휴대폰의 USB/무선 ADB가
동시에 네 개의 transport로 표시되는 경우를 실기 확인했다. 처음 선택한 물리
휴대폰만 유지하고 다른 휴대폰은 무시했으며, 고정된 같은 휴대폰의 USB↔무선
전환은 정상적으로 세션을 정리하고 다시 실행했다.

## 이번 작업의 v1 기기 정책

- 앱이 처음 선택한 휴대폰을 종료까지 고정한다.
- 다른 휴대폰이 추가되거나 고정 기기가 분리되어도 다른 폰으로 전환하지 않는다.
- `ro.serialno`/`ro.boot.serialno`/Android ID가 같으면 USB와 무선 ADB 주소가
  달라도 같은 휴대폰으로 인정한다.
- 다중 휴대폰 선택과 동시 제어는 v2 후보 기능이다.

## 다음 확인

1. Windows 7 SP1~11에서 Scrcpy 4.1 DeX/단일창 시작·종료 회귀 확인
2. 한글·Unicode 단일/복수 파일, 충돌 이름, 취소와 순정 전환 실기 확인
3. 설정·환경 점검의 실제 ADB `Version ...` 표시 확인
4. Scrcpy 4.1에서 오른쪽 Shift 호환 보정 필요 여부 확인
5. v1.1.0 패키지에서 helper, 문서, 라이선스와 런타임 데이터 제외 확인
6. 공개 Release 사용 피드백과 새 이슈 확인
7. Scrcpy 4.0/SDL3 오른쪽 Shift 재현 내용을 upstream에 보고

빌드·커밋·배포 전 `bin\Debug`, `bin\Release`의 `logs`, `screenshot` 테스트
파일을 비운다. 실기 확인하지 않은 흐름은 문서나 보고에서 확인 완료로 쓰지 않는다.
