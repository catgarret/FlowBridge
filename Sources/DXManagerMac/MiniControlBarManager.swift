import AppKit
import SwiftUI
import DXManagerCore

@MainActor
final class MiniControlBarManager {
    private var panels: [String: NSPanel] = [:]
    private var lastProtectedScreen = false

    func sync(sessions: [DXSessionController.SessionInfo], protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
              initialVolume: Int, setVolume: @escaping (Int) -> Void,
              back: @escaping () -> Void, home: @escaping () -> Void,
              power: @escaping () -> Void, stop: @escaping () -> Void) {
        if protectedScreen != lastProtectedScreen { closeAll(); lastProtectedScreen = protectedScreen }
        let active = Set(sessions.map(\.id))
        for key in panels.keys where !active.contains(key) { panels[key]?.close(); panels.removeValue(forKey: key) }
        for session in sessions {
            guard let window = windowInfo(processID: session.processID) else { continue }
            let panel = panels[session.id] ?? makePanel(session: session, windowID: window.id, protectedScreen: protectedScreen, capture: capture, initialVolume: initialVolume, setVolume: setVolume, back: back, home: home, power: power, stop: stop)
            panels[session.id] = panel
            positionPanel(panel, below: window.bounds)
            panel.order(.above, relativeTo: Int(window.id))
        }
    }

    func closeAll() { panels.values.forEach { $0.close() }; panels.removeAll() }

    private func makePanel(session: DXSessionController.SessionInfo, windowID: CGWindowID, protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
                           initialVolume: Int, setVolume: @escaping (Int) -> Void,
                           back: @escaping () -> Void, home: @escaping () -> Void,
                           power: @escaping () -> Void, stop: @escaping () -> Void) -> NSPanel {
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 480, height: 48),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.level = .normal
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false
        panel.isMovableByWindowBackground = false
        panel.contentView = NSHostingView(rootView: CompactSessionControls(initialVolume: initialVolume, showsPhoneNavigation: session.id.hasPrefix("phone:"), protectedScreen: protectedScreen, capture: { capture(windowID, session.title) }, setVolume: setVolume, back: back, home: home, power: power, stop: stop))
        return panel
    }

    private func positionPanel(_ panel: NSPanel, below frame: CGRect) {
        let screenTop = NSScreen.screens.first?.frame.maxY ?? 0
        let video = CGRect(x: frame.minX, y: screenTop - frame.maxY, width: frame.width, height: frame.height)
        let height: CGFloat = 48
        panel.setFrame(NSRect(x: video.minX, y: video.minY - height, width: max(240, video.width), height: height), display: false)
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
    let showsPhoneNavigation: Bool
    let protectedScreen: Bool
    let capture: () -> Void; let setVolume: (Int) -> Void; let back: () -> Void; let home: () -> Void; let power: () -> Void; let stop: () -> Void
    init(initialVolume: Int, showsPhoneNavigation: Bool, protectedScreen: Bool, capture: @escaping () -> Void, setVolume: @escaping (Int) -> Void, back: @escaping () -> Void, home: @escaping () -> Void, power: @escaping () -> Void, stop: @escaping () -> Void) { _volume = State(initialValue: Double(initialVolume)); self.showsPhoneNavigation = showsPhoneNavigation; self.protectedScreen = protectedScreen; self.capture = capture; self.setVolume = setVolume; self.back = back; self.home = home; self.power = power; self.stop = stop }
    var body: some View {
        HStack(spacing: 10) {
            if showsPhoneNavigation {
                Button(action: back) { Image(systemName: "arrow.uturn.backward") }.help("뒤로")
                Button(action: home) { Image(systemName: "house.fill") }.help("홈")
                Divider().frame(height: 22)
            }
            Image(systemName: volume == 0 ? "speaker.slash.fill" : "speaker.wave.2.fill").foregroundStyle(.secondary)
            Slider(value: $volume, in: 0...15, step: 1).frame(minWidth: 120, idealWidth: 180, maxWidth: 220).onChange(of: volume) { setVolume(Int($0)) }
            if protectedScreen { Label("보호된 화면 · Galaxy에서 계속", systemImage: "lock.fill").font(.caption.weight(.medium)).foregroundStyle(.orange) }
            Spacer(minLength: 8)
            Divider().frame(height: 22)
            Button(action: power) { Image(systemName: "power") }.help("Galaxy 전원")
            Button(action: capture) { Image(systemName: "camera") }.help("화면 캡처")
            Button(action: stop) { Image(systemName: "xmark.circle.fill").foregroundStyle(.red) }.help("화면 종료")
        }.buttonStyle(.bordered).controlSize(.regular).padding(.horizontal, 12).frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(.ultraThinMaterial)
            .overlay(alignment: .top) { Divider() }
    }
}
