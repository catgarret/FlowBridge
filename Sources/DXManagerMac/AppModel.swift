import Foundation
import AppKit
import CryptoKit
import ServiceManagement
import UserNotifications
import UniformTypeIdentifiers
import DXManagerCore

final class MacNotificationDelegate: NSObject, UNUserNotificationCenterDelegate {
    func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification, withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .list, .sound])
    }
}

enum SessionPhase { case idle, launching, running, failed }

@MainActor
final class AppModel: ObservableObject {
    @Published var devices: [Device] = []
    @Published var selectedSerial = ""
    @Published var deviceAlias = ""
    @Published var settings = DisplaySettings()
    @Published var nativeDisplayWidth = 0
    @Published var nativeDisplayHeight = 0
    @Published var packageNames = ["com.android.settings", "", ""]
    @Published var installedApps: [InstalledApp] = []
    @Published var appSearch = ""
    @Published var phoneNumber = ""
    @Published var messageBody = ""
    @Published var isSendingMessage = false
    @Published var phoneSearch = ""
    @Published var contacts: [PhoneContact] = []
    @Published var contactPhotoURLs: [String: URL] = [:]
    @Published var activeNotifications: [PhoneNotification] = []
    @Published var appIconURLs: [String: URL] = [:]
    @Published var notificationDeliveryStatus = ""
    @Published var notificationAuthorizationStatus = "확인 중"
    @Published var recentCalls: [PhoneCall] = []
    @Published var recentMessages: [PhoneMessage] = []
    @Published var wirelessEndpoint = ""
    @Published var pairingEndpoint = ""
    @Published var pairingCode = ""
    @Published var discoveredWirelessServices: [ADBMDNSService] = []
    @Published var diagnostics = ""
    @Published var transferStatus = ""
    @Published var isTransferring = false
    @Published var remoteFiles: [RemoteFile] = []
    @Published var remoteDirectory = "/sdcard/Download"
    @Published var remoteThumbnailURLs: [String: URL] = [:]
    @Published var pendingTransferURLs: [URL] = []
    @Published var status = "ADB 기기를 검색하는 중입니다."
    @Published var isBusy = false
    @Published var autoHideMinutes = 10
    @Published var keyboardCorrectionEnabled = false
    @Published var shiftEnterMode = false
    @Published var launchAtLogin = false
    @Published var presenceMode: AppPresenceMode = .dockAndMenuBar
    @Published var openMainWindowAtLaunch = true
    @Published var appLaunchMode: AppLaunchMode = .desktopWindow
    @Published var mediaVolume = 8
    @Published var controlBarPosition: ControlBarPosition = .bottom
    @Published var protectedScreenDetected = false
    @Published var automaticReconnect = true
    @Published var phoneNotificationsEnabled = false
    @Published var messageNotificationsEnabled = false
    @Published var appNotificationsEnabled = false
    @Published var blockedNotificationPackages: Set<String> = []
    @Published var dimPhoneOnStart = false
    @Published var hasActiveSession = false
    @Published var sessionPhase: SessionPhase = .idle
    @Published var phoneNeedsUnlock = false
    @Published var activeScreenMode = ""
    @Published var updateStatus = "업데이트를 확인하지 않았습니다."
    @Published var latestVersion = ""
    @Published var isUpdateAvailable = false

    private var appSettings = AppSettings()
    private var controller: DXSessionController?
    private let transferQueue = TransferQueue()
    private let miniBars = MiniControlBarManager()
    private let keyboardCorrection = KeyboardCorrectionService()
    private var logLines: [String] = []
    private var lastActivity = Date()
    private var activityMonitor: Any?
    private let notificationDelegate = MacNotificationDelegate()
    private var notificationPollInProgress = false
    private var notificationBaselineSerial = ""
    private var seenNotificationFingerprints: Set<String> = []
    private var lastPhoneDataRefresh = Date.distantPast
    private var brightnessBeforeSession: ScreenBrightnessState?
    private var brightnessSessionSerial = ""
    private var sessionAttempt = UUID()
    private var didConfigureLaunchPresentation = false
    private var protectedScreenPollInProgress = false
    private var overlayCleanupInProgress: Set<String> = []
    private var iconLoadsInProgress: Set<String> = []
    private var remoteThumbnailLoadsInProgress: Set<String> = []
    private var lastWindowPlacementCapture = Date.distantPast

