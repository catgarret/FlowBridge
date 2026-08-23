import Foundation

public final class DXSessionController: @unchecked Sendable {
    public struct SessionInfo: Sendable, Identifiable {
        public let id: String
        public let processID: Int32
        public let title: String
        public let serial: String
    }

    private struct SessionRecord {
        let process: Process
        let title: String
        let serial: String
    }
    private let adb: ADBService
    private let scrcpy: String
    private let lock = NSLock()
    private var processes: [String: SessionRecord] = [:]

    public init(adb: ADBService, scrcpy: String) {
        self.adb = adb
        self.scrcpy = scrcpy
    }

    public func startDeX(serial: String, settings: DisplaySettings, placement: WindowPlacement? = nil) throws {
        guard settings.isValid else { throw DXError.invalidSettings }
        // A previous unclean shutdown can leave Android's single global overlay
        // setting behind. Clear it first so the before/after ID comparison stays
        // deterministic and never guesses an existing display ID.
        _ = try adb.shell(serial: serial, ["settings", "delete", "global", "overlay_display_devices"])
        Thread.sleep(forTimeInterval: 0.35)
        let before = DisplayParser.ids(from: try adb.shell(serial: serial, ["dumpsys", "display"]))
        _ = try adb.shell(serial: serial, ["settings", "put", "global", "overlay_display_devices", settings.overlayValue])
        var displayID: Int?
        for _ in 0..<20 {
            Thread.sleep(forTimeInterval: 0.25)
            let after = DisplayParser.ids(from: try adb.shell(serial: serial, ["dumpsys", "display"]))
            let created = after.subtracting(before)
            if created.count == 1 { displayID = created.first; break }
        }
        guard let displayID else {
            _ = try? adb.shell(serial: serial, ["settings", "delete", "global", "overlay_display_devices"])
            throw DXError.displayNotFound
        }
        let title = "Flow Bridge - Desktop - \(serial)"
        try launch(key: "dex:\(serial)", serial: serial, title: title, arguments: baseArguments(serial, settings) + (placement?.scrcpyArguments ?? []) + ["--display-id", String(displayID), "--window-title", title])
    }

    public func startApp(serial: String, package: String, settings: DisplaySettings, slot: Int = 1) throws {
        guard settings.isValid else { throw DXError.invalidSettings }
        let cleanPackage = package.trimmingCharacters(in: .whitespacesAndNewlines)
        guard cleanPackage.range(of: #"^[A-Za-z0-9_.]+$"#, options: .regularExpression) != nil else {
            throw DXError.commandFailed("Android 패키지명이 올바르지 않습니다.")
        }
        let title = "Flow Bridge - App \(slot) - \(cleanPackage) - \(serial)"
        try launch(key: "app:\(serial):\(slot)", serial: serial, title: title, arguments: baseArguments(serial, settings) + ["--new-display=\(settings.width)x\(settings.height)/\(settings.dpi)", "--start-app=\(cleanPackage)", "--window-title", title])
    }

    public func startPhoneMirror(serial: String, settings: DisplaySettings, placement: WindowPlacement? = nil) throws {
        guard settings.isValid else { throw DXError.invalidSettings }
        let title = "Flow Bridge - Phone Mirror - \(serial)"
        try launch(key: "phone:\(serial)", serial: serial, title: title,
                   arguments: baseArguments(serial, settings) + (placement?.scrcpyArguments ?? []) + ["--window-title", title])
    }

    public func startAppOnPhone(serial: String, package: String, settings: DisplaySettings, placement: WindowPlacement? = nil) throws {
        try adb.launchApp(serial: serial, package: package)
        try startPhoneMirror(serial: serial, settings: settings, placement: placement)
    }

    public func stop(serial: String) {
        lock.lock()
        let targets = processes.filter { $0.key.contains(serial) }
        targets.forEach { processes.removeValue(forKey: $0.key) }
        lock.unlock()
        targets.values.forEach { $0.process.terminate() }
        _ = try? adb.shell(serial: serial, ["settings", "delete", "global", "overlay_display_devices"])
    }

    public func stopAll() {
        lock.lock(); let running = processes.values; processes.removeAll(); lock.unlock()
        running.forEach { $0.process.terminate() }
    }

    public func sessions() -> [SessionInfo] {
        lock.lock(); defer { lock.unlock() }
        return processes.compactMap { key, record in
            guard record.process.isRunning else { return nil }
            return SessionInfo(id: key, processID: record.process.processIdentifier, title: record.title, serial: record.serial)
        }
    }

    public func stopMainDisplays(serial: String) {
        lock.lock()
        let targets = processes.filter { ($0.key.hasPrefix("dex:") || $0.key.hasPrefix("phone:")) && $0.value.serial == serial }
        targets.forEach { processes.removeValue(forKey: $0.key) }
        lock.unlock()
        targets.values.forEach { $0.process.terminate() }
        _ = try? adb.shell(serial: serial, ["settings", "delete", "global", "overlay_display_devices"])
    }

    private func baseArguments(_ serial: String, _ settings: DisplaySettings) -> [String] {
        ["-s", serial, "--video-bit-rate", "\(settings.bitrate)M", "--max-fps", String(settings.fps), "--shortcut-mod=lsuper"]
    }

    private func launch(key: String, serial: String, title: String, arguments: [String]) throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: scrcpy)
        process.arguments = arguments
        try process.run()
        lock.lock(); processes[key]?.process.terminate(); processes[key] = SessionRecord(process: process, title: title, serial: serial); lock.unlock()
    }
}
