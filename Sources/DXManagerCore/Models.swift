import Foundation

public struct Device: Identifiable, Hashable, Sendable {
    public let serial: String
    public let state: String
    public let model: String
    public let physicalSerial: String
    public let customName: String
    public var id: String { serial }
    public var isReady: Bool { state == "device" }
    public var displayName: String { customName.isEmpty ? (model.isEmpty ? serial : model.replacingOccurrences(of: "_", with: " ")) : customName }
    public var modelName: String { model.isEmpty ? serial : model.replacingOccurrences(of: "_", with: " ") }
    public var isWireless: Bool { serial.contains(":") || serial.contains("._adb-tls-") }
    public var connectionName: String { isWireless ? "Wi-Fi" : "USB" }

    public init(serial: String, state: String, model: String, physicalSerial: String = "", customName: String = "") {
        self.serial = serial; self.state = state; self.model = model; self.physicalSerial = physicalSerial; self.customName = customName
    }
}

public struct InstalledApp: Identifiable, Hashable, Sendable {
    public let name: String
    public let package: String
    public var id: String { package }
    public init(name: String, package: String) { self.name = name; self.package = package }
}

public struct ADBMDNSService: Hashable, Sendable {
    public let name: String
    public let type: String
    public let endpoint: String
    public var isPairing: Bool { type == "_adb-tls-pairing._tcp" }
    public var isConnect: Bool { type == "_adb-tls-connect._tcp" }
    public init(name: String, type: String, endpoint: String) { self.name = name; self.type = type; self.endpoint = endpoint }
}

public struct RemoteFile: Identifiable, Hashable, Sendable {
    public let name: String
    public let path: String
    public let isDirectory: Bool
    public var id: String { path }
    public init(name: String, path: String, isDirectory: Bool = false) { self.name = name; self.path = path; self.isDirectory = isDirectory }
}

public struct PhoneScreenState: Equatable, Sendable {
    public let isAwake: Bool
    public let isLocked: Bool
    public init(isAwake: Bool, isLocked: Bool) { self.isAwake = isAwake; self.isLocked = isLocked }
}

public struct ScreenBrightnessState: Codable, Equatable, Sendable {
    public let value: Int
    public let mode: Int
    public let extraDimActivated: Int?
    public let extraDimLevel: Int?
    public init(value: Int, mode: Int, extraDimActivated: Int? = nil, extraDimLevel: Int? = nil) {
        self.value = value; self.mode = mode; self.extraDimActivated = extraDimActivated; self.extraDimLevel = extraDimLevel
    }
}

public struct PhoneContact: Identifiable, Hashable, Sendable {
    public let name: String; public let number: String; public let photoURI: String
    public var id: String { "\(name)|\(number)" }
    public init(name: String, number: String, photoURI: String = "") { self.name = name; self.number = number; self.photoURI = photoURI }
}
public struct PhoneCall: Identifiable, Hashable, Sendable {
    public let number: String; public let type: Int; public let date: Date; public let duration: Int
    public var id: String { "\(number)|\(date.timeIntervalSince1970)|\(type)" }
    public init(number: String, type: Int, date: Date, duration: Int = 0) { self.number = number; self.type = type; self.date = date; self.duration = duration }
}
public struct PhoneMessage: Identifiable, Hashable, Sendable {
    public let address: String; public let body: String; public let date: Date; public let type: Int
    public var id: String { "\(address)|\(date.timeIntervalSince1970)|\(body.hashValue)" }
    public init(address: String, body: String, date: Date, type: Int = 1) { self.address = address; self.body = body; self.date = date; self.type = type }
    public var isOutgoing: Bool { type == 2 || type == 4 || type == 5 || type == 6 }
}

public struct DisplaySettings: Codable, Equatable, Sendable {
    public var width = 1920
    public var height = 1080
    public var dpi = 240
    public var bitrate = 16
    public var fps = 60

