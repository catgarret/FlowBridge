import Foundation

public enum NotificationParser {
    public static func parse(_ dump: String) -> [PhoneNotification] {
        dump.components(separatedBy: "NotificationRecord(").dropFirst().compactMap(parseRecord)
    }

    private static func parseRecord(_ record: String) -> PhoneNotification? {
        guard let header = record.split(separator: "\n", maxSplits: 1).first.map(String.init),
              let package = capture(#"pkg=([^\s]+)"#, in: header),
              let key = record.split(whereSeparator: \ .isNewline)
                .map({ $0.trimmingCharacters(in: .whitespaces) })
                .first(where: { $0.hasPrefix("key=") })
                .map({ String($0.dropFirst(4)) }) else { return nil }

        let title = extra("android.title", in: record)
        let body = extra("android.text", in: record).isEmpty ? extra("android.bigText", in: record) : extra("android.text", in: record)
        guard !title.isEmpty || !body.isEmpty else { return nil }
        let lower = (header + "\n" + record.prefix(1200)).lowercased()
        return PhoneNotification(key: key, package: package, title: title, body: body, kind: classify(package: package, text: lower))
    }

    private static func extra(_ name: String, in record: String) -> String {
        let escaped = NSRegularExpression.escapedPattern(for: name)
        guard let value = capture("(?m)^\\s*\(escaped)=String \\((.*)\\)\\s*$", in: record) else { return "" }
        return value.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func classify(package: String, text: String) -> PhoneNotificationKind {
        let calls: Set<String> = ["com.samsung.android.incallui", "com.samsung.android.dialer", "com.android.server.telecom", "com.google.android.dialer"]
        let messages: Set<String> = ["com.samsung.android.messaging", "com.google.android.apps.messaging", "com.android.mms"]
        if calls.contains(package) || text.contains("category=call") || text.contains("channel=call") { return .call }
        if messages.contains(package) || text.contains("category=msg") || text.contains("category=message") { return .message }
        return .application
    }

    private static func capture(_ pattern: String, in text: String) -> String? {
        guard let expression = try? NSRegularExpression(pattern: pattern),
              let match = expression.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)),
              match.numberOfRanges > 1,
              let range = Range(match.range(at: 1), in: text) else { return nil }
        return String(text[range])
    }
}
