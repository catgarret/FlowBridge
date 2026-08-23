import AppKit
import SwiftUI
import DXManagerCore

@MainActor
final class MiniControlBarManager {
    private var panels: [String: NSPanel] = [:]

    func sync(sessions: [DXSessionController.SessionInfo], capture: @escaping (CGWindowID, String) -> Void,
              initialVolume: Int, setVolume: @escaping (Int) -> Void,
              power: @escaping () -> Void, stop: @escaping () -> Void) {
        let active = Set(sessions.map(\.id))
        for key in panels.keys where !active.contains(key) { panels[key]?.close(); panels.removeValue(forKey: key) }
        for session in sessions {
            guard let window = windowInfo(processID: session.processID) else { continue }
            let panel = panels[session.id] ?? makePanel(session: session, windowID: window.id, capture: capture, initialVolume: initialVolume, setVolume: setVolume, power: power, stop: stop)
            panels[session.id] = panel
            let frame = window.bounds
            panel.setFrameOrigin(NSPoint(x: frame.maxX + 8, y: NSScreen.screens.first.map { $0.frame.maxY - frame.maxY } ?? frame.minY))
            panel.orderFrontRegardless()
        }
    }

    func closeAll() { panels.values.forEach { $0.close() }; panels.removeAll() }

    private func makePanel(session: DXSessionController.SessionInfo, windowID: CGWindowID, capture: @escaping (CGWindowID, String) -> Void,
                           initialVolume: Int, setVolume: @escaping (Int) -> Void,
                           power: @escaping () -> Void, stop: @escaping () -> Void) -> NSPanel {
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 250, height: 48),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false
        panel.contentView = NSHostingView(rootView: CompactSessionControls(initialVolume: initialVolume, capture: { capture(windowID, session.title) }, setVolume: setVolume, power: power, stop: stop))
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

private struct CompactSessionControls: View {
    @State private var volume: Double
    let capture: () -> Void; let setVolume: (Int) -> Void; let power: () -> Void; let stop: () -> Void
    init(initialVolume: Int, capture: @escaping () -> Void, setVolume: @escaping (Int) -> Void, power: @escaping () -> Void, stop: @escaping () -> Void) { _volume = State(initialValue: Double(initialVolume)); self.capture = capture; self.setVolume = setVolume; self.power = power; self.stop = stop }
    var body: some View {
        HStack(spacing: 9) {
            Image(systemName: volume == 0 ? "speaker.slash.fill" : "speaker.wave.2.fill").foregroundStyle(.secondary)
            Slider(value: $volume, in: 0...15, step: 1) { Text("볼륨") }.frame(width: 88).onChange(of: volume) { setVolume(Int($0)) }
            Divider().frame(height: 22)
            Button(action: power) { Image(systemName: "power") }.help("Galaxy 전원")
            Button(action: capture) { Image(systemName: "camera") }.help("화면 캡처")
            Button(action: stop) { Image(systemName: "xmark.circle.fill").foregroundStyle(.red) }.help("화면 종료")
        }.buttonStyle(.borderless).padding(.horizontal, 12).frame(height: 44)
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.primary.opacity(0.12)))
    }
}
