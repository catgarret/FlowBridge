import Foundation

public struct Device: Identifiable, Hashable, Sendable {
    public let serial: String
    public let state: String
    public let model: String
    public let physicalSerial: String
    public var id: String { serial }
    public var isReady: Bool { state == "device" }
    public var displayName: String { model.isEmpty ? serial : model.replacingOccurrences(of: "_", with: " ") }

    public init(serial: String, state: String, model: String, physicalSerial: String = "") {
        self.serial = serial; self.state = state; self.model = model; self.physicalSerial = physicalSerial
    }
}

public struct InstalledApp: Identifiable, Hashable, Sendable {
    public let name: String
    public let package: String
    public var id: String { package }
    public init(name: String, package: String) { self.name = name; self.package = package }
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

public struct AppSettings: Codable, Sendable {
    public var display = DisplaySettings()
    public var scrcpyPath = ""
    public var adbPath = ""
    public var deviceDisplays: [String: DisplaySettings] = [:]
    public var appProfiles: [String: DisplaySettings] = [:]
    public var autoHideMinutes = 10
    public var lastWirelessEndpoint = ""
    public var automaticReconnect = true
    public var phoneNotificationsEnabled = false
    public var messageNotificationsEnabled = false
    public var appNotificationsEnabled = false
    public init() {}

    private enum CodingKeys: String, CodingKey { case display, scrcpyPath, adbPath, deviceDisplays, appProfiles, autoHideMinutes, lastWirelessEndpoint, automaticReconnect, phoneNotificationsEnabled, messageNotificationsEnabled, appNotificationsEnabled }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        display = try values.decodeIfPresent(DisplaySettings.self, forKey: .display) ?? DisplaySettings()
        scrcpyPath = try values.decodeIfPresent(String.self, forKey: .scrcpyPath) ?? ""
        adbPath = try values.decodeIfPresent(String.self, forKey: .adbPath) ?? ""
        deviceDisplays = try values.decodeIfPresent([String: DisplaySettings].self, forKey: .deviceDisplays) ?? [:]
        appProfiles = try values.decodeIfPresent([String: DisplaySettings].self, forKey: .appProfiles) ?? [:]
        autoHideMinutes = try values.decodeIfPresent(Int.self, forKey: .autoHideMinutes) ?? 10
        lastWirelessEndpoint = try values.decodeIfPresent(String.self, forKey: .lastWirelessEndpoint) ?? ""
        automaticReconnect = try values.decodeIfPresent(Bool.self, forKey: .automaticReconnect) ?? true
        phoneNotificationsEnabled = try values.decodeIfPresent(Bool.self, forKey: .phoneNotificationsEnabled) ?? false
        messageNotificationsEnabled = try values.decodeIfPresent(Bool.self, forKey: .messageNotificationsEnabled) ?? false
        appNotificationsEnabled = try values.decodeIfPresent(Bool.self, forKey: .appNotificationsEnabled) ?? false
    }
}

public enum PhoneNotificationKind: String, Codable, Sendable {
    case call, message, application
}

public struct PhoneNotification: Hashable, Sendable {
    public let key: String
    public let package: String
    public let title: String
    public let body: String
    public let kind: PhoneNotificationKind

    public init(key: String, package: String, title: String, body: String, kind: PhoneNotificationKind) {
        self.key = key; self.package = package; self.title = title; self.body = body; self.kind = kind
    }

    public var fingerprint: String { "\(key)|\(title)|\(body)" }
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
