import AppKit
import SwiftUI
import DXManagerCore

@MainActor
final class MiniControlBarManager {
    private var panels: [String: NSPanel] = [:]

    func sync(sessions: [DXSessionController.SessionInfo], capture: @escaping (CGWindowID, String) -> Void,
              power: @escaping () -> Void, stop: @escaping () -> Void) {
        let active = Set(sessions.map(\.id))
        for key in panels.keys where !active.contains(key) { panels[key]?.close(); panels.removeValue(forKey: key) }
        for session in sessions {
            guard let window = windowInfo(processID: session.processID) else { continue }
            let panel = panels[session.id] ?? makePanel(session: session, windowID: window.id, capture: capture, power: power, stop: stop)
            panels[session.id] = panel
            let frame = window.bounds
            panel.setFrameOrigin(NSPoint(x: frame.maxX + 6, y: NSScreen.screens.first.map { $0.frame.maxY - frame.maxY } ?? frame.minY))
            panel.orderFrontRegardless()
        }
    }

    func closeAll() { panels.values.forEach { $0.close() }; panels.removeAll() }

    private func makePanel(session: DXSessionController.SessionInfo, windowID: CGWindowID, capture: @escaping (CGWindowID, String) -> Void,
                           power: @escaping () -> Void, stop: @escaping () -> Void) -> NSPanel {
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 42, height: 150),
                            styleMask: [.titled, .utilityWindow, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.title = "DX"
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false
        panel.contentView = NSHostingView(rootView: VStack(spacing: 8) {
            Button(action: { capture(windowID, session.title) }) { Image(systemName: "camera") }.help("scrcpy 창 캡처")
            Button(action: power) { Image(systemName: "power") }.help("휴대폰 전원")
            Button(action: stop) { Image(systemName: "stop.fill") }.help("세션 중지")
        }.buttonStyle(.borderless).padding(8))
        return panel
    }

    private func windowInfo(processID: Int32) -> (id: CGWindowID, bounds: CGRect)? {
        guard let items = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else { return nil }
        for item in items where (item[kCGWindowOwnerPID as String] as? Int32) == processID {
            guard let number = item[kCGWindowNumber as String] as? CGWindowID,
                  let dict = item[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: dict), bounds.width > 200 else { continue }
            return (number, bounds)
        }
        return nil
    }
}
