import Foundation

public struct CommandResult: Sendable {
    public let status: Int32
    public let stdout: String
    public let stderr: String
}

public protocol CommandRunning: Sendable {
    func run(_ executable: String, _ arguments: [String]) throws -> CommandResult
}

public struct CommandRunner: CommandRunning {
    private final class DataBox: @unchecked Sendable {
        private let lock = NSLock()
        private var value = Data()
        func set(_ data: Data) { lock.lock(); value = data; lock.unlock() }
        func get() -> Data { lock.lock(); defer { lock.unlock() }; return value }
    }

    public init() {}
    public func run(_ executable: String, _ arguments: [String]) throws -> CommandResult {
        let process = Process()
        let output = Pipe()
        let error = Pipe()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        process.standardOutput = output
        process.standardError = error
        do { try process.run() } catch { throw DXError.commandFailed(error.localizedDescription) }
        let stdoutBox = DataBox()
        let stderrBox = DataBox()
        let group = DispatchGroup()
        let queue = DispatchQueue(label: "DXManager.CommandRunner", attributes: .concurrent)
        group.enter()
        queue.async { stdoutBox.set(output.fileHandleForReading.readDataToEndOfFile()); group.leave() }
        group.enter()
        queue.async { stderrBox.set(error.fileHandleForReading.readDataToEndOfFile()); group.leave() }
        process.waitUntilExit()
        group.wait()
        let stdout = String(decoding: stdoutBox.get(), as: UTF8.self)
        let stderr = String(decoding: stderrBox.get(), as: UTF8.self)
        let result = CommandResult(status: process.terminationStatus, stdout: stdout, stderr: stderr)
        guard result.status == 0 else {
            throw DXError.commandFailed(stderr.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? stdout : stderr)
        }
        return result
    }
}
