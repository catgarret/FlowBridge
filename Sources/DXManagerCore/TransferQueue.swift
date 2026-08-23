import Foundation

public struct TransferUpdate: Sendable {
    public let current: String
    public let completed: Int
    public let failed: Int
    public let waiting: Int
    public let cancelled: Bool
}

public final class TransferQueue: @unchecked Sendable {
    private let lock = NSLock()
    private var shouldCancel = false

    public init() {}

    public func cancel() {
        lock.lock(); shouldCancel = true; lock.unlock()
    }

    public func run(adb: ADBService, serial: String, urls: [URL], remoteDirectory: String,
                    update: @escaping @Sendable (TransferUpdate) -> Void) {
        lock.lock(); shouldCancel = false; lock.unlock()
        var completed = 0
        var failed = 0
        for (index, url) in urls.enumerated() {
            lock.lock(); let cancelled = shouldCancel; lock.unlock()
            if cancelled {
                update(TransferUpdate(current: url.lastPathComponent, completed: completed, failed: failed,
                                      waiting: urls.count - index, cancelled: true))
                return
            }
            update(TransferUpdate(current: url.lastPathComponent, completed: completed, failed: failed,
                                  waiting: urls.count - index, cancelled: false))
            do { _ = try adb.push(serial: serial, localURL: url, remoteDirectory: remoteDirectory); completed += 1 }
            catch { failed += 1 }
        }
        update(TransferUpdate(current: "", completed: completed, failed: failed, waiting: 0, cancelled: false))
    }
}
