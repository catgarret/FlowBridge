import Foundation
import AppKit
import CryptoKit
import ServiceManagement
import UserNotifications
import UniformTypeIdentifiers
import DXManagerCore

final class MacNotificationDelegate: NSObject, UNUserNotificationCenterDelegate {
    func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification, withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .sound])
    }
}

enum SessionPhase { case idle, launching, running, failed }

@MainActor
final class AppModel: ObservableObject {
    @Published var devices: [Device] = []
    @Published var selectedSerial = ""
    @Published var settings = DisplaySettings()
    @Published var packageNames = ["com.android.settings", "", ""]
    @Published var installedApps: [InstalledApp] = []
    @Published var appSearch = ""
    @Published var phoneNumber = ""
    @Published var messageBody = ""
    @Published var phoneSearch = ""
    @Published var contacts: [PhoneContact] = []
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
    @Published var status = "ADB 기기를 검색하는 중입니다."
    @Published var isBusy = false
    @Published var autoHideMinutes = 10
    @Published var keyboardCorrectionEnabled = false
    @Published var shiftEnterMode = false
    @Published var launchAtLogin = false
    @Published var automaticReconnect = true
    @Published var phoneNotificationsEnabled = false
    @Published var messageNotificationsEnabled = false
    @Published var appNotificationsEnabled = false
    @Published var turnPhoneScreenOffOnStart = false
    @Published var hasActiveSession = false
    @Published var sessionPhase: SessionPhase = .idle
    @Published var phoneNeedsUnlock = false
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
    private var brightnessBeforeSession: Int?
    private var sessionAttempt = UUID()

