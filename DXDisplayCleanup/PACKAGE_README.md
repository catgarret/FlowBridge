# DX Display Cleaner 1.0.0

DX Display Cleaner is an optional Android companion for DX Manager. It checks
and removes the `overlay_display_devices` setting when a simulated secondary
display remains on the phone after an interrupted session.

## Install and grant permission

1. Install `DX-Display-Cleaner-v1.0.0.apk` on the phone. You may open it on the
   phone or drop the standalone APK onto a running DX Manager scrcpy window.
2. Enable USB debugging and approve this computer.
3. In DX Manager, open **Settings > Diagnostics > Phone virtual display
   cleanup** and select **Grant cleanup permission**. The button is enabled only
   after the exact package and official signing certificate are verified.

If needed, the equivalent manual command is:

```text
adb shell pm grant io.github.mazemei.dxdisplaycleanup android.permission.WRITE_SECURE_SETTINGS
```

Open the app to check or clean the display. You may also add its Quick Settings
tile or home-screen widget. A color DX icon means an overlay setting is active;
a grayscale icon means none is set. A warning icon means permission is missing
or the status could not be read.

The cleanup action removes Android's single global simulated-secondary-display
setting. It cannot distinguish a display created by DX Manager from one that
you selected manually in Developer options.

The grant survives phone restarts and updates signed with the same official
certificate. Uninstalling the app removes the grant, so repeat the DX Manager
permission step after reinstalling it.

The app requests no Internet permission and contains no arbitrary shell or data
collection feature. Verify the official signing fingerprint in `SIGNING.md`.

---

# DX 가상화면 정리 1.0.0

DX 가상화면 정리는 중단된 DX Manager 세션 뒤 휴대폰에 보조 디스플레이
시뮬레이션 화면이 남았을 때 `overlay_display_devices` 설정을 확인하고
제거하는 선택형 Android 보조 앱입니다.

## 설치 및 권한 부여

1. 휴대폰에 `DX-Display-Cleaner-v1.0.0.apk`를 설치합니다. 휴대폰에서 APK를
   직접 열거나 실행 중인 DX Manager scrcpy 창에 APK 하나를 놓아도 됩니다.
2. USB 디버깅을 켜고 이 컴퓨터의 RSA 연결을 승인합니다.
3. DX Manager에서 **설정 > 진단 > 휴대폰 가상화면 정리 도구**를 열고
   **정리 앱 권한 부여**를 누릅니다. 정확한 패키지와 공식 서명 인증서가
   확인된 경우에만 버튼이 활성화됩니다.

필요한 경우 같은 작업을 다음 ADB 명령으로 직접 실행할 수도 있습니다.

```text
adb shell pm grant io.github.mazemei.dxdisplaycleanup android.permission.WRITE_SECURE_SETTINGS
```

앱에서 상태를 확인하거나 가상화면을 정리할 수 있습니다. 빠른 설정 타일과
홈 화면 위젯도 추가할 수 있습니다. 컬러 DX 아이콘은 overlay 설정 활성,
흑백 아이콘은 비활성을 뜻합니다. 경고 아이콘은 권한이 없거나 상태 확인에
실패한 경우입니다.

Android에는 보조 디스플레이 시뮬레이션용 global 설정이 하나만 있으므로,
정리하면 DX Manager가 만든 화면뿐 아니라 개발자 옵션에서 사용자가 직접
선택한 시뮬레이션 화면도 함께 제거됩니다.

권한은 휴대폰 재부팅과 같은 공식 서명의 앱 업데이트 후에도 유지됩니다. 앱을
삭제하면 권한도 제거되므로 다시 설치한 뒤 DX Manager에서 권한을 다시
부여하십시오.

인터넷 권한, 임의 shell 실행과 데이터 수집 기능은 없습니다. 공식 서명
지문은 함께 제공된 `SIGNING.md`에서 확인할 수 있습니다.
