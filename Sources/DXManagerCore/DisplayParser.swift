import Foundation

public enum DisplayParser {
    private static let idPattern = try! NSRegularExpression(pattern: #"mDisplayId\s*=\s*(\d+)"#)

    public static func ids(from dump: String) -> Set<Int> {
        let range = NSRange(dump.startIndex..., in: dump)
        return Set(idPattern.matches(in: dump, range: range).compactMap { match in
            guard let swiftRange = Range(match.range(at: 1), in: dump) else { return nil }
            return Int(dump[swiftRange])
        }.filter { $0 > 0 })
    }
}
