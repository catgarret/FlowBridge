import AppKit
import SwiftUI
import DXManagerCore

@MainActor
final class MiniControlBarManager {
    private var panels: [String: NSPanel] = [:]
    private var collapsed: Set<String> = []
    private var lastProtectedScreen = false
    private var currentPosition: ControlBarPosition = .bottom

    func sync(sessions: [DXSessionController.SessionInfo], position: ControlBarPosition, protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
              initialVolume: Int, setVolume: @escaping (Int) -> Void,
              power: @escaping () -> Void, stop: @escaping () -> Void) {
        currentPosition = position
        if protectedScreen != lastProtectedScreen { closeAll(); lastProtectedScreen = protectedScreen }
        let active = Set(sessions.map(\.id))
        for key in panels.keys where !active.contains(key) { panels[key]?.close(); panels.removeValue(forKey: key) }
        for session in sessions {
            guard let window = windowInfo(processID: session.processID) else { continue }
            let panel = panels[session.id] ?? makePanel(session: session, windowID: window.id, position: position, protectedScreen: protectedScreen, capture: capture, initialVolume: initialVolume, setVolume: setVolume, power: power, stop: stop)
            panels[session.id] = panel
            positionPanel(panel, inside: window.bounds, position: position, isCollapsed: collapsed.contains(session.id))
            panel.order(.above, relativeTo: Int(window.id))
        }
    }

    func closeAll() { panels.values.forEach { $0.close() }; panels.removeAll(); collapsed.removeAll() }

    private func makePanel(session: DXSessionController.SessionInfo, windowID: CGWindowID, position: ControlBarPosition, protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
                           initialVolume: Int, setVolume: @escaping (Int) -> Void,
                           power: @escaping () -> Void, stop: @escaping () -> Void) -> NSPanel {
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 480, height: 52),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.level = .normal
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false
        panel.isMovableByWindowBackground = false
        panel.contentView = NSHostingView(rootView: CompactSessionControls(initialVolume: initialVolume, protectedScreen: protectedScreen, capture: { capture(windowID, session.title) }, setVolume: setVolume, power: power, stop: stop, collapseChanged: { [weak self, weak panel] isCollapsed in
            guard let self, let panel else { return }
            if isCollapsed { self.collapsed.insert(session.id) } else { self.collapsed.remove(session.id) }
            if let window = self.windowInfo(processID: session.processID) {
                self.positionPanel(panel, inside: window.bounds, position: self.currentPosition, isCollapsed: isCollapsed)
                panel.order(.above, relativeTo: Int(window.id))
            }
        }))
        return panel
    }

    private func positionPanel(_ panel: NSPanel, inside frame: CGRect, position: ControlBarPosition, isCollapsed: Bool) {
        let screenTop = NSScreen.screens.first?.frame.maxY ?? 0
        let video = CGRect(x: frame.minX, y: screenTop - frame.maxY, width: frame.width, height: frame.height)
        let height: CGFloat = isCollapsed ? 30 : 52
        let y = position == .top ? video.maxY - height : video.minY
        panel.setFrame(NSRect(x: video.minX, y: y, width: max(240, video.width), height: height), display: false)
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
    @State private var isCollapsed = false
    let protectedScreen: Bool
    let capture: () -> Void; let setVolume: (Int) -> Void; let power: () -> Void; let stop: () -> Void
    let collapseChanged: (Bool) -> Void
    init(initialVolume: Int, protectedScreen: Bool, capture: @escaping () -> Void, setVolume: @escaping (Int) -> Void, power: @escaping () -> Void, stop: @escaping () -> Void, collapseChanged: @escaping (Bool) -> Void) { _volume = State(initialValue: Double(initialVolume)); self.protectedScreen = protectedScreen; self.capture = capture; self.setVolume = setVolume; self.power = power; self.stop = stop; self.collapseChanged = collapseChanged }
    var body: some View {
        HStack(spacing: 10) {
            Button { withAnimation(.easeInOut(duration: 0.16)) { isCollapsed.toggle(); collapseChanged(isCollapsed) } } label: { Image(systemName: isCollapsed ? "chevron.up" : "chevron.down") }.help(isCollapsed ? "제어 바 펼치기" : "제어 바 접기")
            if !isCollapsed {
                Image(systemName: volume == 0 ? "speaker.slash.fill" : "speaker.wave.2.fill").foregroundStyle(.secondary)
                Text("볼륨").font(.caption.weight(.medium)).foregroundStyle(.secondary).padding(.trailing, 4)
                Slider(value: $volume, in: 0...15, step: 1).frame(minWidth: 120, idealWidth: 180, maxWidth: 220).onChange(of: volume) { setVolume(Int($0)) }
                if protectedScreen { Label("보호된 화면 · Galaxy에서 계속", systemImage: "lock.fill").font(.caption.weight(.medium)).foregroundStyle(.orange) }
                Spacer(minLength: 8)
                Divider().frame(height: 22)
                Button(action: power) { Image(systemName: "power") }.help("Galaxy 전원")
                Button(action: capture) { Image(systemName: "camera") }.help("화면 캡처")
                Button(action: stop) { Image(systemName: "xmark.circle.fill").foregroundStyle(.red) }.help("화면 종료")
            }
        }.buttonStyle(.bordered).controlSize(.regular).padding(.horizontal, 12).frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(.ultraThinMaterial)
            .overlay(alignment: .top) { Divider() }
    }
}
