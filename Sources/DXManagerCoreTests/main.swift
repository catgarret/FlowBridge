import DXManagerCore
import Darwin
import Foundation

private var failures = 0
@MainActor
private func check(_ condition: @autoclosure () -> Bool, _ message: String) {
    if !condition() { failures += 1; print("FAIL: \(message)") }
}

let text = "List of devices attached\nR3CN123 device product:x model:SM_S928N transport_id:1\n192.168.0.2:5555 offline transport_id:2\n"
let devices = ADBService.parseDevices(text)
check(devices.count == 2, "ADB device count")
check(devices.first?.serial == "R3CN123", "ADB serial")
check(devices.first?.displayName == "SM S928N", "model normalization")
check(devices.last?.isReady == false, "offline state")

let merged = ADBService.mergeTransports([
    Device(serial: "172.30.1.3:38735", state: "device", model: "SM_S928N", physicalSerial: "R3CX10CD9YA"),
    Device(serial: "adb-R3CX._adb-tls-connect._tcp", state: "device", model: "SM_S928N", physicalSerial: "R3CX10CD9YA")
])
check(merged.count == 1, "same physical device transport merge")
check(merged.first?.serial == "172.30.1.3:38735", "explicit wireless endpoint preference")

let mdnsDump = """
List of discovered mdns services
adb-test _adb-tls-pairing._tcp 192.168.0.4:37123
adb-test _adb-tls-connect._tcp 192.168.0.4:38911
adb-test _adb-tls-connect._tcp 192.168.0.4:38911
"""
let mdns = ADBService.parseMDNSServices(mdnsDump)
check(mdns.count == 2, "mDNS service parsing and deduplication")
check(mdns.first?.isPairing == true, "mDNS pairing classification")
check(mdns.last?.endpoint == "192.168.0.4:38911", "mDNS connect endpoint")

let downloadEntries = ADBService.parseDownloadEntries("Folder/\nphoto.jpg\nnotes.txt\n")
check(downloadEntries.count == 3, "Download entry parsing")
check(downloadEntries.first?.isDirectory == true, "Download folders sort first")
check(downloadEntries.first?.name == "Folder", "Download folder suffix trimming")

let launcherDump = """
3 activities found:
  Activity #0:
    com.android.settings/.Settings
  Activity #1:
    com.samsung.android.dialer/.DialtactsActivity
  Activity #2:
    com.example.my_weather/.MainActivity
"""
let launcherApps = ADBService.parseLauncherApps(launcherDump)
check(launcherApps.count == 3, "launcher app parsing")
check(launcherApps.contains { $0.name == "설정" && $0.package == "com.android.settings" }, "known localized app name")
check(launcherApps.contains { $0.name == "My Weather" }, "fallback friendly app name")

let dump = "mDisplayId=0\nDisplayDeviceInfo{mDisplayId = 4, 1920 x 1080}\nmDisplayId=4\nmDisplayId=7"
check(DisplayParser.ids(from: dump) == Set([4, 7]), "unique non-primary display IDs")
check(DisplaySettings().isValid, "default display settings")
check(!DisplaySettings(width: 5000).isValid, "maximum width")
check(!DisplaySettings(dpi: 119).isValid, "minimum DPI")

let legacyJSON = #"{"display":{"width":1280,"height":720,"dpi":240,"bitrate":8,"fps":30},"scrcpyPath":"","adbPath":""}"#.data(using: .utf8)!
let migrated = try! JSONDecoder().decode(AppSettings.self, from: legacyJSON)
check(migrated.deviceDisplays.isEmpty, "legacy settings migration")
check(migrated.appProfiles.isEmpty, "legacy app-profile migration")
check(!migrated.phoneNotificationsEnabled, "legacy notification settings migration")
check(migrated.favoritePackages.count == 3, "legacy favorites migration")
check(migrated.deviceAliases.isEmpty, "legacy device aliases migration")
check(migrated.deviceNativeDisplays.isEmpty, "legacy native display migration")

let screenState = ADBService.parsePhoneScreenState(power: "mWakefulness=Awake", policy: "showing=true screenState=SCREEN_STATE_ON")
check(screenState.isAwake && screenState.isLocked, "screen and keyguard state parsing")
let parsedContacts = ADBService.parseContacts("Row: 0 display_name=홍길동, data1=010-1234-5678\n")
check(parsedContacts.first == PhoneContact(name: "홍길동", number: "010-1234-5678"), "contact parsing")
let parsedCalls = ADBService.parseCalls("Row: 0 number=01012345678, type=1, date=1700000000000\n")
check(parsedCalls.first?.type == 1, "call history parsing")
let parsedMessages = ADBService.parseMessages("Row: 0 address=01012345678, body=안녕, 반가워요, date=1700000000000\n")
check(parsedMessages.first?.body == "안녕, 반가워요", "message history parsing with comma")
let outgoingMessage = ADBService.parseMessages("Row: 0 address=01012345678, body=보냄, date=1700000000000, type=2\n")
check(outgoingMessage.first?.isOutgoing == true, "outgoing message classification")
let nativeSize = ADBService.parseNativeDisplaySize("Physical size: 1440x3120\nOverride size: 1080x2340")
check(nativeSize?.width == 3120 && nativeSize?.height == 1440, "native display size parsing and landscape normalization")

let notificationDump = """
    NotificationRecord(0x01: pkg=com.samsung.android.messaging user=UserHandle{0} id=1 tag=null importance=4 key=0|com.samsung.android.messaging|1|null|10001: Notification(channel=messages category=msg))
      key=0|com.samsung.android.messaging|1|null|10001
            android.title=String (홍길동)
            android.text=String (테스트 문자입니다.)
    NotificationRecord(0x02: pkg=com.example.app user=UserHandle{0} id=2 tag=null importance=3 key=0|com.example.app|2|null|10002: Notification(channel=general))
      key=0|com.example.app|2|null|10002
            android.title=String (업데이트)
            android.text=String (완료되었습니다.)
"""
let notifications = NotificationParser.parse(notificationDump)
check(notifications.count == 2, "notification dump parsing")
check(notifications.first?.kind == .message, "message notification classification")
check(notifications.last?.kind == .application, "application notification classification")
check(notifications.first?.body == "테스트 문자입니다.", "notification body parsing")

if failures > 0 { exit(1) }
print("DXManagerCoreTests: all checks passed")