    public init(width: Int = 1920, height: Int = 1080, dpi: Int = 240, bitrate: Int = 16, fps: Int = 60) {
        self.width = width; self.height = height; self.dpi = dpi; self.bitrate = bitrate; self.fps = fps
    }

    public var overlayValue: String { "\(width)x\(height)/\(dpi)" }
    public var isValid: Bool {
        (320...4096).contains(width) && (320...4096).contains(height) &&
        (120...640).contains(dpi) && (1...100).contains(bitrate) && (1...240).contains(fps)
    }
}

public enum AppPresenceMode: String, Codable, CaseIterable, Sendable {
    case dockAndMenuBar, menuBarOnly, dockOnly
}

public enum AppLaunchMode: String, Codable, CaseIterable, Sendable { case desktopWindow, phoneScreen }
public enum ControlBarPosition: String, Codable, CaseIterable, Sendable { case top, bottom }

public struct WindowPlacement: Codable, Equatable, Sendable {
    public let x: Int; public let y: Int; public let width: Int; public let height: Int
    public init(x: Int, y: Int, width: Int, height: Int) { self.x = x; self.y = y; self.width = width; self.height = height }
    public var scrcpyArguments: [String] { ["--window-x", String(x), "--window-y", String(y), "--window-width", String(width), "--window-height", String(height)] }
}

public struct AppSettings: Codable, Sendable {
    public var display = DisplaySettings()
    public var scrcpyPath = ""
    public var adbPath = ""
    public var deviceDisplays: [String: DisplaySettings] = [:]
    public var appProfiles: [String: DisplaySettings] = [:]
    public var favoritePackages = ["com.android.settings", "", ""]
    public var deviceAliases: [String: String] = [:]
    public var deviceNativeDisplays: [String: DisplaySettings] = [:]
    public var presenceMode: AppPresenceMode = .dockAndMenuBar
    public var openMainWindowAtLaunch = true
    public var appLaunchMode: AppLaunchMode = .desktopWindow
    public var windowPlacements: [String: WindowPlacement] = [:]
    public var controlBarPosition: ControlBarPosition = .bottom
    public var autoHideMinutes = 10
    public var lastWirelessEndpoint = ""
    public var automaticReconnect = true
    public var phoneNotificationsEnabled = false
    public var messageNotificationsEnabled = false
    public var appNotificationsEnabled = false
    public var blockedNotificationPackages: Set<String> = []
    // Retain the serialized key for settings migration. It now controls Extra dim
    // while keeping the phone display powered; no physical screen-off command is used.
    public var turnPhoneScreenOffOnStart = false
    public var pendingBrightnessRestores: [String: ScreenBrightnessState] = [:]
    public var managedOverlaySerials: Set<String> = []
    public var didCleanLegacyOverlay = false
    public init() {}

