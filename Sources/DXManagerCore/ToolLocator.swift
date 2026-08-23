import Foundation

public enum ToolLocator {
    public static func locate(_ configured: String, candidates: [String]) -> String? {
        if !configured.isEmpty, FileManager.default.isExecutableFile(atPath: configured) { return configured }
        return candidates.first(where: FileManager.default.isExecutableFile(atPath:))
    }

    public static func adb(_ configured: String = "") -> String? {
        locate(configured, candidates: bundledCandidates("adb") + ["/opt/homebrew/bin/adb", "/usr/local/bin/adb"])
    }

    public static func scrcpy(_ configured: String = "") -> String? {
        locate(configured, candidates: bundledCandidates("scrcpy") + ["/opt/homebrew/bin/scrcpy", "/usr/local/bin/scrcpy"])
    }

    private static func bundledCandidates(_ executable: String) -> [String] {
        guard let resources = Bundle.main.resourceURL else { return [] }
        #if arch(arm64)
        let architecture = "arm64"
        #else
        let architecture = "x86_64"
        #endif
        return [resources.appendingPathComponent("runtime/\(architecture)/\(executable)").path]
    }
}
