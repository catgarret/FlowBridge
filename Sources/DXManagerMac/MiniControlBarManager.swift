import AppKit
import SwiftUI
import DXManagerCore

@MainActor
final class MiniControlBarManager {
    private enum DockEdge { case top, bottom, left, right }
    private var panels: [String: NSPanel] = [:]
    private var collapsed: Set<String> = []
    private var dockEdges: [String: DockEdge] = [:]
    private var dragging: Set<String> = []
    private var lastProtectedScreen = false

    func sync(sessions: [DXSessionController.SessionInfo], position: ControlBarPosition, protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
              initialVolume: Int, setVolume: @escaping (Int) -> Void,
              power: @escaping () -> Void, stop: @escaping () -> Void) {
        if protectedScreen != lastProtectedScreen { closeAll(); lastProtectedScreen = protectedScreen }
        let active = Set(sessions.map(\.id))
        for key in panels.keys where !active.contains(key) { panels[key]?.close(); panels.removeValue(forKey: key) }
        for session in sessions {
            guard let window = windowInfo(processID: session.processID) else { continue }
            let panel = panels[session.id] ?? makePanel(session: session, windowID: window.id, position: position, protectedScreen: protectedScreen, capture: capture, initialVolume: initialVolume, setVolume: setVolume, power: power, stop: stop)
            panels[session.id] = panel
            let isPressed = NSEvent.pressedMouseButtons != 0 && panel.frame.insetBy(dx: -8, dy: -8).contains(NSEvent.mouseLocation)
            if isPressed { dragging.insert(session.id); continue }
            if dragging.remove(session.id) != nil { dockEdges[session.id] = nearestEdge(panel: panel, beside: window.bounds) }
            let edge = dockEdges[session.id] ?? (position == .top ? .top : .bottom)
            positionPanel(panel, beside: window.bounds, edge: edge)
        }
    }

    func closeAll() { panels.values.forEach { $0.close() }; panels.removeAll(); dockEdges.removeAll(); dragging.removeAll() }

    private func makePanel(session: DXSessionController.SessionInfo, windowID: CGWindowID, position: ControlBarPosition, protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
                           initialVolume: Int, setVolume: @escaping (Int) -> Void,
                           power: @escaping () -> Void, stop: @escaping () -> Void) -> NSPanel {
        let expandedWidth: CGFloat = protectedScreen ? 560 : 410
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: expandedWidth, height: 58),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false
        panel.isMovableByWindowBackground = true
        panel.contentView = NSHostingView(rootView: CompactSessionControls(initialVolume: initialVolume, protectedScreen: protectedScreen, capture: { capture(windowID, session.title) }, setVolume: setVolume, power: power, stop: stop, collapseChanged: { [weak self, weak panel] isCollapsed in
            guard let self, let panel else { return }
            if isCollapsed { self.collapsed.insert(session.id) } else { self.collapsed.remove(session.id) }
            panel.setContentSize(NSSize(width: isCollapsed ? 54 : expandedWidth, height: 58))
            if let window = self.windowInfo(processID: session.processID) { self.positionPanel(panel, beside: window.bounds, edge: self.dockEdges[session.id] ?? (position == .top ? .top : .bottom)) }
        }))
        panel.orderFrontRegardless()
        return panel
    }

    private func positionPanel(_ panel: NSPanel, beside frame: CGRect, edge: DockEdge) {
        let screenTop = NSScreen.screens.first?.frame.maxY ?? 0
        let video = CGRect(x: frame.minX, y: screenTop - frame.maxY, width: frame.width, height: frame.height)
        let inset: CGFloat = 10
        let origin: NSPoint
        switch edge {
        case .top: origin = NSPoint(x: video.midX - panel.frame.width / 2, y: video.maxY - panel.frame.height - inset)
        case .bottom: origin = NSPoint(x: video.midX - panel.frame.width / 2, y: video.minY + inset)
        case .left: origin = NSPoint(x: video.minX + inset, y: video.midY - panel.frame.height / 2)
        case .right: origin = NSPoint(x: video.maxX - panel.frame.width - inset, y: video.midY - panel.frame.height / 2)
        }
        panel.setFrameOrigin(origin)
    }

    private func nearestEdge(panel: NSPanel, beside frame: CGRect) -> DockEdge {
        let screenTop = NSScreen.screens.first?.frame.maxY ?? 0
        let video = CGRect(x: frame.minX, y: screenTop - frame.maxY, width: frame.width, height: frame.height)
        let point = CGPoint(x: panel.frame.midX, y: panel.frame.midY)
        let distances: [(DockEdge, CGFloat)] = [(.top, abs(video.maxY - point.y)), (.bottom, abs(point.y - video.minY)), (.left, abs(point.x - video.minX)), (.right, abs(video.maxX - point.x))]
        return distances.min(by: { $0.1 < $1.1 })?.0 ?? .bottom
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
        HStack(spacing: 12) {
            Button { withAnimation(.easeInOut(duration: 0.16)) { isCollapsed.toggle(); collapseChanged(isCollapsed) } } label: { Image(systemName: isCollapsed ? "chevron.right" : "chevron.left") }.help(isCollapsed ? "제어 바 펼치기" : "제어 바 접기")
            if !isCollapsed {
                Image(systemName: volume == 0 ? "speaker.slash.fill" : "speaker.wave.2.fill").foregroundStyle(.secondary)
                Slider(value: $volume, in: 0...15, step: 1) { Text("볼륨") }.frame(width: 150).onChange(of: volume) { setVolume(Int($0)) }
                if protectedScreen { Label("보호된 화면 · Galaxy에서 계속", systemImage: "lock.fill").font(.caption.weight(.medium)).foregroundStyle(.orange) }
                Divider().frame(height: 22)
                Button(action: power) { Image(systemName: "power") }.help("Galaxy 전원")
                Button(action: capture) { Image(systemName: "camera") }.help("화면 캡처")
                Button(action: stop) { Image(systemName: "xmark.circle.fill").foregroundStyle(.red) }.help("화면 종료")
            }
        }.buttonStyle(.bordered).controlSize(.large).padding(.horizontal, 14).frame(height: 54)
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.primary.opacity(0.12)))
    }
}