    private enum CodingKeys: String, CodingKey { case display, scrcpyPath, adbPath, deviceDisplays, appProfiles, favoritePackages, deviceAliases, deviceNativeDisplays, presenceMode, openMainWindowAtLaunch, appLaunchMode, windowPlacements, controlBarPosition, autoHideMinutes, lastWirelessEndpoint, automaticReconnect, phoneNotificationsEnabled, messageNotificationsEnabled, appNotificationsEnabled, blockedNotificationPackages, turnPhoneScreenOffOnStart, pendingBrightnessRestores, managedOverlaySerials, didCleanLegacyOverlay }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        display = try values.decodeIfPresent(DisplaySettings.self, forKey: .display) ?? DisplaySettings()
        scrcpyPath = try values.decodeIfPresent(String.self, forKey: .scrcpyPath) ?? ""
        adbPath = try values.decodeIfPresent(String.self, forKey: .adbPath) ?? ""
        deviceDisplays = try values.decodeIfPresent([String: DisplaySettings].self, forKey: .deviceDisplays) ?? [:]
        appProfiles = try values.decodeIfPresent([String: DisplaySettings].self, forKey: .appProfiles) ?? [:]
        favoritePackages = try values.decodeIfPresent([String].self, forKey: .favoritePackages) ?? ["com.android.settings", "", ""]
        while favoritePackages.count < 3 { favoritePackages.append("") }
        favoritePackages = Array(favoritePackages.prefix(3))
        deviceAliases = try values.decodeIfPresent([String: String].self, forKey: .deviceAliases) ?? [:]
        deviceNativeDisplays = try values.decodeIfPresent([String: DisplaySettings].self, forKey: .deviceNativeDisplays) ?? [:]
        presenceMode = try values.decodeIfPresent(AppPresenceMode.self, forKey: .presenceMode) ?? .dockAndMenuBar
        openMainWindowAtLaunch = try values.decodeIfPresent(Bool.self, forKey: .openMainWindowAtLaunch) ?? true
        appLaunchMode = try values.decodeIfPresent(AppLaunchMode.self, forKey: .appLaunchMode) ?? .desktopWindow
        windowPlacements = try values.decodeIfPresent([String: WindowPlacement].self, forKey: .windowPlacements) ?? [:]
        controlBarPosition = try values.decodeIfPresent(ControlBarPosition.self, forKey: .controlBarPosition) ?? .bottom
        autoHideMinutes = try values.decodeIfPresent(Int.self, forKey: .autoHideMinutes) ?? 10
        lastWirelessEndpoint = try values.decodeIfPresent(String.self, forKey: .lastWirelessEndpoint) ?? ""
        automaticReconnect = try values.decodeIfPresent(Bool.self, forKey: .automaticReconnect) ?? true
        phoneNotificationsEnabled = try values.decodeIfPresent(Bool.self, forKey: .phoneNotificationsEnabled) ?? false
        messageNotificationsEnabled = try values.decodeIfPresent(Bool.self, forKey: .messageNotificationsEnabled) ?? false
        appNotificationsEnabled = try values.decodeIfPresent(Bool.self, forKey: .appNotificationsEnabled) ?? false
        blockedNotificationPackages = try values.decodeIfPresent(Set<String>.self, forKey: .blockedNotificationPackages) ?? []
        turnPhoneScreenOffOnStart = try values.decodeIfPresent(Bool.self, forKey: .turnPhoneScreenOffOnStart) ?? false
        pendingBrightnessRestores = try values.decodeIfPresent([String: ScreenBrightnessState].self, forKey: .pendingBrightnessRestores) ?? [:]
        managedOverlaySerials = try values.decodeIfPresent(Set<String>.self, forKey: .managedOverlaySerials) ?? []
        didCleanLegacyOverlay = try values.decodeIfPresent(Bool.self, forKey: .didCleanLegacyOverlay) ?? false
    }
}

public enum PhoneNotificationKind: String, Codable, Sendable {
    case call, message, application
}

public struct PhoneNotification: Identifiable, Hashable, Sendable {
    public let key: String
    public let package: String
    public let title: String
    public let body: String
    public let kind: PhoneNotificationKind

    public init(key: String, package: String, title: String, body: String, kind: PhoneNotificationKind) {
        self.key = key; self.package = package; self.title = title; self.body = body; self.kind = kind
    }

    public var fingerprint: String { "\(key)|\(title)|\(body)" }
    public var id: String { key }
}

public enum DXError: LocalizedError {
    case toolMissing(String)
    case commandFailed(String)
    case invalidSettings
    case displayNotFound

    public var errorDescription: String? {
        switch self {
        case .toolMissing(let tool): return "\(tool)을 찾을 수 없습니다. Homebrew로 설치하거나 설정에서 경로를 지정해 주세요."
        case .commandFailed(let message): return message
        case .invalidSettings: return "해상도, DPI, 비트레이트 또는 FPS 값이 허용 범위를 벗어났습니다."
        case .displayNotFound: return "생성된 데스크톱 가상 디스플레이 ID를 확인하지 못했습니다."
        }
    }
}