    init() {
        load()
        autoHideMinutes = appSettings.autoHideMinutes
        launchAtLogin = SMAppService.mainApp.status == .enabled
        presenceMode = appSettings.presenceMode
        openMainWindowAtLaunch = appSettings.openMainWindowAtLaunch
        appLaunchMode = appSettings.appLaunchMode
        controlBarPosition = appSettings.controlBarPosition
        wirelessEndpoint = appSettings.lastWirelessEndpoint
        automaticReconnect = appSettings.automaticReconnect
        phoneNotificationsEnabled = appSettings.phoneNotificationsEnabled
        messageNotificationsEnabled = appSettings.messageNotificationsEnabled
        appNotificationsEnabled = appSettings.appNotificationsEnabled
        blockedNotificationPackages = appSettings.blockedNotificationPackages
        dimPhoneOnStart = appSettings.turnPhoneScreenOffOnStart
        packageNames = appSettings.favoritePackages
        UNUserNotificationCenter.current().delegate = notificationDelegate
        DispatchQueue.main.async { [weak self] in
            self?.refreshNotificationAuthorization()
            if self?.phoneNotificationsEnabled == true || self?.messageNotificationsEnabled == true || self?.appNotificationsEnabled == true { self?.notificationSettingsChanged() }
        }
        refresh()
        if automaticReconnect, !wirelessEndpoint.isEmpty { connectWireless() }
        checkForUpdates()
        activityMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .mouseMoved, .leftMouseDown, .rightMouseDown, .scrollWheel]) { [weak self] event in
            self?.lastActivity = Date()
            return event
        }
        Timer.scheduledTimer(withTimeInterval: 3, repeats: true) { [weak self] _ in
            Task { @MainActor in
                guard let self else { return }
                if !self.isBusy { self.refresh(silent: true) }
                self.applyAutoHide()
                self.pollPhoneNotifications()
                self.pollProtectedScreen()
                if Date().timeIntervalSince(self.lastPhoneDataRefresh) >= 30, !self.isBusy, !self.selectedSerial.isEmpty { self.refreshPhoneData(silent: true) }
            }
        }
        Timer.scheduledTimer(withTimeInterval: 0.15, repeats: true) { [weak self] _ in Task { @MainActor in self?.syncMiniBars() } }
    }

    func refresh(silent: Bool = false) {
        perform(showBusy: !silent) { [settings = appSettings] in
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            let list = try adb.devices()
            let apps: [InstalledApp]
            if !silent, let serial = list.first?.serial { apps = (try? adb.launcherApps(serial: serial)) ?? [] }
            else { apps = [] }
            return { model in
                model.devices = list
                if !apps.isEmpty { model.installedApps = apps }
                if !list.contains(where: { $0.serial == model.selectedSerial }) { model.selectedSerial = list.first?.serial ?? "" }
                if let selected = list.first(where: { $0.serial == model.selectedSerial }) {
                    let key = model.deviceIdentityKey(selected)
                    model.deviceAlias = model.appSettings.deviceAliases[key] ?? ""
                    if let native = model.appSettings.deviceNativeDisplays[key] {
                        model.nativeDisplayWidth = native.width; model.nativeDisplayHeight = native.height
                    }
                    model.cleanupStaleOverlayIfNeeded(serial: selected.serial)
                    if model.sessionPhase == .idle { model.restoreBrightnessIfNeeded(serial: selected.serial) }
                }
                if !silent { model.status = list.isEmpty ? "연결된 ADB 기기가 없습니다." : String(format: NSLocalizedString("%d대의 기기를 찾았습니다.", comment: ""), list.count) }
            }
        }
    }

    func pairWireless() {
        let endpoint = pairingEndpoint.trimmingCharacters(in: .whitespacesAndNewlines)
        let code = pairingCode.trimmingCharacters(in: .whitespacesAndNewlines)
        perform { [settings = appSettings] in
            guard !endpoint.isEmpty, !code.isEmpty else { throw DXError.commandFailed("페어링 IP:포트와 코드를 입력해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            _ = try adb.pair(endpoint, code: code)
            Thread.sleep(forTimeInterval: 0.8)
            let services = (try? adb.mdnsServices()) ?? []
            let connectEndpoint = services.first(where: \.isConnect)?.endpoint
            if let connectEndpoint { _ = try? adb.connect(connectEndpoint) }
            return { model in
                model.pairingCode = ""
                model.discoveredWirelessServices = services
                if let connectEndpoint {
                    model.wirelessEndpoint = connectEndpoint
                    model.appSettings.lastWirelessEndpoint = connectEndpoint
                    model.save()
                    model.status = "페어링과 무선 연결이 완료되었습니다. 다음부터 자동으로 연결합니다."
                } else {
                    model.status = "페어링은 완료되었습니다. 새로고침하면 무선 기기가 자동으로 나타납니다."
                }
                model.refresh()
            }
        }
    }

    func discoverWirelessSetup() {
        perform { [settings = appSettings] in
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let services = try ADBService(executable: adbPath).mdnsServices()
            return { model in
                model.discoveredWirelessServices = services
                if let pairing = services.first(where: \.isPairing) { model.pairingEndpoint = pairing.endpoint }
                if let connect = services.first(where: \.isConnect) { model.wirelessEndpoint = connect.endpoint }
                model.status = services.isEmpty ? "무선 디버깅 서비스를 찾지 못했습니다. 휴대폰에서 페어링 화면을 열어 주세요." : "무선 디버깅 서비스 \(services.count)개를 찾았습니다."
            }
        }
    }

    func prepareWirelessFromUSB() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("USB로 연결된 기기를 선택해 주세요.") }
            guard serial.range(of: #"^[A-Za-z0-9]+$"#, options: .regularExpression) != nil else {
                throw DXError.commandFailed("이미 무선으로 연결되어 있습니다. USB 케이블 연결 시 한 번에 무선 전환할 수 있습니다.")
            }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            let ip = try adb.wirelessIPv4(serial: serial)
            _ = try adb.enableTCPIP(serial: serial, port: 5555)
            Thread.sleep(forTimeInterval: 1.2)
            let endpoint = "\(ip):5555"
            _ = try adb.connect(endpoint)
            return { model in
                model.wirelessEndpoint = endpoint
                model.appSettings.lastWirelessEndpoint = endpoint
                model.save()
                model.status = "무선 연결 준비가 끝났습니다. 이제 USB 케이블을 분리해도 됩니다."
                model.refresh()
            }
        }
    }

    func loadDiagnostics() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let values = try ADBService(executable: adbPath).deviceProperties(serial: serial)
            let report = [
                "Serial: \(serial)",
                "Manufacturer: \(values["ro.product.manufacturer"] ?? "")",
                "Model: \(values["ro.product.model"] ?? "")",
                "Android: \(values["ro.build.version.release"] ?? "")",
                "SDK: \(values["ro.build.version.sdk"] ?? "")",
                "One UI: \(values["ro.build.version.oneui"] ?? "")",
                "Security patch: \(values["ro.build.version.security_patch"] ?? "")",
                "ADB: \(adbPath)",
                "scrcpy: \(ToolLocator.scrcpy(settings.scrcpyPath) ?? "not found")"
            ].joined(separator: "\n")
            return { model in model.diagnostics = report; model.status = "기기 진단을 갱신했습니다." }
        }
    }

    func sendKeyEvent(_ code: Int, label: String) {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            try ADBService(executable: adbPath).keyEvent(serial: serial, code: code)
            return { model in model.status = "\(label) 명령을 보냈습니다." }
        }
    }

    func captureRegion() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.png]
        panel.nameFieldStringValue = "Flow-Bridge-Capture-\(Int(Date().timeIntervalSince1970)).png"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
        process.arguments = ["-i", url.path]
        do { try process.run(); status = "캡처 영역을 선택해 주세요." } catch { status = error.localizedDescription }
    }

    func captureScrcpyWindow(windowID: CGWindowID, title: String) {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.png]
        panel.nameFieldStringValue = title.replacingOccurrences(of: " / ", with: "-") + ".png"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
        process.arguments = ["-x", "-l", String(windowID), url.path]
        do { try process.run(); process.waitUntilExit(); status = process.terminationStatus == 0 ? "scrcpy 창을 캡처했습니다." : "scrcpy 창 캡처에 실패했습니다." }
        catch { status = error.localizedDescription }
    }

    func connectWireless() {
        let endpoint = wirelessEndpoint.trimmingCharacters(in: .whitespacesAndNewlines)
        perform { [settings = appSettings] in
            guard !endpoint.isEmpty else { throw DXError.commandFailed("IP:포트를 입력해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let message = try ADBService(executable: adbPath).connect(endpoint)
            return { model in
                model.appSettings.lastWirelessEndpoint = endpoint
                model.save()
                model.status = message
                model.refresh()
            }
        }
    }

    func startDeX() {
        activeScreenMode = "DEX 모드"
        let placement = savedPlacement(kind: "desktop")
        let deviceName = selectedScreenDeviceName
        start(exclusiveMainDisplay: true, trackMainSession: true, createsOverlay: true) { try $0.startDeX(serial: $1, deviceName: deviceName, settings: $2, placement: placement) }
    }
    func startPhoneMirror() {
        activeScreenMode = "휴대폰 미러링"
        let placement = savedPlacement(kind: "phone")
        let deviceName = selectedScreenDeviceName
        start(exclusiveMainDisplay: true, trackMainSession: true) { try $0.startPhoneMirror(serial: $1, deviceName: deviceName, settings: $2, placement: placement) }
    }

    func volumeDown() { sendKeyEvent(25, label: "볼륨 낮추기") }
    func volumeUp() { sendKeyEvent(24, label: "볼륨 높이기") }
    func setMediaVolume(_ level: Int) {
        let serial = selectedSerial, value = level
        mediaVolume = value
        perform(showBusy: false) { [settings = appSettings] in
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            try ADBService(executable: adbPath).setMediaVolume(serial: serial, level: value)
            return { _ in }
        }
    }

    func applyDisplayPreset(width: Int, height: Int) {
        settings.width = width; settings.height = height
        save(); status = "화면 품질을 \(width)×\(height)로 설정했습니다."
    }

    func applyFramePreset(_ fps: Int) {
        settings.fps = fps
        save()
        status = "화면 프레임을 \(fps) FPS로 적용했습니다."
    }

    func applyNativeDisplayPreset() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let size = try ADBService(executable: adbPath).nativeDisplaySize(serial: serial)
            return { model in
                model.nativeDisplayWidth = size.width; model.nativeDisplayHeight = size.height
                model.settings.width = size.width; model.settings.height = size.height
                if let key = model.selectedDeviceKey { model.appSettings.deviceNativeDisplays[key] = DisplaySettings(width: size.width, height: size.height, dpi: model.settings.dpi, bitrate: model.settings.bitrate, fps: model.settings.fps) }
                model.save(); model.status = "Galaxy 최대 해상도 \(size.width)×\(size.height)를 적용했습니다."
            }
        }
    }

    var isNativeDisplayPresetSelected: Bool {
        nativeDisplayWidth > 0 && settings.width == nativeDisplayWidth && settings.height == nativeDisplayHeight
    }

    var nativeDisplayPresetTitle: String {
        nativeDisplayWidth > 0 ? "기기 최대 · \(nativeDisplayWidth)×\(nativeDisplayHeight)" : "기기 최대"
    }
    func loadPackages() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let apps = try ADBService(executable: adbPath).launcherApps(serial: serial)
            return { model in model.installedApps = apps; model.status = String(format: NSLocalizedString("실행 가능한 앱 %d개를 불러왔습니다.", comment: ""), apps.count) }
        }
    }

    func openDialer() {
        let serial = selectedSerial, number = phoneNumber
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            try ADBService(executable: adbPath).openDialer(serial: serial, number: number)
            return { model in model.status = "Galaxy 전화 화면에 번호를 열었습니다." }
        }
    }

    func composeMessage() {
        let serial = selectedSerial, number = phoneNumber, body = messageBody
        guard !isSendingMessage else { return }
        isSendingMessage = true
        status = "Galaxy에서 메시지 전송 버튼을 확인하는 중입니다."
        Task.detached { [weak self, settings = appSettings] in
            do {
                guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
                guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
                try ADBService(executable: adbPath).sendMessage(serial: serial, number: number, body: body)
                await MainActor.run {
                    guard let self else { return }
                    self.isSendingMessage = false
                    self.messageBody = ""
                    self.status = "메시지를 전송했습니다."
                    self.appendLog(self.status)
                    self.refreshPhoneData(silent: true)
                }
            } catch {
                await MainActor.run {
                    guard let self else { return }
                    self.isSendingMessage = false
                    self.status = error.localizedDescription
                    self.appendLog("ERROR: \(error.localizedDescription)")
                }
            }
        }
    }

    func selectPhoneNumber(_ number: String) { phoneNumber = number }

    func refreshPhoneData() { refreshPhoneData(silent: false) }

    private func refreshPhoneData(silent: Bool) {
        let serial = selectedSerial
        lastPhoneDataRefresh = Date()
        perform(showBusy: !silent) { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            let contacts = try adb.contacts(serial: serial)
            let calls = try adb.recentCalls(serial: serial)
            let messages = try adb.recentMessages(serial: serial)
            return { model in
                model.contacts = contacts; model.recentCalls = calls; model.recentMessages = messages
                model.refreshContactPhotoCache(for: contacts, serial: serial)
                if !silent { model.status = "주소록과 최근 전화·문자를 불러왔습니다." }
            }
        }
    }

    func contactPhotoURL(for number: String) -> URL? { contactPhotoURLs[normalizedPhoneNumber(number)] }

    private func refreshContactPhotoCache(for contacts: [PhoneContact], serial: String) {
        guard let adbPath = ToolLocator.adb(appSettings.adbPath) else { return }
        let directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("FlowBridge/ContactPhotos", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        var cached = contactPhotoURLs
        var missing: [(String, String, URL)] = []
        for contact in contacts where !contact.photoURI.isEmpty {
            let key = normalizedPhoneNumber(contact.number)
            guard !key.isEmpty else { continue }
            let digest = SHA256.hash(data: Data("\(serial)|\(key)".utf8)).map { String(format: "%02x", $0) }.joined()
            let url = directory.appendingPathComponent("\(digest).jpg")
            if FileManager.default.fileExists(atPath: url.path), NSImage(contentsOf: url) != nil { cached[key] = url }
            else if FileManager.default.fileExists(atPath: url.path) { try? FileManager.default.removeItem(at: url); missing.append((key, contact.photoURI, url)) }
            else { missing.append((key, contact.photoURI, url)) }
        }
        contactPhotoURLs = cached
        guard !missing.isEmpty else { return }
        Task.detached { [weak self] in
            let adb = ADBService(executable: adbPath)
            for (key, uri, url) in missing {
                do {
                    try adb.pullContactPhoto(serial: serial, photoURI: uri, localURL: url)
                    guard NSImage(contentsOf: url) != nil else { try? FileManager.default.removeItem(at: url); continue }
                    await MainActor.run { self?.contactPhotoURLs[key] = url }
                } catch { try? FileManager.default.removeItem(at: url) }
            }
        }
    }

    private func normalizedPhoneNumber(_ number: String) -> String { String(number.filter(\.isNumber).suffix(10)) }

    func applyDeviceSettings() {
        contactPhotoURLs = [:]
        if let saved = appSettings.deviceDisplays[selectedSerial] { settings = saved }
        if let key = selectedDeviceKey, let native = appSettings.deviceNativeDisplays[key] {
            nativeDisplayWidth = native.width; nativeDisplayHeight = native.height
        } else { nativeDisplayWidth = 0; nativeDisplayHeight = 0 }
        deviceAlias = selectedDeviceKey.map { appSettings.deviceAliases[$0] ?? "" } ?? ""
        notificationBaselineSerial = ""
        seenNotificationFingerprints.removeAll()
        if installedApps.isEmpty, !selectedSerial.isEmpty { loadPackages() }
        if sessionPhase == .idle { restoreBrightnessIfNeeded(serial: selectedSerial) }
    }

    func deviceLabel(_ device: Device) -> String {
        let alias = appSettings.deviceAliases[deviceIdentityKey(device)]?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return alias.isEmpty ? device.displayName : alias
    }

    var hasSavedDeviceAlias: Bool {
        guard let key = selectedDeviceKey else { return false }
        return !(appSettings.deviceAliases[key] ?? "").isEmpty
    }

    func saveDeviceAlias() {
        guard let key = selectedDeviceKey else { status = "별칭을 지정할 기기를 선택해 주세요."; return }
        let alias = deviceAlias.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !alias.isEmpty else { removeDeviceAlias(); return }
        appSettings.deviceAliases[key] = String(alias.prefix(40))
        deviceAlias = appSettings.deviceAliases[key] ?? alias
        save()
        status = "기기 별칭을 저장했습니다."
    }

    func removeDeviceAlias() {
        guard let key = selectedDeviceKey else { return }
        appSettings.deviceAliases.removeValue(forKey: key)
        deviceAlias = ""
        save()
        status = "기기 별칭을 삭제하고 기본 이름으로 되돌렸습니다."
    }

    func notificationSettingsChanged() {
        appSettings.phoneNotificationsEnabled = phoneNotificationsEnabled
        appSettings.messageNotificationsEnabled = messageNotificationsEnabled
        appSettings.appNotificationsEnabled = appNotificationsEnabled
        appSettings.blockedNotificationPackages = blockedNotificationPackages
        save()
        guard phoneNotificationsEnabled || messageNotificationsEnabled || appNotificationsEnabled else { return }
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound]) { [weak self] granted, error in
            Task { @MainActor in
                if let error { self?.status = "macOS 알림 권한 요청 실패: \(error.localizedDescription)" }
                else if !granted { self?.status = "macOS 시스템 설정에서 Flow Bridge 알림을 허용해 주세요." }
                else { self?.status = "휴대폰 알림 전달을 켰습니다."; self?.notificationDeliveryStatus = "macOS 알림 허용됨" }
                self?.refreshNotificationAuthorization()
            }
        }
    }

    func refreshNotificationAuthorization() {
        UNUserNotificationCenter.current().getNotificationSettings { [weak self] settings in
            let authorizationStatus = settings.authorizationStatus.rawValue
            Task { @MainActor in
                self?.notificationAuthorizationStatus = switch UNAuthorizationStatus(rawValue: authorizationStatus) ?? .notDetermined {
                case .authorized, .provisional, .ephemeral: "허용됨"
                case .denied: "차단됨 · macOS 시스템 설정에서 허용 필요"
                case .notDetermined: "권한 요청 필요"
                @unknown default: "상태 확인 불가"
                }
            }
        }
    }

    func sendTestNotification() {
        deliver(PhoneNotification(key: UUID().uuidString, package: "", title: "알림 테스트", body: "Mac 알림 센터 전달이 정상적으로 동작합니다.", kind: .application))
    }

    func isNotificationAllowed(package: String) -> Bool { !blockedNotificationPackages.contains(package) }

    func setNotificationAllowed(_ allowed: Bool, package: String) {
        guard !package.isEmpty else { return }
        if allowed { blockedNotificationPackages.remove(package) }
        else { blockedNotificationPackages.insert(package) }
        notificationSettingsChanged()
        status = allowed ? "\(appDisplayName(package: package)) 알림을 허용했습니다." : "\(appDisplayName(package: package)) 알림을 껐습니다."
    }

    func appDisplayName(package: String) -> String {
        installedApps.first(where: { $0.package == package })?.name ?? ADBService.friendlyAppName(for: package)
    }

    func openMacNotificationSettings() {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.Notifications-Settings.extension") else { return }
        NSWorkspace.shared.open(url)
    }

    func loadActiveNotifications() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let items = try ADBService(executable: adbPath).notifications(serial: serial)
            return { model in model.activeNotifications = items; model.status = "Galaxy 알림을 불러왔습니다." }
        }
    }

    func dismissNotification(_ item: PhoneNotification) { dismissNotifications([item]) }
    func dismissAllNotifications() { dismissNotifications(activeNotifications) }

    private func dismissNotifications(_ items: [PhoneNotification]) {
        let serial = selectedSerial
        guard !items.isEmpty else { return }
        perform { [settings = appSettings] in
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            for item in items { try adb.dismissNotification(serial: serial, key: item.key) }
            let remaining = try adb.notifications(serial: serial)
            return { model in model.activeNotifications = remaining; model.status = "Galaxy 알림을 정리했습니다." }
        }
    }

    func saveAppProfile(slot: Int) {
        guard packageNames.indices.contains(slot) else { return }
        let package = packageNames[slot].trimmingCharacters(in: .whitespacesAndNewlines)
        guard !package.isEmpty else { status = "앱 패키지명을 입력해 주세요."; return }
        appSettings.appProfiles[package] = settings
        save()
        status = "\(package) 프로필을 저장했습니다."
    }

    func applyAppProfile(slot: Int) {
        guard packageNames.indices.contains(slot), let profile = appSettings.appProfiles[packageNames[slot]] else {
            status = "저장된 앱 프로필이 없습니다."; return
        }
        settings = profile
        status = "앱 프로필을 적용했습니다."
    }

    func startApp(slot: Int) {
        guard packageNames.indices.contains(slot) else { return }
        let package = packageNames[slot]
        launchApp(package: package, slot: slot + 1)
    }

    func startApp(package: String) {
        launchApp(package: package, slot: 0)
    }

    private func launchApp(package: String, slot: Int) {
        if appLaunchMode == .phoneScreen {
            let placement = savedPlacement(kind: "phone")
            let deviceName = selectedScreenDeviceName
            start(exclusiveMainDisplay: true, trackMainSession: true) { try $0.startAppOnPhone(serial: $1, deviceName: deviceName, package: package, settings: $2, placement: placement) }
        } else {
            start { try $0.startApp(serial: $1, package: package, settings: $2, slot: slot) }
        }
    }

    func appLaunchModeChanged() { appSettings.appLaunchMode = appLaunchMode; save() }
    func controlBarPositionChanged() { appSettings.controlBarPosition = controlBarPosition; save() }

    func assignFavorite(package: String, slot: Int) {
        guard packageNames.indices.contains(slot) else { return }
        packageNames[slot] = package
        save()
        status = "앱 바로 실행 \(slot + 1)에 지정했습니다."
    }

    func toggleFavorite(package: String, slot: Int) {
        guard packageNames.indices.contains(slot) else { return }
        if packageNames[slot] == package {
            packageNames[slot] = ""
            save()
            status = "앱 바로 실행 \(slot + 1) 지정을 해제했습니다."
        } else {
            for index in packageNames.indices where packageNames[index] == package { packageNames[index] = "" }
            assignFavorite(package: package, slot: slot)
        }
    }

    func chooseAndTransfer() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseFiles = true
        panel.canChooseDirectories = true
        guard panel.runModal() == .OK else { return }
        enqueueTransfer(urls: panel.urls)
    }

    func enqueueTransfer(urls: [URL]) {
        for url in urls where !pendingTransferURLs.contains(url) { pendingTransferURLs.append(url) }
        if !urls.isEmpty { status = "전송 대기 목록에 \(pendingTransferURLs.count)개를 추가했습니다." }
    }

    func removePendingTransfer(_ url: URL) { pendingTransferURLs.removeAll { $0 == url } }
    func clearPendingTransfers() { pendingTransferURLs.removeAll() }
    func startPendingTransfers() {
        let urls = pendingTransferURLs
        guard !urls.isEmpty else { return }
        pendingTransferURLs.removeAll()
        transfer(urls: urls)
    }

    func transfer(urls: [URL]) {
        guard !urls.isEmpty else { return }
        let serial = selectedSerial
        isTransferring = true
        transferStatus = "전송 준비 중"
        perform { [settings = appSettings, transferQueue] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            transferQueue.run(adb: adb, serial: serial, urls: urls, remoteDirectory: "/sdcard/Download/") { update in
                Task { @MainActor in
                    self.transferStatus = update.cancelled ? "취소됨: 완료 \(update.completed), 실패 \(update.failed)" :
                        "\(update.current)  완료 \(update.completed) · 실패 \(update.failed) · 대기 \(update.waiting)"
                }
            }
            return { model in model.isTransferring = false; model.status = "파일 전송 작업을 마쳤습니다." }
        }
    }

    @discardableResult
    func pasteFilesFromClipboard() -> Bool {
        let urls = NSPasteboard.general.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL] ?? []
        guard !urls.isEmpty else { status = "Mac 클립보드에 복사된 파일이 없습니다."; return false }
        enqueueTransfer(urls: urls)
        return true
    }

    func loadRemoteFiles(directory: String? = nil) {
        let serial = selectedSerial
        let target = directory ?? remoteDirectory
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let files = try ADBService(executable: adbPath).downloadFiles(serial: serial, directory: target)
            return { model in model.remoteDirectory = target; model.remoteFiles = files; model.status = "Galaxy \(target.replacingOccurrences(of: "/sdcard/Download", with: "Download")) 항목 \(files.count)개를 불러왔습니다." }
        }
    }

    func openRemoteDirectory(_ file: RemoteFile) {
        guard file.isDirectory else { return }
        loadRemoteFiles(directory: file.path)
    }

    func openRemoteParentDirectory() {
        guard remoteDirectory != "/sdcard/Download" else { return }
        loadRemoteFiles(directory: (remoteDirectory as NSString).deletingLastPathComponent)
    }

    func requestRemoteThumbnail(_ file: RemoteFile) {
        let supported = Set(["jpg", "jpeg", "png", "webp", "heic", "heif", "gif", "bmp"])
        let ext = URL(fileURLWithPath: file.name).pathExtension.lowercased()
        guard !file.isDirectory, supported.contains(ext), remoteThumbnailURLs[file.path] == nil,
              !remoteThumbnailLoadsInProgress.contains(file.path), !selectedSerial.isEmpty,
              let adbPath = ToolLocator.adb(appSettings.adbPath) else { return }
        remoteThumbnailLoadsInProgress.insert(file.path)
        let serial = selectedSerial
        Task.detached { [weak self] in
            let cache = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0].appendingPathComponent("FlowBridge/RemoteThumbnails", isDirectory: true)
            try? FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
            let digest = SHA256.hash(data: Data("\(serial)|\(file.path)".utf8)).map { String(format: "%02x", $0) }.joined()
            let output = cache.appendingPathComponent("\(digest).\(ext)")
            if !FileManager.default.fileExists(atPath: output.path) {
                try? ADBService(executable: adbPath).pull(serial: serial, remotePath: file.path, localURL: output)
            }
            await MainActor.run {
                if NSImage(contentsOf: output) != nil { self?.remoteThumbnailURLs[file.path] = output }
                else { try? FileManager.default.removeItem(at: output) }
                self?.remoteThumbnailLoadsInProgress.remove(file.path)
            }
        }
    }

    func copyRemoteFile(_ file: RemoteFile) {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let folder = Self.clipboardDirectory
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let local = folder.appendingPathComponent(file.name)
            try ADBService(executable: adbPath).pull(serial: serial, remotePath: file.path, localURL: local)
            return { model in
                NSPasteboard.general.clearContents()
                NSPasteboard.general.writeObjects([local as NSURL])
                model.status = "\(file.name)을 Mac 클립보드에 복사했습니다. Finder에서 ⌘V로 붙여넣으세요."
            }
        }
    }

    func downloadRemoteFile(_ file: RemoteFile) {
        let destination: URL
        if file.isDirectory {
            let panel = NSOpenPanel()
            panel.canChooseFiles = false; panel.canChooseDirectories = true; panel.canCreateDirectories = true
            panel.prompt = "이 위치로 폴더 저장"
            guard panel.runModal() == .OK, let folder = panel.url else { return }
            destination = folder.appendingPathComponent(file.name, isDirectory: true)
        } else {
            let panel = NSSavePanel()
            panel.nameFieldStringValue = file.name
            guard panel.runModal() == .OK, let url = panel.url else { return }
            destination = url
        }
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            try ADBService(executable: adbPath).pull(serial: serial, remotePath: file.path, localURL: destination)
            return { model in model.status = "Galaxy 파일을 Mac에 저장했습니다." }
        }
    }

    func downloadRemoteFiles(_ files: [RemoteFile]) {
        guard !files.isEmpty else { return }
        let panel = NSOpenPanel()
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.canCreateDirectories = true
        panel.allowsMultipleSelection = false
        panel.prompt = "다운로드"
        panel.message = "선택한 \(files.count)개 항목을 저장할 폴더를 선택하세요."
        guard panel.runModal() == .OK, let folder = panel.url else { return }
        let serial = selectedSerial
        isTransferring = true
        transferStatus = "다운로드 준비 중"
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            var completed = 0
            for file in files.sorted(by: { $0.name.localizedStandardCompare($1.name) == .orderedAscending }) {
                let destination = Self.availableDestination(in: folder, name: file.name, isDirectory: file.isDirectory)
                try adb.pull(serial: serial, remotePath: file.path, localURL: destination)
                completed += 1
                let progress = completed
                Task { @MainActor in self.transferStatus = "\(file.name) · \(progress)/\(files.count)" }
            }
            return { model in model.isTransferring = false; model.transferStatus = "다운로드 완료"; model.status = "Galaxy에서 \(completed)개 항목을 다운로드했습니다." }
        }
    }

    func remoteFileProvider(_ file: RemoteFile) -> NSItemProvider {
        let provider = NSItemProvider()
        let serial = selectedSerial, settings = appSettings
        provider.suggestedName = file.name
        let type = file.isDirectory ? UTType.folder.identifier : UTType.data.identifier
        provider.registerFileRepresentation(forTypeIdentifier: type, fileOptions: [], visibility: .all) { completion in
            let progress = Progress(totalUnitCount: 1)
            Task.detached {
                do {
                    guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
                    let folder = FileManager.default.temporaryDirectory.appendingPathComponent("FlowBridge-Drag-\(UUID().uuidString)", isDirectory: true)
                    try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
                    let local = folder.appendingPathComponent(file.name)
                    try ADBService(executable: adbPath).pull(serial: serial, remotePath: file.path, localURL: local)
                    progress.completedUnitCount = 1
                    completion(local, false, nil)
                } catch { completion(nil, false, error) }
            }
            return progress
        }
        return provider
    }

    func cancelTransfer() { transferQueue.cancel(); status = "전송 취소를 요청했습니다." }

    func saveLog() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]
        panel.nameFieldStringValue = "Flow-Bridge-Session.log"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do { try logLines.joined(separator: "\n").write(to: url, atomically: true, encoding: .utf8); status = "로그를 저장했습니다." }
        catch { status = error.localizedDescription }
    }

    func installCompanion() {
        companionAction({ adb, serial, apk in
            _ = try adb.install(serial: serial, apk: apk)
            try Self.verifyInstalledCompanion(adb: adb, serial: serial)
        }, success: "화면 복구 도구를 설치했습니다.")
    }

    func uninstallCompanion() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            _ = try ADBService(executable: adbPath).uninstall(serial: serial, package: "io.github.mazemei.dxdisplaycleanup")
            return { model in model.status = "화면 복구 도구를 삭제했습니다." }
        }
    }

    func grantCompanionPermission() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            try Self.verifyInstalledCompanion(adb: adb, serial: serial)
            try adb.grant(serial: serial, package: "io.github.mazemei.dxdisplaycleanup", permission: "android.permission.WRITE_SECURE_SETTINGS")
            try Self.verifyInstalledCompanion(adb: adb, serial: serial)
            return { model in model.status = "화면 복구 도구에 복구 권한을 부여했습니다." }
        }
    }

    private func companionAction(_ action: @escaping @Sendable (ADBService, String, URL) throws -> Void, success: String) {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let apk = Bundle.main.resourceURL?.appendingPathComponent("companion/DX-Companion.apk"),
                  let data = try? Data(contentsOf: apk) else { throw DXError.commandFailed("번들 화면 복구 도구 APK가 없습니다.") }
            let digest = SHA256.hash(data: data).map { String(format: "%02X", $0) }.joined()
            guard digest == "7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F" else {
                throw DXError.commandFailed("화면 복구 도구 APK 해시 검증에 실패했습니다.")
            }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            try action(ADBService(executable: adbPath), serial, apk)
            return { model in model.status = success }
        }
    }

    nonisolated private static func verifyInstalledCompanion(adb: ADBService, serial: String) throws {
        let output = try adb.packagePath(serial: serial, package: "io.github.mazemei.dxdisplaycleanup")
        guard output.hasPrefix("package:") else { throw DXError.commandFailed("공식 화면 복구 도구 설치를 확인하지 못했습니다.") }
        let remote = String(output.dropFirst(8))
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("FlowBridge-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: folder) }
        let local = folder.appendingPathComponent("base.apk")
        try adb.pull(serial: serial, remotePath: remote, localURL: local)
        let data = try Data(contentsOf: local)
        let digest = SHA256.hash(data: data).map { String(format: "%02X", $0) }.joined()
        guard digest == "7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F" else {
            throw DXError.commandFailed("설치된 화면 복구 도구가 공식 APK와 일치하지 않습니다. 권한을 부여하지 않았습니다.")
        }
    }

    func stop() {
        let serial = selectedSerial
        captureWindowPlacements()
        sessionAttempt = UUID()
        controller?.stop(serial: serial)
        let restoreSerials = Set(appSettings.pendingBrightnessRestores.keys).union(brightnessSessionSerial.isEmpty ? [] : [brightnessSessionSerial])
        for restoreSerial in restoreSerials { restoreBrightnessIfNeeded(serial: restoreSerial) }
        hasActiveSession = false
        sessionPhase = .idle
        phoneNeedsUnlock = false
        protectedScreenDetected = false
        activeScreenMode = ""
        status = serial.isEmpty ? "선택된 기기가 없습니다." : "세션과 데스크톱 가상 디스플레이를 정리했습니다."
    }

    func quit() {
        captureWindowPlacements()
        miniBars.closeAll()
        if !selectedSerial.isEmpty { controller?.stop(serial: selectedSerial) }
        let restoreSerial = brightnessSessionSerial.isEmpty ? selectedSerial : brightnessSessionSerial
        if let state = appSettings.pendingBrightnessRestores[restoreSerial] ?? brightnessBeforeSession,
           let adbPath = ToolLocator.adb(appSettings.adbPath) {
            try? ADBService(executable: adbPath).restoreScreenBrightness(serial: restoreSerial, state: state)
            try? ADBService(executable: adbPath).restoreExtraDim(serial: restoreSerial, state: state)
            brightnessBeforeSession = nil
            brightnessSessionSerial = ""
            appSettings.pendingBrightnessRestores.removeValue(forKey: restoreSerial)
        }
        controller?.stopAll()
        if let data = try? JSONEncoder().encode(appSettings) { try? data.write(to: Self.settingsURL, options: .atomic) }
        NSApplication.shared.terminate(nil)
    }

    func showMainWindow() {
        if presenceMode == .menuBarOnly { NSApplication.shared.setActivationPolicy(.accessory) }
        NSApplication.shared.activate(ignoringOtherApps: true)
        NSApplication.shared.windows.first(where: { !($0 is NSPanel) })?.makeKeyAndOrderFront(nil)
        lastActivity = Date()
    }

    var showsMenuBarIcon: Bool { presenceMode != .dockOnly }

    func configureLaunchPresentation() {
        guard !didConfigureLaunchPresentation else { return }
        didConfigureLaunchPresentation = true
        applyPresenceMode()
        if !openMainWindowAtLaunch {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
                NSApplication.shared.windows.filter { !($0 is NSPanel) }.forEach { $0.orderOut(nil) }
            }
        }
    }

    func presentationSettingsChanged() {
        appSettings.presenceMode = presenceMode
        appSettings.openMainWindowAtLaunch = openMainWindowAtLaunch
        save()
        applyPresenceMode()
        status = presenceMode == .menuBarOnly ? "메뉴 막대 중심 모드로 전환했습니다. Dock 아이콘은 숨겨집니다." : presenceMode == .dockOnly ? "Dock 중심 모드로 전환했습니다. 메뉴 막대 아이콘은 숨겨집니다." : "Dock과 메뉴 막대에 모두 표시합니다."
    }

    private func applyPresenceMode() {
        NSApplication.shared.setActivationPolicy(presenceMode == .menuBarOnly ? .accessory : .regular)
    }

    func toggleKeyboardCorrection() {
        if keyboardCorrectionEnabled {
            keyboardCorrection.stop(); keyboardCorrectionEnabled = false
            status = "키보드 보정을 껐습니다."
        } else {
            keyboardCorrectionEnabled = keyboardCorrection.start(prompt: true)
            status = keyboardCorrectionEnabled ? "scrcpy 오른쪽 Shift 보정을 켰습니다." : "손쉬운 사용 권한이 필요합니다. 시스템 설정에서 Flow Bridge를 허용해 주세요."
        }
    }

    func toggleShiftEnter() {
        shiftEnterMode.toggle()
        keyboardCorrection.setShiftEnter(shiftEnterMode)
        status = shiftEnterMode ? "Enter를 Shift+Enter로 전송합니다." : "Enter를 일반 Enter로 전송합니다."
    }

    func toggleLaunchAtLogin() {
        do {
            if launchAtLogin { try SMAppService.mainApp.unregister(); launchAtLogin = false }
            else { try SMAppService.mainApp.register(); launchAtLogin = true }
            status = launchAtLogin ? "로그인 자동 실행을 켰습니다." : "로그인 자동 실행을 껐습니다."
        } catch { status = "로그인 자동 실행 변경 실패: \(error.localizedDescription)" }
    }

    func checkForUpdates() {
        updateStatus = "업데이트를 확인하는 중입니다."
        Task {
            do {
                var request = URLRequest(url: URL(string: "https://api.github.com/repos/catgarret/FlowBridge/releases/latest")!)
                request.setValue("FlowBridge-macOS", forHTTPHeaderField: "User-Agent")
                let (data, response) = try await URLSession.shared.data(for: request)
                guard let http = response as? HTTPURLResponse else { throw DXError.commandFailed("GitHub 응답을 확인하지 못했습니다.") }
                if http.statusCode == 404 {
                    updateStatus = "아직 공개된 GitHub 릴리스가 없습니다."
                    return
                }
                guard (200..<300).contains(http.statusCode) else { throw DXError.commandFailed("GitHub 업데이트 확인 실패 (HTTP \(http.statusCode))") }
                let release = try JSONDecoder().decode(GitHubRelease.self, from: data)
                let latest = release.tagName.trimmingCharacters(in: CharacterSet(charactersIn: "vV"))
                latestVersion = latest
                let current = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.1.0"
                isUpdateAvailable = latest.compare(current, options: .numeric) == .orderedDescending
                updateStatus = isUpdateAvailable ? "새 버전 \(latest)을 사용할 수 있습니다." : "최신 버전 \(current)을 사용 중입니다."
            } catch { updateStatus = "업데이트 확인 실패: \(error.localizedDescription)" }
        }
    }

    private func syncMiniBars() {
        guard let controller else { miniBars.closeAll(); return }
        let sessions = controller.sessions()
        if Date().timeIntervalSince(lastWindowPlacementCapture) >= 1 {
            updateWindowPlacements(from: sessions)
            lastWindowPlacementCapture = Date()
        }
        if sessionPhase == .running && !sessions.contains(where: { $0.id.hasPrefix("dex:") || $0.id.hasPrefix("phone:") }) {
            save()
            restoreBrightnessIfNeeded(serial: selectedSerial)
            sessionPhase = .idle
            hasActiveSession = false
            phoneNeedsUnlock = false
            protectedScreenDetected = false
            activeScreenMode = ""
            status = "화면 창이 종료되어 세션을 정리했습니다."
        }
        keyboardCorrection.setTargets(Set(sessions.map(\.processID)))
        miniBars.sync(sessions: sessions, protectedScreen: protectedScreenDetected, capture: { [weak self] windowID, title in
            self?.captureScrcpyWindow(windowID: windowID, title: title)
        }, initialVolume: mediaVolume, setVolume: { [weak self] level in
            self?.setMediaVolume(level)
        }, back: { [weak self] in
            self?.sendKeyEvent(4, label: "뒤로")
        }, home: { [weak self] in
            self?.sendKeyEvent(3, label: "홈")
        }, recents: { [weak self] in
            self?.sendKeyEvent(187, label: "최근 앱")
        }, power: { [weak self] in
            self?.sendKeyEvent(26, label: "전원")
        }, stop: { [weak self] in
            self?.stop()
        })
    }

    func save() {
        appSettings.display = settings
        appSettings.autoHideMinutes = max(0, autoHideMinutes)
        appSettings.automaticReconnect = automaticReconnect
        appSettings.phoneNotificationsEnabled = phoneNotificationsEnabled
        appSettings.messageNotificationsEnabled = messageNotificationsEnabled
        appSettings.appNotificationsEnabled = appNotificationsEnabled
        appSettings.blockedNotificationPackages = blockedNotificationPackages
        appSettings.turnPhoneScreenOffOnStart = dimPhoneOnStart
        appSettings.favoritePackages = packageNames
        appSettings.presenceMode = presenceMode
        appSettings.openMainWindowAtLaunch = openMainWindowAtLaunch
        appSettings.appLaunchMode = appLaunchMode
        appSettings.controlBarPosition = controlBarPosition
        if !selectedSerial.isEmpty { appSettings.deviceDisplays[selectedSerial] = settings }
        guard let data = try? JSONEncoder().encode(appSettings) else { return }
        try? data.write(to: Self.settingsURL, options: .atomic)
        status = "설정을 저장했습니다."
    }

    func brightnessSettingChanged() {
        appSettings.turnPhoneScreenOffOnStart = dimPhoneOnStart
        if !dimPhoneOnStart { restoreBrightnessIfNeeded(serial: selectedSerial) }
        save()
    }

    private func start(exclusiveMainDisplay: Bool = false, trackMainSession: Bool = false, createsOverlay: Bool = false, _ action: @escaping @Sendable (DXSessionController, String, DisplaySettings) throws -> Void) {
        if trackMainSession && sessionPhase == .launching { return }
        let serial = selectedSerial
        let display = settings
        let activeController = controller
        let dimPhone = dimPhoneOnStart
        let attempt = UUID()
        if trackMainSession {
            sessionAttempt = attempt
            sessionPhase = .launching
            hasActiveSession = false
            phoneNeedsUnlock = false
            status = "휴대폰을 깨우고 화면 연결을 준비하는 중입니다."
        }
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            guard let scrcpyPath = ToolLocator.scrcpy(settings.scrcpyPath) else { throw DXError.toolMissing("scrcpy") }
            let adb = ADBService(executable: adbPath)
            let controller = activeController ?? DXSessionController(adb: ADBService(executable: adbPath), scrcpy: scrcpyPath)
            try? adb.keyEvent(serial: serial, code: 224)
            Thread.sleep(forTimeInterval: 0.4)
            let screenState = try adb.phoneScreenState(serial: serial)
            let currentVolume = (try? adb.mediaVolume(serial: serial)) ?? 8
            if exclusiveMainDisplay { controller.stopMainDisplays(serial: serial) }
            let existingPIDs = Set(controller.sessions().filter { $0.serial == serial }.map { Int($0.processID) })
            try action(controller, serial, display)
            guard Self.waitForVisibleWindow(controller: controller, serial: serial, excluding: existingPIDs) else {
                controller.stopMainDisplays(serial: serial)
                throw DXError.commandFailed("영상 창을 열지 못했습니다. Galaxy 잠금을 해제한 뒤 다시 시도해 주세요.")
            }
            let capturedBrightness = (try? adb.screenBrightness(serial: serial)) ?? 128
            let extraDim = (try? adb.extraDimState(serial: serial)) ?? (activated: nil, level: nil)
            let previousBrightness = dimPhone && trackMainSession ? ScreenBrightnessState(value: capturedBrightness <= 0 ? 128 : capturedBrightness, mode: (try? adb.screenBrightnessMode(serial: serial)) ?? 0, extraDimActivated: extraDim.activated, extraDimLevel: extraDim.level) : nil
            if dimPhone && trackMainSession {
                try? adb.setScreenBrightness(serial: serial, value: 0)
                try? adb.setExtraDim(serial: serial, level: 100)
            }
            return { model in
                if trackMainSession && model.sessionAttempt != attempt {
                    controller.stopMainDisplays(serial: serial)
                    if let previousBrightness { Task.detached { try? adb.restoreScreenBrightness(serial: serial, state: previousBrightness); try? adb.restoreExtraDim(serial: serial, state: previousBrightness) } }
                    return
                }
                model.controller = controller
                model.mediaVolume = currentVolume
                if trackMainSession {
                    model.brightnessBeforeSession = previousBrightness
                    model.brightnessSessionSerial = previousBrightness == nil ? "" : serial
                    if let previousBrightness { model.appSettings.pendingBrightnessRestores[serial] = previousBrightness; model.save() }
                    model.hasActiveSession = true
                    model.sessionPhase = .running
                    model.phoneNeedsUnlock = screenState.isLocked
                }
                if createsOverlay { model.appSettings.managedOverlaySerials.insert(serial); model.save() }
                model.status = screenState.isLocked ? "화면은 열렸습니다. 보호된 내용은 Galaxy 잠금을 해제해야 표시됩니다." : (dimPhone ? "화면을 열고 Galaxy 화면을 최대로 어둡게 했습니다." : "화면이 준비되었습니다.")
            }
        }
    }

    nonisolated private static func waitForVisibleWindow(controller: DXSessionController, serial: String, excluding existingPIDs: Set<Int>) -> Bool {
        for _ in 0..<40 {
            let pids = Set(controller.sessions().filter { $0.serial == serial }.map { Int($0.processID) }).subtracting(existingPIDs)
            if !pids.isEmpty,
               let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]],
               windows.contains(where: { window in
                   guard let pid = window[kCGWindowOwnerPID as String] as? Int, pids.contains(pid),
                         let bounds = window[kCGWindowBounds as String] as? [String: Any],
                         let width = bounds["Width"] as? Double, let height = bounds["Height"] as? Double else { return false }
                   return width > 100 && height > 100
               }) { return true }
            Thread.sleep(forTimeInterval: 0.25)
        }
        return false
    }

    private func restoreBrightnessIfNeeded(serial: String) {
        guard !serial.isEmpty,
              let adbPath = ToolLocator.adb(appSettings.adbPath) else { return }
        guard let savedState = appSettings.pendingBrightnessRestores[serial] ?? (brightnessSessionSerial == serial ? brightnessBeforeSession : nil) else { return }
        let state = ScreenBrightnessState(value: savedState.value <= 0 ? 128 : savedState.value, mode: savedState.mode, extraDimActivated: savedState.extraDimActivated, extraDimLevel: savedState.extraDimLevel)
        if brightnessSessionSerial == serial { brightnessBeforeSession = nil; brightnessSessionSerial = "" }
        Task.detached { [weak self] in
            do {
                let adb = ADBService(executable: adbPath)
                try adb.restoreScreenBrightness(serial: serial, state: state)
                try adb.restoreExtraDim(serial: serial, state: state)
                await MainActor.run { self?.appSettings.pendingBrightnessRestores.removeValue(forKey: serial); self?.save() }
            } catch { }
        }
    }

    private func cleanupStaleOverlayIfNeeded(serial: String) {
        guard (appSettings.managedOverlaySerials.contains(serial) || !appSettings.didCleanLegacyOverlay),
              !overlayCleanupInProgress.contains(serial), sessionPhase == .idle,
              let adbPath = ToolLocator.adb(appSettings.adbPath) else { return }
        overlayCleanupInProgress.insert(serial)
        Task.detached { [weak self] in
            do {
                _ = try ADBService(executable: adbPath).shell(serial: serial, ["settings", "delete", "global", "overlay_display_devices"])
                await MainActor.run {
                    guard let self else { return }
                    self.overlayCleanupInProgress.remove(serial)
                    self.appSettings.managedOverlaySerials.remove(serial)
                    self.appSettings.didCleanLegacyOverlay = true
                    self.save()
                    self.status = "남아 있던 DEX 오버레이를 정리했습니다."
                }
            } catch { await MainActor.run { if let self { self.overlayCleanupInProgress.remove(serial) } } }
        }
    }

    private func perform(showBusy: Bool = true, _ work: @escaping @Sendable () throws -> (@MainActor (AppModel) -> Void)) {
        if showBusy { isBusy = true }
        Task.detached {
            do {
                let update = try work()
                await MainActor.run { update(self); self.appendLog(self.status); if showBusy { self.isBusy = false } }
            } catch {
                await MainActor.run { self.status = error.localizedDescription; if self.sessionPhase == .launching { self.sessionPhase = .failed }; self.appendLog("ERROR: \(error.localizedDescription)"); if showBusy { self.isBusy = false }; self.isTransferring = false }
            }
        }
    }

    private func appendLog(_ message: String) {
        let formatter = ISO8601DateFormatter()
        logLines.append("\(formatter.string(from: Date())) \(message)")
        if logLines.count > 2000 { logLines.removeFirst(logLines.count - 2000) }
    }

    private func applyAutoHide() {
        guard autoHideMinutes > 0,
              Date().timeIntervalSince(lastActivity) >= Double(autoHideMinutes * 60),
              NSApplication.shared.isActive else { return }
        NSApplication.shared.hide(nil)
        appendLog("자동 숨김")
        lastActivity = Date()
    }

    private func pollPhoneNotifications() {
        guard !notificationPollInProgress, !selectedSerial.isEmpty,
              phoneNotificationsEnabled || messageNotificationsEnabled || appNotificationsEnabled else { return }
        notificationPollInProgress = true
        let serial = selectedSerial
        let isNewBaseline = notificationBaselineSerial != serial
        let options = (phoneNotificationsEnabled, messageNotificationsEnabled, appNotificationsEnabled)
        let settings = appSettings
        Task.detached { [weak self] in
            do {
                guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
                let notifications = try ADBService(executable: adbPath).notifications(serial: serial)
                await MainActor.run {
                    guard let self else { return }
                    defer { self.notificationPollInProgress = false }
                    let current = Set(notifications.map(\.fingerprint))
                    self.activeNotifications = notifications
                    if isNewBaseline {
                        self.notificationBaselineSerial = serial
                        self.seenNotificationFingerprints = current
                        return
                    }
                    for item in notifications where !self.seenNotificationFingerprints.contains(item.fingerprint) {
                        let enabled = item.kind == .call ? options.0 : item.kind == .message ? options.1 : options.2
                        if enabled && !self.blockedNotificationPackages.contains(item.package) { self.deliver(item) }
                    }
                    self.seenNotificationFingerprints = current
                }
            } catch {
                await MainActor.run { self?.notificationPollInProgress = false; self?.notificationDeliveryStatus = "Galaxy 알림 확인 실패: \(error.localizedDescription)" }
            }
        }
    }

    private func pollProtectedScreen() {
        guard hasActiveSession, !protectedScreenPollInProgress, !selectedSerial.isEmpty else { if !hasActiveSession { protectedScreenDetected = false }; return }
        protectedScreenPollInProgress = true
        let serial = selectedSerial, settings = appSettings
        Task.detached { [weak self] in
            let detected: Bool
            if let adbPath = ToolLocator.adb(settings.adbPath) { detected = (try? ADBService(executable: adbPath).isProtectedScreenFocused(serial: serial)) ?? false } else { detected = false }
            await MainActor.run { self?.protectedScreenDetected = detected; self?.protectedScreenPollInProgress = false }
        }
    }

    private func deliver(_ item: PhoneNotification) {
        let content = UNMutableNotificationContent()
        let appName = notificationAppName(for: item)
        content.title = appName
        let sourceTitle = item.title.trimmingCharacters(in: .whitespacesAndNewlines)
        content.subtitle = sourceTitle.isEmpty || sourceTitle == appName
            ? (item.kind == .call ? "Galaxy 전화" : item.kind == .message ? "Galaxy 문자" : "Galaxy 알림")
            : sourceTitle
        content.body = item.body
        content.sound = .default
        content.threadIdentifier = item.package.isEmpty ? "flowbridge.test" : "galaxy.\(item.package)"
        if let iconURL = appIconURLs[item.package], let attachmentURL = notificationAttachmentURL(package: item.package, iconURL: iconURL),
           let attachment = try? UNNotificationAttachment(identifier: "galaxy-app-icon", url: attachmentURL) {
            content.attachments = [attachment]
        } else if !item.package.isEmpty {
            requestAppIcon(package: item.package)
        }
        let deliveredTitle = content.title
        let request = UNNotificationRequest(identifier: "flowbridge.\(UUID().uuidString)", content: content, trigger: nil)
        UNUserNotificationCenter.current().add(request) { [weak self] error in
            Task { @MainActor in self?.notificationDeliveryStatus = error.map { "macOS 알림 전달 실패: \($0.localizedDescription)" } ?? "최근 전달: \(deliveredTitle)" }
        }
    }

    private func notificationAppName(for item: PhoneNotification) -> String {
        if item.package.isEmpty { return "Flow Bridge" }
        return appDisplayName(package: item.package)
    }

    private func notificationAttachmentURL(package: String, iconURL: URL) -> URL? {
        guard let image = NSImage(contentsOf: iconURL), let tiff = image.tiffRepresentation,
              let bitmap = NSBitmapImageRep(data: tiff), let png = bitmap.representation(using: .png, properties: [:]) else { return nil }
        let directory = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("FlowBridge/NotificationIcons", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let digest = SHA256.hash(data: Data(package.utf8)).map { String(format: "%02x", $0) }.joined()
        let output = directory.appendingPathComponent("\(digest).png")
        if !FileManager.default.fileExists(atPath: output.path) { try? png.write(to: output, options: .atomic) }
        return FileManager.default.fileExists(atPath: output.path) ? output : nil
    }

    func requestAppIcon(package: String) {
        guard !package.isEmpty, appIconURLs[package] == nil, !iconLoadsInProgress.contains(package), !selectedSerial.isEmpty,
              let adbPath = ToolLocator.adb(appSettings.adbPath) else { return }
        iconLoadsInProgress.insert(package)
        let serial = selectedSerial
        Task.detached { [weak self] in
            let cache = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0].appendingPathComponent("FlowBridge/AppIcons", isDirectory: true)
            try? FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
            let digest = SHA256.hash(data: Data("\(serial)|\(package)".utf8)).map { String(format: "%02x", $0) }.joined()
            let output = cache.appendingPathComponent("\(digest).icon")
            if !FileManager.default.fileExists(atPath: output.path) {
                let apk = FileManager.default.temporaryDirectory.appendingPathComponent("flowbridge-\(UUID().uuidString).apk")
                defer { try? FileManager.default.removeItem(at: apk) }
                do {
                    let adb = ADBService(executable: adbPath)
                    let remote = try adb.packagePath(serial: serial, package: package).split(whereSeparator: \.isNewline).first.map(String.init)?.replacingOccurrences(of: "package:", with: "") ?? ""
                    guard !remote.isEmpty else { throw DXError.commandFailed("앱 경로 없음") }
                    try adb.pull(serial: serial, remotePath: remote, localURL: apk)
                    let names = String(data: try Self.runData("/usr/bin/unzip", ["-Z1", apk.path]), encoding: .utf8) ?? ""
                    let candidates = names.split(whereSeparator: \.isNewline).map(String.init).filter { path in let lower = path.lowercased(); return (lower.hasSuffix(".png") || lower.hasSuffix(".webp")) && (lower.contains("launcher") || lower.contains("app_icon") || lower.hasSuffix("/icon.png")) }
                    guard let iconPath = candidates.last else { throw DXError.commandFailed("앱 아이콘 없음") }
                    let data = try Self.runData("/usr/bin/unzip", ["-p", apk.path, iconPath])
                    try data.write(to: output, options: .atomic)
                } catch { }
            }
            await MainActor.run { if FileManager.default.fileExists(atPath: output.path) { self?.appIconURLs[package] = output }; self?.iconLoadsInProgress.remove(package) }
        }
    }

    nonisolated private static func runData(_ executable: String, _ arguments: [String]) throws -> Data {
        let process = Process(); let pipe = Pipe(); process.executableURL = URL(fileURLWithPath: executable); process.arguments = arguments; process.standardOutput = pipe; process.standardError = FileHandle.nullDevice; try process.run(); let data = pipe.fileHandleForReading.readDataToEndOfFile(); process.waitUntilExit(); guard process.terminationStatus == 0 else { throw DXError.commandFailed("아이콘 추출 실패") }; return data
    }

    private func load() {
        if let data = try? Data(contentsOf: Self.settingsURL), let decoded = try? JSONDecoder().decode(AppSettings.self, from: data) {
            appSettings = decoded; settings = decoded.display
        }
    }

    private func savedPlacement(kind: String) -> WindowPlacement? {
        guard let key = selectedDeviceKey else { return nil }
        return appSettings.windowPlacements["\(kind):\(key)"]
    }

    private func updateWindowPlacements(from sessions: [DXSessionController.SessionInfo]) {
        guard let deviceKey = selectedDeviceKey,
              let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else { return }
        for session in sessions where session.id.hasPrefix("dex:") || session.id.hasPrefix("phone:") {
            guard let item = windows.first(where: { ($0[kCGWindowOwnerPID as String] as? Int32) == session.processID }),
                  let dictionary = item[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: dictionary), bounds.width > 100, bounds.height > 100 else { continue }
            let kind = session.id.hasPrefix("dex:") ? "desktop" : "phone"
            appSettings.windowPlacements["\(kind):\(deviceKey)"] = WindowPlacement(x: Int(bounds.minX), y: Int(bounds.minY), width: Int(bounds.width), height: Int(bounds.height))
        }
    }

    private func captureWindowPlacements() {
        if let controller { updateWindowPlacements(from: controller.sessions()); save() }
    }

    private var selectedDeviceKey: String? {
        devices.first(where: { $0.serial == selectedSerial }).map(deviceIdentityKey)
    }

    private var selectedScreenDeviceName: String {
        devices.first(where: { $0.serial == selectedSerial }).map(deviceLabel) ?? "Galaxy"
    }

    private func deviceIdentityKey(_ device: Device) -> String {
        device.physicalSerial.isEmpty ? "transport:\(device.serial)" : "physical:\(device.physicalSerial)"
    }

    private static var settingsURL: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let base = support.appendingPathComponent("FlowBridge", isDirectory: true)
        try? FileManager.default.createDirectory(at: base, withIntermediateDirectories: true)
        let current = base.appendingPathComponent("settings.json")
        let legacy = support.appendingPathComponent("DXManagerMac/settings.json")
        if !FileManager.default.fileExists(atPath: current.path), FileManager.default.fileExists(atPath: legacy.path) {
            try? FileManager.default.copyItem(at: legacy, to: current)
        }
        return current
    }

    nonisolated private static var clipboardDirectory: URL {
        FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0].appendingPathComponent("FlowBridge/Clipboard", isDirectory: true)
    }

    nonisolated private static func availableDestination(in folder: URL, name: String, isDirectory: Bool) -> URL {
        let manager = FileManager.default
        let original = folder.appendingPathComponent(name, isDirectory: isDirectory)
        guard manager.fileExists(atPath: original.path) else { return original }
        let source = URL(fileURLWithPath: name)
        let ext = isDirectory ? "" : source.pathExtension
        let stem = isDirectory || ext.isEmpty ? name : source.deletingPathExtension().lastPathComponent
        for index in 2...999 {
            let candidateName = ext.isEmpty ? "\(stem) \(index)" : "\(stem) \(index).\(ext)"
            let candidate = folder.appendingPathComponent(candidateName, isDirectory: isDirectory)
            if !manager.fileExists(atPath: candidate.path) { return candidate }
        }
        return folder.appendingPathComponent("\(UUID().uuidString)-\(name)", isDirectory: isDirectory)
    }
}

private struct GitHubRelease: Decodable {
    let tagName: String
    private enum CodingKeys: String, CodingKey { case tagName = "tag_name" }
}
