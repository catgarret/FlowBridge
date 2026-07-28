# DX Companion 1.1.0

DX Companion is an optional Android companion for DX Manager. It helps recover
from an interrupted session by removing a simulated secondary display and
turning off Developer options **Stay awake**.

## Install and grant permission

1. Install `DX-Companion-v1.1.0.apk` on the phone. You may open it on the
   phone or drop the standalone APK onto a running DX Manager scrcpy window.
2. Enable USB debugging and approve this computer.
3. In DX Manager, open **Settings > Diagnostics > Phone virtual display
   cleanup** and select **Grant cleanup permission**. The button is enabled only
   after the exact package and official signing certificate are verified.

The app provides separate buttons for virtual-display cleanup and Stay awake,
plus a combined cleanup button. You may also add its Quick Settings tile or
compact 2 × 1 home-screen widget. The tile and widget clean both items by
default, and their targets can be changed in the app.

The cleanup action removes Android's single global simulated-secondary-display
setting. It cannot distinguish a display created by DX Manager from one that
you selected manually in Developer options. The Stay awake action sets the
corresponding Developer option to off.

The grant survives phone restarts and updates signed with the same official
certificate. Uninstalling the app removes the grant, so repeat the DX Manager
permission step after reinstalling it.

The app requests no Internet permission and contains no arbitrary shell or data
collection feature. Verify the official signing fingerprint in `SIGNING.md`.

---

# DX Companion 1.1.0

DX Companion은 DX Manager 세션이 비정상적으로 끝났을 때 휴대폰에 남은
보조 디스플레이 시뮬레이션 화면을 제거하고, 개발자 옵션의 **절전모드
해제**를 끄는 선택형 Android 보조 앱입니다.

## 설치 및 권한 부여

1. 휴대폰에 `DX-Companion-v1.1.0.apk`를 설치합니다. 휴대폰에서 APK를
   직접 열거나 실행 중인 DX Manager scrcpy 창에 APK 하나를 놓아도 됩니다.
2. USB 디버깅을 켜고 이 컴퓨터의 RSA 연결을 승인합니다.
3. DX Manager에서 **설정 > 진단 > 휴대폰 가상화면 정리 도구**를 열고
   **정리 앱 권한 부여**를 누릅니다. 정확한 패키지와 공식 서명 인증서가
   확인된 경우에만 버튼이 활성화됩니다.

앱에서는 가상화면 제거와 절전모드 해제 끄기를 각각 실행하거나 두 항목을
한 번에 정리할 수 있습니다. 빠른 설정 타일과 2 × 1 홈 화면 위젯도 사용할
수 있습니다. 타일과 위젯은 기본적으로 두 항목을 모두 정리하며, 앱 설정에서
정리 대상을 바꿀 수 있습니다.

Android에는 보조 디스플레이 시뮬레이션용 global 설정이 하나만 있으므로,
정리하면 DX Manager가 만든 화면뿐 아니라 개발자 옵션에서 사용자가 직접
선택한 시뮬레이션 화면도 함께 제거됩니다. 절전모드 해제 정리는 해당 개발자
옵션을 끔 상태로 바꿉니다.

권한은 휴대폰 재부팅과 같은 공식 서명의 앱 업데이트 후에도 유지됩니다. 앱을
삭제하면 권한도 제거되므로 다시 설치한 뒤 DX Manager에서 권한을 다시
부여하십시오.

인터넷 권한, 임의 shell 실행과 데이터 수집 기능은 없습니다. 공식 서명
지문은 함께 제공된 `SIGNING.md`에서 확인할 수 있습니다.
