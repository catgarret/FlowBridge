import AppKit
import SwiftUI
import DXManagerCore

@MainActor
final class MiniControlBarManager {
    private var panels: [String: NSPanel] = [:]
    private var lastProtectedScreen = false

    func sync(sessions: [DXSessionController.SessionInfo], protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
              initialVolume: Int, setVolume: @escaping (Int) -> Void,
              back: @escaping () -> Void, home: @escaping () -> Void, recents: @escaping () -> Void,
              power: @escaping () -> Void, stop: @escaping () -> Void) {
        if protectedScreen != lastProtectedScreen { closeAll(); lastProtectedScreen = protectedScreen }
        let active = Set(sessions.map(\.id))
        for key in panels.keys where !active.contains(key) { panels[key]?.close(); panels.removeValue(forKey: key) }
        for session in sessions {
            guard let window = windowInfo(processID: session.processID) else { continue }
            let panel = panels[session.id] ?? makePanel(session: session, windowID: window.id, protectedScreen: protectedScreen, capture: capture, initialVolume: initialVolume, setVolume: setVolume, back: back, home: home, recents: recents, power: power, stop: stop)
            panels[session.id] = panel
            positionPanel(panel, below: window.bounds)
            panel.order(.above, relativeTo: Int(window.id))
        }
    }

    func closeAll() { panels.values.forEach { $0.close() }; panels.removeAll() }

    private func makePanel(session: DXSessionController.SessionInfo, windowID: CGWindowID, protectedScreen: Bool, capture: @escaping (CGWindowID, String) -> Void,
                           initialVolume: Int, setVolume: @escaping (Int) -> Void,
                           back: @escaping () -> Void, home: @escaping () -> Void, recents: @escaping () -> Void,
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
        panel.contentView = NSHostingView(rootView: CompactSessionControls(initialVolume: initialVolume, showsPhoneNavigation: session.id.hasPrefix("phone:"), protectedScreen: protectedScreen, capture: { capture(windowID, session.title) }, setVolume: setVolume, back: back, home: home, recents: recents, power: power, stop: stop))
        return panel
    }

    private func positionPanel(_ panel: NSPanel, below frame: CGRect) {
        let screenTop = NSScreen.screens.first?.frame.maxY ?? 0
        let video = CGRect(x: frame.minX, y: screenTop - frame.maxY, width: frame.width, height: frame.height)
        let height: CGFloat = 48
        panel.setFrame(NSRect(x: video.minX, y: video.minY - height, width: video.width, height: height), display: false)
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
    let capture: () -> Void; let setVolume: (Int) -> Void; let back: () -> Void; let home: () -> Void; let recents: () -> Void; let power: () -> Void; let stop: () -> Void
    init(initialVolume: Int, showsPhoneNavigation: Bool, protectedScreen: Bool, capture: @escaping () -> Void, setVolume: @escaping (Int) -> Void, back: @escaping () -> Void, home: @escaping () -> Void, recents: @escaping () -> Void, power: @escaping () -> Void, stop: @escaping () -> Void) { _volume = State(initialValue: Double(initialVolume)); self.showsPhoneNavigation = showsPhoneNavigation; self.protectedScreen = protectedScreen; self.capture = capture; self.setVolume = setVolume; self.back = back; self.home = home; self.recents = recents; self.power = power; self.stop = stop }
    var body: some View {
        ViewThatFits(in: .horizontal) {
            fullControlRow
            compactControlRow
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(.ultraThinMaterial)
        .overlay(alignment: .top) { Divider() }
        .clipShape(BottomRoundedCorners(radius: 12))
    }

    private var fullControlRow: some View {
        HStack(spacing: 8) {
            if showsPhoneNavigation {
                controlButton("arrow.uturn.backward", help: "뒤로", action: back)
                controlButton("house.fill", help: "홈", action: home)
                controlButton("rectangle.stack.fill", help: "최근 앱", action: recents)
                Divider().frame(height: 24)
            }
            Image(systemName: volume == 0 ? "speaker.slash.fill" : "speaker.wave.2.fill").foregroundStyle(.secondary)
            Slider(value: $volume, in: 0...15, step: 1).frame(minWidth: 120, idealWidth: 180, maxWidth: 220).onChange(of: volume) { setVolume(Int($0)) }
            if protectedScreen { Label("보호된 화면 · Galaxy에서 계속", systemImage: "lock.fill").font(.caption.weight(.medium)).foregroundStyle(.orange) }
            Spacer(minLength: 8)
            Divider().frame(height: 24)
            controlButton("power", help: "Galaxy 전원", action: power)
            controlButton("camera", help: "화면 캡처", action: capture)
            controlButton("xmark.circle.fill", help: "화면 종료", tint: .red, action: stop)
        }
        .buttonStyle(.bordered)
        .controlSize(.regular)
        .padding(.horizontal, 10)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var compactControlRow: some View {
        HStack(spacing: 6) {
            if showsPhoneNavigation {
                controlButton("arrow.uturn.backward", help: "뒤로", action: back)
                controlButton("house.fill", help: "홈", action: home)
                controlButton("rectangle.stack.fill", help: "최근 앱", action: recents)
            }
            Image(systemName: volume == 0 ? "speaker.slash.fill" : "speaker.wave.2.fill").foregroundStyle(.secondary).frame(width: 24)
            Slider(value: $volume, in: 0...15, step: 1).frame(minWidth: 54, maxWidth: 100).onChange(of: volume) { setVolume(Int($0)) }
            Menu {
                Button("Galaxy 전원", systemImage: "power", action: power)
                Button("화면 캡처", systemImage: "camera", action: capture)
                Divider()
                Button("화면 종료", systemImage: "xmark.circle", role: .destructive, action: stop)
            } label: { Image(systemName: "ellipsis").frame(width: 30, height: 28) }
                .menuStyle(.borderlessButton).menuIndicator(.hidden).help("더보기")
        }
        .buttonStyle(.bordered)
        .controlSize(.regular)
        .padding(.horizontal, 6)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func controlButton(_ systemName: String, help: String, tint: Color? = nil, action: @escaping () -> Void) -> some View {
        Button(action: action) { Image(systemName: systemName).foregroundStyle(tint ?? .primary).frame(width: 24, height: 22) }
            .frame(minWidth: 38, minHeight: 30).help(help)
    }
}

private struct BottomRoundedCorners: Shape {
    let radius: CGFloat
    func path(in rect: CGRect) -> Path {
        var path = Path()
        path.move(to: CGPoint(x: rect.minX, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.maxX, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.maxX, y: rect.maxY - radius))
        path.addQuadCurve(to: CGPoint(x: rect.maxX - radius, y: rect.maxY), control: CGPoint(x: rect.maxX, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.minX + radius, y: rect.maxY))
        path.addQuadCurve(to: CGPoint(x: rect.minX, y: rect.maxY - radius), control: CGPoint(x: rect.minX, y: rect.maxY))
        path.closeSubpath()
        return path
    }
}
