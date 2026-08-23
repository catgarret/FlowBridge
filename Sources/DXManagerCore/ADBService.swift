import Foundation

public struct ADBService: Sendable {
    let runner: any CommandRunning
    let executable: String

    public init(executable: String, runner: any CommandRunning = CommandRunner()) {
        self.executable = executable
        self.runner = runner
    }

    public func devices() throws -> [Device] {
        let result = try runner.run(executable, ["devices", "-l"])
        let observed = Self.parseDevices(result.stdout)
        var enriched: [Device] = []
        for device in observed where device.isReady {
            let identity = (try? shell(serial: device.serial, ["getprop", "ro.serialno"]))?
                .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            enriched.append(Device(serial: device.serial, state: device.state, model: device.model, physicalSerial: identity))
        }
        return Self.mergeTransports(enriched)
    }

    public func shell(serial: String, _ arguments: [String]) throws -> String {
        try runner.run(executable, ["-s", serial, "shell"] + arguments).stdout
    }

    public func connect(_ endpoint: String) throws -> String {
        try runner.run(executable, ["connect", endpoint]).stdout.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func pair(_ endpoint: String, code: String) throws -> String {
        try runner.run(executable, ["pair", endpoint, code]).stdout.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func disconnect(_ endpoint: String) throws -> String {
        try runner.run(executable, ["disconnect", endpoint]).stdout.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func mdnsServices() throws -> [ADBMDNSService] {
        Self.parseMDNSServices(try runner.run(executable, ["mdns", "services"]).stdout)
    }

    public func enableTCPIP(serial: String, port: Int = 5555) throws -> String {
        try runner.run(executable, ["-s", serial, "tcpip", String(port)]).stdout.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func wirelessIPv4(serial: String) throws -> String {
        let output = try shell(serial: serial, ["ip", "route", "get", "1.1.1.1"])
        guard let match = output.range(of: #"\bsrc\s+(\d{1,3}(?:\.\d{1,3}){3})\b"#, options: .regularExpression) else {
            throw DXError.commandFailed("휴대폰의 Wi-Fi IP 주소를 자동으로 찾지 못했습니다.")
        }
        let fragment = String(output[match])
        return fragment.split(whereSeparator: \ .isWhitespace).last.map(String.init) ?? ""
    }

    public func deviceProperties(serial: String) throws -> [String: String] {
        let keys = ["ro.product.manufacturer", "ro.product.model", "ro.build.version.release", "ro.build.version.sdk", "ro.build.version.security_patch", "ro.build.version.oneui"]
        var result: [String: String] = [:]
        for key in keys {
            result[key] = try shell(serial: serial, ["getprop", key]).trimmingCharacters(in: .whitespacesAndNewlines)
        }
        return result
    }

    public func keyEvent(serial: String, code: Int) throws {
        _ = try shell(serial: serial, ["input", "keyevent", String(code)])
    }

    public func phoneScreenState(serial: String) throws -> PhoneScreenState {
        let power = try shell(serial: serial, ["dumpsys", "power"])
        let policy = try shell(serial: serial, ["dumpsys", "window", "policy"])
        return Self.parsePhoneScreenState(power: power, policy: policy)
    }

    public static func parsePhoneScreenState(power: String, policy: String) -> PhoneScreenState {
        let awake = power.contains("mWakefulness=Awake") || policy.contains("screenState=SCREEN_STATE_ON")
        let locked = policy.contains("showing=true") || policy.contains("isKeyguardShowing=true")
        return PhoneScreenState(isAwake: awake, isLocked: locked)
    }

    public func screenBrightness(serial: String) throws -> Int {
        Int(try shell(serial: serial, ["settings", "get", "system", "screen_brightness"])
            .trimmingCharacters(in: .whitespacesAndNewlines)) ?? 128
    }

    public func setScreenBrightness(serial: String, value: Int) throws {
        _ = try shell(serial: serial, ["settings", "put", "system", "screen_brightness_mode", "0"])
        _ = try shell(serial: serial, ["settings", "put", "system", "screen_brightness", String(max(0, min(255, value)))])
    }

    public func nativeDisplaySize(serial: String) throws -> (width: Int, height: Int) {
        let output = try shell(serial: serial, ["wm", "size"])
        guard let size = Self.parseNativeDisplaySize(output) else {
            throw DXError.commandFailed("Galaxy의 실제 화면 해상도를 확인하지 못했습니다.")
        }
        return size
    }

    public static func parseNativeDisplaySize(_ output: String) -> (width: Int, height: Int)? {
        guard let match = output.range(of: #"Physical size:\s*(\d+)x(\d+)"#, options: .regularExpression) else {
            return nil
        }
        let values = output[match].split(whereSeparator: { !$0.isNumber }).compactMap { Int($0) }
        guard values.count >= 2 else { return nil }
        return (max(values[0], values[1]), min(values[0], values[1]))
    }

    public func installedPackages(serial: String) throws -> [String] {
        let output = try shell(serial: serial, ["pm", "list", "packages", "-3"])
        return output.split(whereSeparator: \ .isNewline)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { $0.hasPrefix("package:") }
            .map { String($0.dropFirst(8)) }
            .sorted()
    }

    public func launcherApps(serial: String) throws -> [InstalledApp] {
        let output = try shell(serial: serial, ["cmd", "package", "query-activities", "--brief", "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER"])
        return Self.parseLauncherApps(output)
    }

    public static func parseLauncherApps(_ output: String) -> [InstalledApp] {
        let packages = Set(output.split(whereSeparator: \ .isNewline).compactMap { raw -> String? in
            let line = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !line.hasPrefix("Activity #"), let slash = line.firstIndex(of: "/") else { return nil }
            let package = String(line[..<slash])
            return package.contains(".") && !package.contains(" ") ? package : nil
        })
        return packages.map { InstalledApp(name: Self.friendlyAppName(for: $0), package: $0) }
            .sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }

    public func openDialer(serial: String, number: String) throws {
        let digits = number.filter { $0.isNumber || $0 == "+" || $0 == "*" || $0 == "#" }
        guard !digits.isEmpty else { throw DXError.commandFailed("전화번호를 입력해 주세요.") }
        _ = try shell(serial: serial, ["am", "start", "-a", "android.intent.action.DIAL", "-d", "tel:\(digits)"])
    }

    public func composeMessage(serial: String, number: String, body: String) throws {
        let digits = number.filter { $0.isNumber || $0 == "+" }
        guard !digits.isEmpty else { throw DXError.commandFailed("받는 사람 전화번호를 입력해 주세요.") }
        var arguments = ["am", "start", "-a", "android.intent.action.SENDTO", "-d", "smsto:\(digits)"]
        if !body.isEmpty { arguments += ["--es", "sms_body", body] }
        _ = try shell(serial: serial, arguments)
    }

    public func push(serial: String, localURL: URL, remoteDirectory: String) throws -> String {
        try runner.run(executable, ["-s", serial, "push", localURL.path, remoteDirectory]).stdout
    }

    public func install(serial: String, apk: URL) throws -> String {
        try runner.run(executable, ["-s", serial, "install", "-r", apk.path]).stdout.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func uninstall(serial: String, package: String) throws -> String {
        try runner.run(executable, ["-s", serial, "uninstall", package]).stdout.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func grant(serial: String, package: String, permission: String) throws {
        _ = try shell(serial: serial, ["pm", "grant", package, permission])
    }

    public func packagePath(serial: String, package: String) throws -> String {
        try shell(serial: serial, ["pm", "path", package]).trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func notifications(serial: String) throws -> [PhoneNotification] {
        NotificationParser.parse(try shell(serial: serial, ["dumpsys", "notification", "--noredact"]))
    }

    public func contacts(serial: String) throws -> [PhoneContact] {
        Self.parseContacts(try shell(serial: serial, ["content", "query", "--uri", "content://com.android.contacts/data/phones", "--projection", "display_name:data1"]))
    }

    public func recentCalls(serial: String) throws -> [PhoneCall] {
        Array(Self.parseCalls(try shell(serial: serial, ["content", "query", "--uri", "content://call_log/calls", "--projection", "number:type:date"])).prefix(100))
    }

    public func recentMessages(serial: String) throws -> [PhoneMessage] {
        Array(Self.parseMessages(try shell(serial: serial, ["content", "query", "--uri", "content://sms", "--projection", "address:body:date:type"])).prefix(200))
    }

    public static func parseContacts(_ output: String) -> [PhoneContact] {
        var seen = Set<String>()
        return output.split(whereSeparator: \.isNewline).compactMap { raw in
            let line = String(raw); guard let nameStart = line.range(of: "display_name="), let numberMark = line.range(of: ", data1=") else { return nil }
            let name = String(line[nameStart.upperBound..<numberMark.lowerBound]); let number = String(line[numberMark.upperBound...])
            guard !name.isEmpty, !number.isEmpty, seen.insert("\(name)|\(number)").inserted else { return nil }
            return PhoneContact(name: name, number: number)
        }.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }

    public static func parseCalls(_ output: String) -> [PhoneCall] {
        output.split(whereSeparator: \.isNewline).compactMap { raw in
            let line = String(raw); guard let n = line.range(of: "number="), let t = line.range(of: ", type="), let d = line.range(of: ", date=") else { return nil }
            return PhoneCall(number: String(line[n.upperBound..<t.lowerBound]), type: Int(line[t.upperBound..<d.lowerBound]) ?? 0, date: Date(timeIntervalSince1970: (Double(line[d.upperBound...]) ?? 0) / 1000))
        }.sorted { $0.date > $1.date }
    }

    public static func parseMessages(_ output: String) -> [PhoneMessage] {
        output.split(whereSeparator: \.isNewline).compactMap { raw in
            let line = String(raw); guard let a = line.range(of: "address="), let b = line.range(of: ", body="), let d = line.range(of: ", date=", options: .backwards) else { return nil }
            let typeMark = line.range(of: ", type=", range: d.upperBound..<line.endIndex)
            let dateEnd = typeMark?.lowerBound ?? line.endIndex
            let type = typeMark.flatMap { Int(line[$0.upperBound...]) } ?? 1
            return PhoneMessage(address: String(line[a.upperBound..<b.lowerBound]), body: String(line[b.upperBound..<d.lowerBound]), date: Date(timeIntervalSince1970: (Double(line[d.upperBound..<dateEnd]) ?? 0) / 1000), type: type)
        }.sorted { $0.date > $1.date }
    }

    public func pull(serial: String, remotePath: String, localURL: URL) throws {
        _ = try runner.run(executable, ["-s", serial, "pull", remotePath, localURL.path])
    }

    public func downloadFiles(serial: String) throws -> [RemoteFile] {
        let output = try shell(serial: serial, ["ls", "-1p", "/sdcard/Download"])
        return Self.parseDownloadEntries(output)
    }

    public static func parseDownloadEntries(_ output: String) -> [RemoteFile] {
        output.split(whereSeparator: \ .isNewline).compactMap { raw in
            var name = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !name.isEmpty else { return nil }
            let isDirectory = name.hasSuffix("/")
            if isDirectory { name.removeLast() }
            return RemoteFile(name: name, path: "/sdcard/Download/\(name)", isDirectory: isDirectory)
        }.sorted {
            if $0.isDirectory != $1.isDirectory { return $0.isDirectory }
            return $0.name.localizedStandardCompare($1.name) == .orderedAscending
        }
    }

    public static func parseDevices(_ output: String) -> [Device] {
        output.split(whereSeparator: \ .isNewline).dropFirst().compactMap { raw in
            let fields = raw.split(whereSeparator: \ .isWhitespace).map(String.init)
            guard fields.count >= 2, !fields[0].isEmpty else { return nil }
            let model = fields.first(where: { $0.hasPrefix("model:") }).map { String($0.dropFirst(6)) } ?? ""
            return Device(serial: fields[0], state: fields[1], model: model)
        }
    }

    public static func parseMDNSServices(_ output: String) -> [ADBMDNSService] {
        var seen: Set<String> = []
        return output.split(whereSeparator: \ .isNewline).compactMap { raw in
            let fields = raw.split(whereSeparator: \ .isWhitespace).map(String.init)
            guard fields.count >= 3, fields[1].hasPrefix("_adb-tls-"), fields[2].contains(":"), seen.insert("\(fields[1])|\(fields[2])").inserted else { return nil }
            return ADBMDNSService(name: fields[0], type: fields[1], endpoint: fields[2])
        }
    }

    public static func mergeTransports(_ devices: [Device]) -> [Device] {
        let grouped = Dictionary(grouping: devices) { device in
            device.physicalSerial.isEmpty ? "transport:\(device.serial)" : "physical:\(device.physicalSerial)"
        }
        return grouped.values.compactMap { candidates in
            candidates.sorted { lhs, rhs in transportRank(lhs.serial) < transportRank(rhs.serial) }.first
        }.sorted { $0.displayName.localizedStandardCompare($1.displayName) == .orderedAscending }
    }

    private static func transportRank(_ serial: String) -> Int {
        if serial.range(of: #"^[A-Za-z0-9]+$"#, options: .regularExpression) != nil { return 0 }
        if serial.range(of: #"^\d+\.\d+\.\d+\.\d+:\d+$"#, options: .regularExpression) != nil { return 1 }
        if serial.contains("._adb-tls-connect._tcp") { return 2 }
        return 3
    }

    private static func friendlyAppName(for package: String) -> String {
        let known: [String: String] = [
            "com.android.settings": "설정", "com.android.vending": "Google Play 스토어",
            "com.google.android.apps.maps": "Google 지도", "com.google.android.apps.messaging": "Google 메시지",
            "com.google.android.youtube": "YouTube", "com.google.android.gm": "Gmail",
            "com.google.android.apps.photos": "Google 포토", "com.google.android.calendar": "Google 캘린더",
            "com.google.android.keep": "Google Keep", "com.google.android.apps.docs": "Google Drive",
            "com.google.android.googlequicksearchbox": "Google", "com.android.chrome": "Chrome",
            "com.samsung.android.dialer": "전화", "com.samsung.android.messaging": "메시지",
            "com.samsung.android.app.contacts": "연락처", "com.samsung.android.calendar": "캘린더",
            "com.samsung.android.email.provider": "Samsung 이메일", "com.samsung.android.oneconnect": "SmartThings",
            "com.sec.android.app.camera": "카메라", "com.sec.android.gallery3d": "갤러리",
            "com.sec.android.app.music": "Samsung Music", "com.samsung.android.app.notes": "Samsung Notes",
            "com.samsung.android.app.reminder": "리마인더", "com.samsung.android.health": "Samsung Health",
            "com.samsung.android.wearable.app": "Galaxy Wearable", "com.microsoft.emmx": "Microsoft Edge",
            "com.instagram.android": "Instagram", "com.facebook.katana": "Facebook",
            "com.kakao.talk": "카카오톡", "com.nhn.android.search": "네이버",
            "com.spotify.music": "Spotify", "com.netflix.mediaclient": "Netflix"
        ]
        if let name = known[package] { return name }
        return package.split(separator: ".").last.map { token in
            token.replacingOccurrences(of: "_", with: " ").capitalized
        } ?? package
    }
}