    init() {
        load()
        autoHideMinutes = appSettings.autoHideMinutes
        launchAtLogin = SMAppService.mainApp.status == .enabled
        wirelessEndpoint = appSettings.lastWirelessEndpoint
        automaticReconnect = appSettings.automaticReconnect
        phoneNotificationsEnabled = appSettings.phoneNotificationsEnabled
        messageNotificationsEnabled = appSettings.messageNotificationsEnabled
        appNotificationsEnabled = appSettings.appNotificationsEnabled
        turnPhoneScreenOffOnStart = appSettings.turnPhoneScreenOffOnStart
        packageNames = appSettings.favoritePackages
        UNUserNotificationCenter.current().delegate = notificationDelegate
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
                self.syncMiniBars()
                self.applyAutoHide()
                self.pollPhoneNotifications()
            }
        }
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

    func startDeX() { start(exclusiveMainDisplay: true, trackMainSession: true) { try $0.startDeX(serial: $1, settings: $2) } }
    func startPhoneMirror() { start(exclusiveMainDisplay: true, trackMainSession: true) { try $0.startPhoneMirror(serial: $1, settings: $2) } }

    func volumeDown() { sendKeyEvent(25, label: "볼륨 낮추기") }
    func volumeUp() { sendKeyEvent(24, label: "볼륨 높이기") }
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
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            try ADBService(executable: adbPath).composeMessage(serial: serial, number: number, body: body)
            return { model in model.status = "Galaxy 메시지 작성 화면을 열었습니다." }
        }
    }

    func selectPhoneNumber(_ number: String) { phoneNumber = number }

    func refreshPhoneData() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let adb = ADBService(executable: adbPath)
            let contacts = try adb.contacts(serial: serial)
            let calls = try adb.recentCalls(serial: serial)
            let messages = try adb.recentMessages(serial: serial)
            return { model in model.contacts = contacts; model.recentCalls = calls; model.recentMessages = messages; model.status = "주소록과 최근 전화·문자를 불러왔습니다." }
        }
    }

    func applyDeviceSettings() {
        if let saved = appSettings.deviceDisplays[selectedSerial] { settings = saved }
        notificationBaselineSerial = ""
        seenNotificationFingerprints.removeAll()
        if installedApps.isEmpty, !selectedSerial.isEmpty { loadPackages() }
    }

    func notificationSettingsChanged() {
        appSettings.phoneNotificationsEnabled = phoneNotificationsEnabled
        appSettings.messageNotificationsEnabled = messageNotificationsEnabled
        appSettings.appNotificationsEnabled = appNotificationsEnabled
        save()
        guard phoneNotificationsEnabled || messageNotificationsEnabled || appNotificationsEnabled else { return }
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound]) { [weak self] granted, error in
            Task { @MainActor in
                if let error { self?.status = "macOS 알림 권한 요청 실패: \(error.localizedDescription)" }
                else if !granted { self?.status = "macOS 시스템 설정에서 Flow Bridge 알림을 허용해 주세요." }
                else { self?.status = "휴대폰 알림 전달을 켰습니다." }
            }
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
        start { try $0.startApp(serial: $1, package: package, settings: $2, slot: slot + 1) }
    }

    func startApp(package: String) {
        start { try $0.startApp(serial: $1, package: package, settings: $2, slot: 0) }
    }

    func assignFavorite(package: String, slot: Int) {
        guard packageNames.indices.contains(slot) else { return }
        packageNames[slot] = package
        save()
        status = "즐겨찾기 \(slot + 1)에 저장했습니다."
    }

    func chooseAndTransfer() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseFiles = true
        panel.canChooseDirectories = true
        guard panel.runModal() == .OK else { return }
        transfer(urls: panel.urls)
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

    func pasteFilesFromClipboard() {
        let urls = NSPasteboard.general.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL] ?? []
        guard !urls.isEmpty else { status = "Mac 클립보드에 복사된 파일이 없습니다."; return }
        transfer(urls: urls)
    }

    func loadRemoteFiles() {
        let serial = selectedSerial
        perform { [settings = appSettings] in
            guard !serial.isEmpty else { throw DXError.commandFailed("기기를 선택해 주세요.") }
            guard let adbPath = ToolLocator.adb(settings.adbPath) else { throw DXError.toolMissing("adb") }
            let files = try ADBService(executable: adbPath).downloadFiles(serial: serial)
            return { model in model.remoteFiles = files; model.status = "Galaxy Download 파일 \(files.count)개를 불러왔습니다." }
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
        sessionAttempt = UUID()
        controller?.stop(serial: serial)
        restoreBrightnessIfNeeded(serial: serial)
        hasActiveSession = false
        sessionPhase = .idle
        phoneNeedsUnlock = false
        status = serial.isEmpty ? "선택된 기기가 없습니다." : "세션과 데스크톱 가상 디스플레이를 정리했습니다."
    }

    func quit() {
        miniBars.closeAll()
        if !selectedSerial.isEmpty { controller?.stop(serial: selectedSerial) }
        controller?.stopAll()
        NSApplication.shared.terminate(nil)
    }

    func showMainWindow() {
        NSApplication.shared.activate(ignoringOtherApps: true)
        NSApplication.shared.windows.first(where: { !($0 is NSPanel) })?.makeKeyAndOrderFront(nil)
        lastActivity = Date()
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
        if sessionPhase == .running && !sessions.contains(where: { $0.id.hasPrefix("dex:") || $0.id.hasPrefix("phone:") }) {
            restoreBrightnessIfNeeded(serial: selectedSerial)
            sessionPhase = .idle
            hasActiveSession = false
            phoneNeedsUnlock = false
            status = "화면 창이 종료되어 세션을 정리했습니다."
        }
        keyboardCorrection.setTargets(Set(sessions.map(\.processID)))
        miniBars.sync(sessions: sessions, capture: { [weak self] windowID, title in
            self?.captureScrcpyWindow(windowID: windowID, title: title)
        }, volumeDown: { [weak self] in
            self?.volumeDown()
        }, volumeUp: { [weak self] in
            self?.volumeUp()
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
        appSettings.turnPhoneScreenOffOnStart = turnPhoneScreenOffOnStart
        appSettings.favoritePackages = packageNames
        if !selectedSerial.isEmpty { appSettings.deviceDisplays[selectedSerial] = settings }
        guard let data = try? JSONEncoder().encode(appSettings) else { return }
        try? data.write(to: Self.settingsURL, options: .atomic)
        status = "설정을 저장했습니다."
    }

    private func start(exclusiveMainDisplay: Bool = false, trackMainSession: Bool = false, _ action: @escaping @Sendable (DXSessionController, String, DisplaySettings) throws -> Void) {
        if trackMainSession && sessionPhase == .launching { return }
        let serial = selectedSerial
        let display = settings
        let activeController = controller
        let dimPhone = turnPhoneScreenOffOnStart
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
            if exclusiveMainDisplay { controller.stopMainDisplays(serial: serial) }
            let existingPIDs = Set(controller.sessions().filter { $0.serial == serial }.map { Int($0.processID) })
            try action(controller, serial, display)
            guard Self.waitForVisibleWindow(controller: controller, serial: serial, excluding: existingPIDs) else {
                controller.stopMainDisplays(serial: serial)
                throw DXError.commandFailed("영상 창을 열지 못했습니다. Galaxy 잠금을 해제한 뒤 다시 시도해 주세요.")
            }
            let previousBrightness = dimPhone && trackMainSession ? (try? adb.screenBrightness(serial: serial)) : nil
            if dimPhone && trackMainSession { try? adb.setScreenBrightness(serial: serial, value: 0) }
            return { model in
                if trackMainSession && model.sessionAttempt != attempt {
                    controller.stopMainDisplays(serial: serial)
                    if let previousBrightness { Task.detached { try? adb.setScreenBrightness(serial: serial, value: previousBrightness) } }
                    return
                }
                model.controller = controller
                if trackMainSession {
                    model.brightnessBeforeSession = previousBrightness
                    model.hasActiveSession = true
                    model.sessionPhase = .running
                    model.phoneNeedsUnlock = screenState.isLocked
                }
                model.status = screenState.isLocked ? "화면은 열렸습니다. 보호된 내용은 Galaxy 잠금을 해제해야 표시됩니다." : (dimPhone ? "화면을 열고 Galaxy 밝기를 최저로 낮췄습니다." : "화면이 준비되었습니다.")
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
        guard let value = brightnessBeforeSession, !serial.isEmpty,
              let adbPath = ToolLocator.adb(appSettings.adbPath) else { return }
        brightnessBeforeSession = nil
        Task.detached { try? ADBService(executable: adbPath).setScreenBrightness(serial: serial, value: value) }
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
                    if isNewBaseline {
                        self.notificationBaselineSerial = serial
                        self.seenNotificationFingerprints = current
                        return
                    }
                    for item in notifications where !self.seenNotificationFingerprints.contains(item.fingerprint) {
                        let enabled = item.kind == .call ? options.0 : item.kind == .message ? options.1 : options.2
                        if enabled { self.deliver(item) }
                    }
                    self.seenNotificationFingerprints.formUnion(current)
                    if self.seenNotificationFingerprints.count > 4000 {
                        self.seenNotificationFingerprints = current
                    }
                }
            } catch {
                await MainActor.run { self?.notificationPollInProgress = false }
            }
        }
    }

    private func deliver(_ item: PhoneNotification) {
        let content = UNMutableNotificationContent()
        content.title = item.title.isEmpty ? item.package : item.title
        content.subtitle = item.kind == .call ? "Galaxy 전화" : item.kind == .message ? "Galaxy 문자" : item.package
        content.body = item.body
        content.sound = .default
        content.threadIdentifier = "galaxy.\(item.package)"
        let request = UNNotificationRequest(identifier: item.fingerprint, content: content, trigger: nil)
        UNUserNotificationCenter.current().add(request)
    }

    private func load() {
        if let data = try? Data(contentsOf: Self.settingsURL), let decoded = try? JSONDecoder().decode(AppSettings.self, from: data) {
            appSettings = decoded; settings = decoded.display
        }
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
}

private struct GitHubRelease: Decodable {
    let tagName: String
    private enum CodingKeys: String, CodingKey { case tagName = "tag_name" }
}
