import SwiftUI
import DXManagerCore
import UniformTypeIdentifiers

private func localized(_ key: String) -> String { NSLocalizedString(key, comment: "") }

private enum AppSection: String, CaseIterable, Identifiable {
    case home = "홈", phone = "전화·문자", apps = "앱 바로 실행", transfer = "파일 전송", notifications = "알림", settings = "설정", diagnostics = "진단", about = "정보"
    var id: Self { self }
    var icon: String {
        switch self {
        case .home: return "rectangle.connected.to.line.below"
        case .phone: return "phone.connection"
        case .apps: return "square.grid.2x2"
        case .transfer: return "arrow.up.doc"
        case .notifications: return "bell"
        case .settings: return "slider.horizontal.3"
        case .diagnostics: return "stethoscope"
        case .about: return "info.circle"
        }
    }
}

struct ContentView: View {
    @EnvironmentObject private var model: AppModel
    @State private var section: AppSection? = .home
    @State private var isGlobalDropTarget = false
    @State private var lastRefreshDates: [AppSection: Date] = [:]

    var body: some View {
        NavigationSplitView {
            List(AppSection.allCases, selection: $section) { item in
                Label(localized(item.rawValue), systemImage: item.icon).tag(item)
            }
            .navigationTitle("Flow Bridge")
            .navigationSplitViewColumnWidth(min: 170, ideal: 190, max: 220)
            .safeAreaInset(edge: .bottom) {
                SidebarDeviceControls()
            }
        } detail: {
            VStack(spacing: 0) {
                if section == .phone {
                    VStack(alignment: .leading, spacing: 16) { pageHeader; PhoneView() }
                        .padding(.horizontal, 28).padding(.top, 24).padding(.bottom, 12).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                } else {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 18) {
                            pageHeader
                            switch section ?? .home {
                            case .home: HomeView()
                            case .phone: EmptyView()
                            case .apps: AppsView()
                            case .transfer: TransferView()
                            case .notifications: NotificationsView()
                            case .settings: SettingsView()
                            case .diagnostics: DiagnosticsView()
                            case .about: AboutView()
                            }
                        }.frame(maxWidth: .infinity, alignment: .topLeading).padding(.horizontal, 28).padding(.top, 28).padding(.bottom, 20)
                    }
                }
                statusBar
            }.background(Color(nsColor: .windowBackgroundColor))
        }
        .frame(minWidth: 980, minHeight: 680)
        .controlSize(.regular)
        .overlay {
            if isGlobalDropTarget {
                RoundedRectangle(cornerRadius: 16).strokeBorder(Color.accentColor, style: StrokeStyle(lineWidth: 3, dash: [8]))
                    .background(Color.accentColor.opacity(0.08)).padding(8).allowsHitTesting(false)
            }
        }
        .onDrop(of: [UTType.fileURL], isTargeted: $isGlobalDropTarget, perform: acceptGlobalDrop)
        .onPasteCommand(of: [UTType.fileURL]) { _ in
            if hasConnectedDevice, model.pasteFilesFromClipboard() { section = .transfer }
        }
    }

    private func acceptGlobalDrop(_ providers: [NSItemProvider]) -> Bool {
        guard hasConnectedDevice else { return false }
        loadFileURLs(from: providers) { urls in
            guard !urls.isEmpty else { return }
            section = .transfer
            model.transfer(urls: urls)
        }
        return !providers.isEmpty
    }

    private var pageHeader: some View {
        HStack(alignment: .firstTextBaseline) {
            VStack(alignment: .leading, spacing: 4) {
                Text(localized((section ?? .home).rawValue)).font(.system(size: 28, weight: .bold))
                Text(localized(pageDescription)).font(.subheadline).foregroundStyle(.secondary)
            }
            Spacer()
        }
    }

    private func refreshCurrentSection() {
        lastRefreshDates[section ?? .home] = Date()
        switch section ?? .home {
        case .home, .settings: model.refresh()
        case .phone: model.refreshPhoneData()
        case .apps: model.loadPackages()
        case .transfer: model.loadRemoteFiles()
        case .notifications: model.loadActiveNotifications()
        case .diagnostics: model.loadDiagnostics()
        case .about: model.checkForUpdates()
        }
    }

    private var hasConnectedDevice: Bool {
        model.devices.contains { $0.serial == model.selectedSerial }
    }

    private var refreshNeedsDevice: Bool {
        switch section ?? .home {
        case .phone, .apps, .transfer, .notifications, .settings, .diagnostics: return true
        case .home, .about: return false
        }
    }

    private var statusBar: some View {
        HStack(spacing: 10) {
            if model.isBusy { ProgressView().controlSize(.small) }
            Text(LocalizedStringKey(model.status)).font(.caption).foregroundStyle(.secondary).textSelection(.enabled).lineLimit(1)
            Spacer()
            if model.isTransferring { Text(model.transferStatus).font(.caption); Button("취소", action: model.cancelTransfer).controlSize(.small) }
            TimelineView(.periodic(from: .now, by: 30)) { context in
                Button(action: refreshCurrentSection) {
                    HStack(spacing: 6) { Image(systemName: "arrow.clockwise"); Text(refreshLabel(at: context.date)) }
                        .font(.caption).foregroundStyle(.secondary).padding(.horizontal, 8).frame(height: 28).contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .disabled(model.isBusy || refreshNeedsDevice && !hasConnectedDevice)
                .help(refreshNeedsDevice && !hasConnectedDevice ? "Galaxy를 연결하면 새 정보를 불러올 수 있습니다." : "현재 화면 새로고침")
            }
        }.padding(.horizontal, 16).frame(height: 38).background(.bar).overlay(alignment: .top) { Divider() }
    }

    private func refreshLabel(at now: Date) -> String {
        guard let date = lastRefreshDates[section ?? .home] else { return localized("새로고침") }
        if now.timeIntervalSince(date) < 45 { return localized("방금 업데이트") }
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .full
        return "\(formatter.localizedString(for: date, relativeTo: now)) \(localized("업데이트"))"
    }

    private var pageDescription: String {
        switch section ?? .home {
        case .home: return "Galaxy 연결과 화면 실행을 한곳에서 관리합니다."
        case .phone: return "Galaxy 주소록, 최근 통화와 메시지를 확인하고 상대를 선택해 바로 이어서 작업합니다."
        case .apps: return "지정한 앱을 단축키로 실행하거나 검색해 변경합니다."
        case .transfer: return "파일과 폴더를 Galaxy로 보내거나 Mac으로 가져옵니다."
        case .notifications: return "전화·문자·앱 알림을 Mac 알림 센터로 전달합니다."
        case .settings: return "화면 품질, 자동 연결과 Mac 동작을 설정합니다."
        case .diagnostics: return "기기 정보와 화면 복구 도구 상태를 확인합니다."
        case .about: return "버전, 업데이트, 오픈소스 라이선스와 프로젝트 링크를 확인합니다."
        }
    }
}

private struct SidebarDeviceControls: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(spacing: 9) {
            if let device = model.devices.first(where: { $0.serial == model.selectedSerial }) {
                Menu {
                    ForEach(model.devices) { candidate in Button { model.selectedSerial = candidate.serial; model.applyDeviceSettings() } label: { Label("\(model.deviceLabel(candidate)) · 연결됨", systemImage: "circle.fill") } }
                } label: {
                    HStack { Image(systemName: "iphone.gen3"); VStack(alignment: .leading, spacing: 2) { Text(model.deviceLabel(device)).font(.caption.weight(.semibold)); Text("연결됨 · \(device.connectionName)").font(.caption2).foregroundStyle(.secondary) }; Spacer(); Image(systemName: "chevron.up.chevron.down").font(.caption2) }.frame(maxWidth: .infinity, alignment: .leading).contentShape(Rectangle())
                }.menuStyle(.borderlessButton).buttonStyle(.plain).frame(maxWidth: .infinity)
                HStack(spacing: 8) {
                    Button(action: model.startDeX) { Label("DEX", systemImage: "display") }.disabled(model.sessionPhase != .idle)
                    Button(action: model.startPhoneMirror) { Label("미러링", systemImage: "iphone") }.disabled(model.sessionPhase != .idle)
                }.controlSize(.small)
            } else {
                Label("연결되지 않음", systemImage: "iphone.slash").font(.caption.weight(.semibold)).frame(maxWidth: .infinity, alignment: .leading)
            }
        }.padding(12).background(.ultraThinMaterial)
    }
}

private struct AboutView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("Flow Bridge", icon: "app.badge") {
                HStack {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("버전 0.1.0").font(.title3.weight(.semibold))
                        Text(model.updateStatus).foregroundStyle(model.isUpdateAvailable ? .blue : .secondary)
                    }
                    Spacer(); Button("업데이트 확인", action: model.checkForUpdates).buttonStyle(.borderedProminent)
                }
            }
            Card("오픈소스와 라이선스", icon: "doc.text") {
                Text("Flow Bridge의 macOS 코드는 MIT License로 배포됩니다. maze-mei의 MIT 라이선스 프로젝트 DX Manager를 일부 기반으로 하며, 원저작권 표시와 라이선스 전문을 보존합니다.")
                Text("scrcpy, Android Debug Bridge, SDL, FFmpeg와 동봉 구성요소에는 각각의 오픈소스 라이선스가 적용됩니다.")
                    .foregroundStyle(.secondary)
                HStack(spacing: 12) {
                    Link("Flow Bridge 소스", destination: URL(string: "https://github.com/catgarret/FlowBridge")!)
                    Divider().frame(height: 16)
                    Link("원본 DX Manager", destination: URL(string: "https://github.com/maze-mei/DX-Manager")!)
                    Divider().frame(height: 16)
                    Link("scrcpy", destination: URL(string: "https://github.com/Genymobile/scrcpy")!)
                    Divider().frame(height: 16)
                    Link("전체 라이선스 보기", destination: URL(string: "https://github.com/catgarret/FlowBridge#attribution-licenses-and-trademarks--출처라이선스상표")!)
                }.font(.subheadline)
            }
            Card("독립 프로젝트 고지", icon: "checkmark.shield") {
                Text("Flow Bridge는 독립적으로 개발된 오픈소스 프로젝트이며 Samsung Electronics, Apple, Google, Genymobile 또는 Microsoft와 제휴·후원·보증 관계가 없습니다.")
                    .foregroundStyle(.secondary)
            }
        }
    }
}

private struct HomeView: View {
    @EnvironmentObject private var model: AppModel
    @State private var isEditingAlias = false
    @State private var showsConnectionSetup = false
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Surface {
                VStack(spacing: 12) {
                    if let device = model.devices.first(where: { $0.serial == model.selectedSerial }) {
                        HStack(spacing: 16) {
                            VStack(alignment: .leading, spacing: 5) {
                                if isEditingAlias {
                                    HStack(spacing: 8) { TextField(device.displayName, text: $model.deviceAlias).textFieldStyle(.roundedBorder).frame(maxWidth: 260).onSubmit { model.saveDeviceAlias(); isEditingAlias = false }; Button("완료") { model.saveDeviceAlias(); isEditingAlias = false }.buttonStyle(.borderedProminent) }
                                } else {
                                    Button { isEditingAlias = true } label: { HStack(spacing: 6) { Text(model.deviceAlias.isEmpty ? device.displayName : model.deviceAlias).font(.title3.weight(.semibold)); Image(systemName: "pencil").font(.caption).foregroundStyle(.secondary) } }.buttonStyle(.plain).help("기기 별칭 지정 또는 수정")
                                }
                                HStack(spacing: 6) { Circle().fill(.green).frame(width: 8, height: 8); Text("연결됨").font(.subheadline.weight(.medium)); Text(device.connectionName).font(.caption).padding(.horizontal, 7).padding(.vertical, 2).background(Color.secondary.opacity(0.12), in: Capsule()) }
                                if !model.deviceAlias.isEmpty { Text(device.modelName).font(.caption).foregroundStyle(.secondary) }
                            }
                            Spacer()
                            if model.devices.count > 1 {
                                Picker("기기 전환", selection: $model.selectedSerial) { ForEach(model.devices) { Text(model.deviceLabel($0)).tag($0.serial) } }.frame(maxWidth: 260).onChange(of: model.selectedSerial) { _ in model.applyDeviceSettings() }
                            }
                            DeviceLaunchButtons()
                        }.padding(.vertical, 6)
                        Divider()
                        Button { withAnimation(.easeInOut(duration: 0.18)) { showsConnectionSetup.toggle() } } label: {
                            HStack(spacing: 10) { Image(systemName: "chevron.right").rotationEffect(.degrees(showsConnectionSetup ? 90 : 0)); Text("다른 기기 추가 또는 연결 방식 변경").fontWeight(.medium); Spacer() }.padding(.vertical, 8).contentShape(Rectangle())
                        }.buttonStyle(.plain)
                        if showsConnectionSetup { ConnectionSetupView().padding(.top, 2).transition(.opacity.combined(with: .move(edge: .top))) }
                        Divider().padding(.vertical, 2)
                        ScreenLaunchControls()
                    } else {
                        VStack(spacing: 8) { Image(systemName: "iphone.slash").font(.system(size: 34)).foregroundStyle(.secondary); Text("연결된 Galaxy가 없습니다").font(.title3.weight(.semibold)); Text("USB 케이블로 연결하거나 Galaxy의 무선 디버깅을 사용해 기기를 추가하세요.").font(.caption).foregroundStyle(.secondary) }.frame(maxWidth: .infinity).padding(.vertical, 14)
                        ConnectionSetupView()
                    }
                    if !model.pairingEndpoint.isEmpty {
                        HStack {
                            Label(model.pairingEndpoint, systemImage: "dot.radiowaves.left.and.right")
                            Spacer(); SecureField(localized("6자리 코드"), text: $model.pairingCode).frame(width: 130)
                            Button("페어링 완료", action: model.pairWireless).buttonStyle(.borderedProminent)
                        }.padding(12).background(Color.green.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
                    }
                }
            }
        }
    }
}

private struct ScreenLaunchControls: View {
    @EnvironmentObject private var model: AppModel
    @State private var showBrightnessHelp = false
    private let brightnessHelp = "화면 보호와 온도 제어를 위해 DEX/휴대폰 미러링 실행 시 밝기가 자동으로 최저로 낮아지며 종료 시 원래 밝기로 복원됩니다."
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 6) {
                Text("실행 시 밝기 최저 조절")
                Button { showBrightnessHelp.toggle() } label: { Image(systemName: "questionmark.circle").foregroundStyle(.secondary) }.buttonStyle(.plain).help(brightnessHelp).popover(isPresented: $showBrightnessHelp, arrowEdge: .bottom) { Text(brightnessHelp).frame(width: 280, alignment: .leading).padding(14) }
                Spacer(); Toggle("", isOn: $model.turnPhoneScreenOffOnStart).labelsHidden().toggleStyle(.switch).onChange(of: model.turnPhoneScreenOffOnStart) { _ in model.brightnessSettingChanged() }
            }
            if model.sessionPhase == .running && model.phoneNeedsUnlock { Label("Galaxy가 잠겨 있습니다. 잠금을 해제하면 보호되지 않은 화면이 표시됩니다.", systemImage: "lock.fill").foregroundStyle(.orange) }
        }
    }
}

private struct DeviceLaunchButtons: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        Group {
            if model.sessionPhase == .launching {
                HStack(spacing: 10) { ProgressView(); Text("\(model.activeScreenMode) 여는 중").fontWeight(.medium); Button("취소", role: .destructive, action: model.stop) }.frame(minWidth: 300, alignment: .trailing)
            } else if model.sessionPhase == .running {
                HStack(spacing: 10) { Label("\(model.activeScreenMode) 실행 중", systemImage: "checkmark.circle.fill").foregroundStyle(.green); Button("종료", role: .destructive, action: model.stop).controlSize(.large) }.frame(minWidth: 300, alignment: .trailing)
            } else {
                HStack(spacing: 10) {
                    ScreenModeButton(title: "DEX 모드", subtitle: "데스크톱 화면", icon: "display", action: model.startDeX)
                    ScreenModeButton(title: "휴대폰 미러링", subtitle: "휴대폰 화면", icon: "iphone", action: model.startPhoneMirror)
                }.fixedSize()
            }
        }
    }
}

private struct AppsView: View {
    @EnvironmentObject private var model: AppModel
    @State private var showAppPicker = false
    @State private var editingSlot: Int?
    private var isConnected: Bool { model.devices.contains { $0.serial == model.selectedSerial } }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(spacing: 18) {
                Text("앱 실행 방식").fontWeight(.semibold)
                Spacer()
                Picker("앱 실행 방식", selection: $model.appLaunchMode) {
                    Text("DEX 모드").tag(AppLaunchMode.desktopWindow)
                    Text("휴대폰 미러링 모드").tag(AppLaunchMode.phoneScreen)
                }.pickerStyle(.segmented).labelsHidden().frame(width: 420).onChange(of: model.appLaunchMode) { _ in model.appLaunchModeChanged() }
            }.frame(maxWidth: .infinity, alignment: .trailing)
            Card("앱 바로 실행 지정", icon: "bolt.square") {
                VStack(spacing: 0) {
                    ForEach(0..<3, id: \.self) { slot in
                        let name = model.installedApps.first(where: { $0.package == model.packageNames[slot] })?.name ?? (model.packageNames[slot] == "com.android.settings" ? "설정" : "지정 안 됨")
                        HStack(spacing: 12) {
                            Button { openPicker(for: slot) } label: {
                                HStack(spacing: 14) {
                                    AppIconView(package: model.packageNames[slot], url: model.appIconURLs[model.packageNames[slot]], fallback: model.packageNames[slot].isEmpty ? "" : "\(slot + 1)").onAppear { if isConnected && !model.packageNames[slot].isEmpty { model.requestAppIcon(package: model.packageNames[slot]) } }
                                    VStack(alignment: .leading, spacing: 3) { Text(name).font(.headline).lineLimit(1); Text(model.packageNames[slot].isEmpty ? "클릭해서 앱 선택" : "⌘\(slot + 1) · 클릭해서 변경").font(.caption).foregroundStyle(.secondary) }
                                    Spacer()
                                }.contentShape(Rectangle())
                            }.buttonStyle(.plain)
                            if !model.packageNames[slot].isEmpty {
                                Button("실행") { model.startApp(slot: slot) }.buttonStyle(.borderedProminent).disabled(!isConnected).keyboardShortcut(KeyEquivalent(Character(String(slot + 1))), modifiers: .command)
                            }
                        }.padding(.vertical, 12)
                        if slot < 2 { Divider() }
                    }
                }
            }
            .overlay(alignment: .topTrailing) { Button { openPicker(for: nil) } label: { Label("앱 검색", systemImage: "magnifyingglass") }.buttonStyle(.bordered).padding(.top, 14).padding(.trailing, 18) }
        }
        .sheet(isPresented: $showAppPicker) { AppPickerSheet(targetSlot: editingSlot).environmentObject(model) }
    }

    private func openPicker(for slot: Int?) { editingSlot = slot; model.appSearch = ""; showAppPicker = true }
}

private struct AppPickerSheet: View {
    @EnvironmentObject private var model: AppModel
    @Environment(\.dismiss) private var dismiss
    let targetSlot: Int?
    private var isConnected: Bool { model.devices.contains { $0.serial == model.selectedSerial } }
    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 3) { Text(targetSlot.map { "바로 실행 \($0 + 1) 앱 선택" } ?? "앱 검색").font(.title2.weight(.semibold)); if targetSlot != nil { Text("앱을 선택하면 해당 자리에 바로 지정됩니다.").font(.caption).foregroundStyle(.secondary) } }
                Spacer(); Button("완료") { dismiss() }
            }.padding(.horizontal, 22).padding(.vertical, 18)
            Divider()
            HStack(spacing: 10) { Image(systemName: "magnifyingglass").foregroundStyle(.secondary); TextField("앱 이름 검색", text: $model.appSearch).textFieldStyle(.plain) }.padding(.horizontal, 13).frame(height: 38).background(Color.primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 9)).padding(.horizontal, 22).padding(.vertical, 14)
            let matches = model.installedApps.filter { model.appSearch.isEmpty || $0.name.localizedCaseInsensitiveContains(model.appSearch) || $0.package.localizedCaseInsensitiveContains(model.appSearch) }
            let grouped = Dictionary(grouping: Array(matches.prefix(300))) { indexKey($0.name) }.sorted { $0.key.localizedStandardCompare($1.key) == .orderedAscending }
            ScrollViewReader { proxy in
                ZStack(alignment: .trailing) {
                    List { ForEach(grouped, id: \.key) { letter, apps in Section(letter) { ForEach(apps) { app in appRow(app) } }.id(letter) } }.listStyle(.inset).scrollContentBackground(.hidden).padding(.trailing, model.appSearch.isEmpty ? 30 : 0)
                    if model.appSearch.isEmpty { VStack(spacing: 0) { ForEach(grouped.map(\.key), id: \.self) { letter in Button(letter) { withAnimation { proxy.scrollTo(letter, anchor: .top) } }.buttonStyle(.plain).font(.caption2.weight(.semibold)).foregroundStyle(.secondary).frame(width: 22, height: 16) } }.padding(.vertical, 6).background(.regularMaterial, in: Capsule()).padding(.trailing, 18) }
                }
            }
            Divider(); HStack { Text(targetSlot == nil ? "1·2·3으로 바로 실행 지정" : "바로 실행 \((targetSlot ?? 0) + 1)에 지정").font(.caption).foregroundStyle(.secondary); Spacer(); Text("\(matches.count)개 앱").font(.caption).foregroundStyle(.secondary) }.padding(.horizontal, 22).padding(.vertical, 13)
        }.frame(width: 720, height: 580)
    }

    @ViewBuilder private func appRow(_ app: InstalledApp) -> some View {
        HStack(spacing: 14) {
            Button { select(app) } label: {
                HStack(spacing: 14) { AppIconView(package: app.package, url: model.appIconURLs[app.package], fallback: String(app.name.prefix(1))).onAppear { if isConnected { model.requestAppIcon(package: app.package) } }; VStack(alignment: .leading, spacing: 3) { Text(app.name).fontWeight(.medium); Text(app.package).font(.caption2).foregroundStyle(.secondary).lineLimit(1) }; Spacer(); if targetSlot != nil { Image(systemName: "chevron.right").font(.caption).foregroundStyle(.tertiary) } }.contentShape(Rectangle())
            }.buttonStyle(.plain)
            if targetSlot == nil {
                HStack(spacing: 6) { ForEach(0..<3) { slot in assignmentButton(app: app, slot: slot) } }
                Button("실행") { model.startApp(package: app.package) }.disabled(!isConnected)
            }
        }.padding(.vertical, 7)
    }

    @ViewBuilder private func assignmentButton(app: InstalledApp, slot: Int) -> some View {
        let selected = model.packageNames[slot] == app.package
        Button { model.toggleFavorite(package: app.package, slot: slot) } label: { Text("\(slot + 1)").fontWeight(.semibold).frame(width: 28, height: 24).background(selected ? Color.accentColor : Color.primary.opacity(0.07), in: RoundedRectangle(cornerRadius: 6)).foregroundStyle(selected ? Color.white : Color.primary) }.buttonStyle(.plain).help("⌘\(slot + 1) 바로 실행 지정")
    }

    private func select(_ app: InstalledApp) { guard let targetSlot else { return }; model.assignFavorite(package: app.package, slot: targetSlot); dismiss() }

    private func indexKey(_ name: String) -> String {
        guard let scalar = name.unicodeScalars.first else { return "#" }
        let value = Int(scalar.value)
        if (0xAC00...0xD7A3).contains(value) {
            let initials = ["ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"]
            return initials[(value - 0xAC00) / 588]
        }
        let letter = String(scalar).uppercased()
        return letter.range(of: "^[A-Z]$", options: .regularExpression) == nil ? "#" : letter
    }
}

private struct ConnectionOption<Accessory: View>: View {
    let icon: String; let title: String; let subtitle: String; @ViewBuilder let accessory: Accessory
    init(icon: String, title: String, subtitle: String, @ViewBuilder accessory: () -> Accessory) { self.icon = icon; self.title = title; self.subtitle = subtitle; self.accessory = accessory() }
    var body: some View { HStack(spacing: 14) { Image(systemName: icon).font(.title3).foregroundStyle(.blue).frame(width: 30); VStack(alignment: .leading, spacing: 4) { Text(title).fontWeight(.semibold); Text(subtitle).font(.caption).foregroundStyle(.secondary) }; Spacer(); accessory }.padding(.vertical, 14).padding(.horizontal, 4) }
}

private struct ConnectionSetupView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(spacing: 0) {
            ConnectionOption(icon: "cable.connector", title: "USB로 기기 추가", subtitle: "Galaxy에서 이 Mac의 USB 디버깅을 허용한 뒤 무선 연결로 전환합니다.") { Button("USB 기기 추가", action: model.prepareWirelessFromUSB).buttonStyle(.borderedProminent) }
            Divider()
            ConnectionOption(icon: "wifi", title: "무선으로 기기 추가", subtitle: "Galaxy의 무선 디버깅 페어링 화면에서 기기를 검색합니다.") { Button("기기 검색", action: model.discoverWirelessSetup) }
            Divider()
            VStack(alignment: .leading, spacing: 12) {
                HStack(spacing: 14) {
                    Image(systemName: "network").font(.title3).foregroundStyle(.blue).frame(width: 30)
                    VStack(alignment: .leading, spacing: 4) { Text("IP 주소로 직접 연결").fontWeight(.semibold); Text("자동 검색이 어려울 때 무선 ADB 주소나 페어링 정보를 입력합니다.").font(.caption).foregroundStyle(.secondary) }
                }
                HStack { TextField(localized("무선 ADB 주소  예: 172.30.1.3:44065"), text: $model.wirelessEndpoint); Button("직접 연결", action: model.connectWireless) }
                HStack { TextField(localized("페어링 IP:포트"), text: $model.pairingEndpoint); SecureField(localized("6자리 코드"), text: $model.pairingCode).frame(width: 120); Button("직접 페어링", action: model.pairWireless) }
            }.padding(.vertical, 14).padding(.horizontal, 4)
        }
    }
}

private struct PhoneView: View {
    @EnvironmentObject private var model: AppModel
    @State private var tab = 0
    @State private var callSource = 0
    @State private var selectedNumber = ""
    @State private var selectedRowID = ""
    @State private var showDialPad = false
    @State private var showCallToast = false
    private var isConnected: Bool { model.devices.contains { $0.serial == model.selectedSerial } }
    var body: some View {
        VStack(spacing: 0) {
            CompactTabSwitcher(selection: $tab, items: [("통화", "phone.fill"), ("메시지", "message.fill")])
                .padding(.bottom, 16)
            Divider()
            HStack(spacing: 0) {
                VStack(spacing: 0) {
                    if tab == 0 {
                        HStack(spacing: 10) {
                            Picker("통화 목록", selection: $callSource) { Text("최근 통화").tag(0); Text("연락처").tag(1) }.pickerStyle(.segmented).labelsHidden()
                            Button { model.phoneNumber = ""; showDialPad = true } label: { Image(systemName: "circle.grid.3x3.fill") }.buttonStyle(.bordered).help("다이얼 열기")
                        }.frame(height: 34).padding(.horizontal, 16).padding(.top, 12).padding(.bottom, 10)
                    }
                    HStack(spacing: 9) {
                        Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
                        TextField("이름, 번호 또는 내용 검색", text: $model.phoneSearch).textFieldStyle(.plain)
                    }.padding(.horizontal, 11).frame(height: 36).background(Color.primary.opacity(0.055), in: RoundedRectangle(cornerRadius: 8)).overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.primary.opacity(0.08))).padding(.horizontal, 16).padding(.top, tab == 0 ? 0 : 12).padding(.bottom, 12)
                    Divider()
                    if tab == 0 { callSource == 0 ? AnyView(callList) : AnyView(contactList) } else { messageThreadList }
                }.frame(width: 360)
                Divider()
                if tab == 0 && showDialPad { dialPad } else if selectedNumber.isEmpty { emptyDetail } else if tab == 0 { callDetail } else { messageDetail }
            }.frame(maxWidth: .infinity, maxHeight: .infinity)
        }.frame(maxWidth: .infinity, maxHeight: .infinity)
            .overlay(alignment: .top) { if showCallToast { Label("Galaxy에서 통화를 확인해 주세요.", systemImage: "iphone.gen3").padding(.horizontal, 16).padding(.vertical, 10).background(.regularMaterial, in: Capsule()).shadow(radius: 8).padding(.top, 8).transition(.move(edge: .top).combined(with: .opacity)) } }
            .onAppear { if isConnected && model.contacts.isEmpty { model.refreshPhoneData() } }
    }

    private var callList: some View {
        let filtered = model.recentCalls.filter { model.phoneSearch.isEmpty || $0.number.contains(model.phoneSearch) || contactName($0.number).localizedCaseInsensitiveContains(model.phoneSearch) }
        return List(filtered.prefix(100)) { call in
            HStack(spacing: 12) { ContactAvatar(name: contactName(call.number), photoURL: contactPhoto(call.number)); VStack(alignment: .leading, spacing: 5) { Text(contactName(call.number)).fontWeight(.medium); HStack(spacing: 4) { Image(systemName: call.type == 1 ? "phone.arrow.down.left" : call.type == 2 ? "phone.arrow.up.right" : "phone.down"); Text(call.number) }.font(.caption).foregroundStyle(call.type == 3 ? .red : .secondary) }; Spacer(); Text(call.date, style: .relative).font(.caption2).foregroundStyle(.secondary).help(exactDate(call.date)) }.padding(.vertical, 7).contentShape(Rectangle()).onTapGesture { select(call.number, rowID: call.id) }.listRowBackground(selectedRowID == call.id ? Color.accentColor.opacity(0.14) : Color.clear)
        }.listStyle(.plain).scrollContentBackground(.hidden).background(Color(nsColor: .windowBackgroundColor))
    }

    private var contactList: some View {
        let filtered = model.contacts.filter { model.phoneSearch.isEmpty || $0.number.contains(model.phoneSearch) || $0.name.localizedCaseInsensitiveContains(model.phoneSearch) }.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
        let grouped = Dictionary(grouping: Array(filtered.prefix(200))) { contactIndexKey($0.name) }.sorted { $0.key.localizedStandardCompare($1.key) == .orderedAscending }
        return ScrollViewReader { proxy in
            ZStack(alignment: .trailing) {
                List { ForEach(grouped, id: \.key) { letter, contacts in Section(letter) { ForEach(contacts) { contact in HStack(spacing: 12) { ContactAvatar(name: contact.name, photoURL: model.contactPhotoURL(for: contact.number)); VStack(alignment: .leading, spacing: 5) { Text(contact.name).fontWeight(.medium); Text(contact.number).font(.caption).foregroundStyle(.secondary) }; Spacer(); Image(systemName: "chevron.right").font(.caption).foregroundStyle(.tertiary) }.padding(.vertical, 7).contentShape(Rectangle()).onTapGesture { select(contact.number, rowID: contact.id) }.listRowBackground(selectedRowID == contact.id ? Color.accentColor.opacity(0.14) : Color.clear) } }.id(letter) } }
                    .listStyle(.plain).scrollContentBackground(.hidden).background(Color(nsColor: .windowBackgroundColor)).padding(.trailing, model.phoneSearch.isEmpty ? 22 : 0)
                if model.phoneSearch.isEmpty { VStack(spacing: 0) { ForEach(grouped.map(\.key), id: \.self) { letter in Button(letter) { withAnimation { proxy.scrollTo(letter, anchor: .top) } }.buttonStyle(.plain).font(.caption2.weight(.semibold)).foregroundStyle(.secondary).frame(width: 18, height: 15) } }.padding(.vertical, 5).background(.regularMaterial, in: RoundedRectangle(cornerRadius: 8)).padding(.trailing, 4) }
            }
        }
    }

    private var messageThreadList: some View {
        let latest = Dictionary(grouping: model.recentMessages, by: \.address).compactMap { $0.value.max(by: { $0.date < $1.date }) }.sorted { $0.date > $1.date }
        let filtered = latest.filter { model.phoneSearch.isEmpty || $0.address.contains(model.phoneSearch) || $0.body.localizedCaseInsensitiveContains(model.phoneSearch) || contactName($0.address).localizedCaseInsensitiveContains(model.phoneSearch) }
        return List(filtered) { message in
            HStack(spacing: 12) { ContactAvatar(name: contactName(message.address), photoURL: contactPhoto(message.address)); VStack(alignment: .leading, spacing: 5) { HStack { Text(contactName(message.address)).fontWeight(.medium).lineLimit(1); Spacer(); Text(message.date, style: .relative).font(.caption2).foregroundStyle(.secondary).help(exactDate(message.date)) }; Text(message.body.replacingOccurrences(of: "\n", with: " ")).lineLimit(1).truncationMode(.tail).font(.caption).foregroundStyle(.secondary) } }.padding(.vertical, 7).contentShape(Rectangle()).onTapGesture { select(message.address, rowID: message.address) }.listRowBackground(selectedRowID == message.address ? Color.accentColor.opacity(0.14) : Color.clear)
        }.listStyle(.plain).scrollContentBackground(.hidden).background(Color(nsColor: .windowBackgroundColor))
    }

    private var emptyDetail: some View {
        VStack(spacing: 12) { Image(systemName: tab == 0 ? "phone.circle" : "message.circle").font(.system(size: 54)).foregroundStyle(.secondary); Text(tab == 0 ? "통화 내역이나 연락처를 선택하세요" : "대화를 선택하세요").font(.title3.weight(.semibold)); Text("왼쪽 목록에서 상대를 선택하면 상세 기능이 표시됩니다.").foregroundStyle(.secondary) }.frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var callDetail: some View {
        if showDialPad { return AnyView(dialPad) }
        let calls = model.recentCalls.filter { $0.number == selectedNumber }.sorted { $0.date > $1.date }
        return AnyView(VStack(spacing: 0) {
            VStack(spacing: 8) { ContactAvatar(name: contactName(selectedNumber), photoURL: contactPhoto(selectedNumber), large: true); Text(contactName(selectedNumber)).font(.title2.weight(.semibold)); Text(selectedNumber).foregroundStyle(.secondary); HStack { Button { tab = 1 } label: { Label("메시지", systemImage: "message") }; Button { model.phoneNumber = selectedNumber; showDialPad = true } label: { Label("전화", systemImage: "phone.fill") }.buttonStyle(.borderedProminent).disabled(!isConnected) } }.padding(24)
            Divider()
            HStack { Text("통화 기록").font(.headline); Spacer(); Text("\(calls.count)건").foregroundStyle(.secondary) }.padding(14)
            List(calls) { call in HStack(spacing: 12) { Image(systemName: call.type == 1 ? "phone.arrow.down.left" : call.type == 2 ? "phone.arrow.up.right" : "phone.down").foregroundStyle(call.type == 3 ? .red : .secondary); VStack(alignment: .leading, spacing: 3) { Text(call.type == 1 ? "수신 통화" : call.type == 2 ? "발신 통화" : "부재중 통화"); Text(call.date.formatted(date: .abbreviated, time: .shortened)).font(.caption).foregroundStyle(.secondary) }; Spacer(); Text(durationText(call.duration)).font(.caption).foregroundStyle(.secondary) } }.listStyle(.inset)
        }.frame(maxWidth: .infinity))
    }

    private var dialPad: some View {
        let keys = [("1", ""), ("2", "ABC"), ("3", "DEF"), ("4", "GHI"), ("5", "JKL"), ("6", "MNO"), ("7", "PQRS"), ("8", "TUV"), ("9", "WXYZ"), ("*", ""), ("0", "+"), ("#", "")]
        return VStack(spacing: 16) {
            HStack { Text("다이얼").font(.title2.weight(.semibold)); Spacer(); Button { showDialPad = false } label: { Image(systemName: "xmark.circle.fill").font(.title2).foregroundStyle(.secondary) }.buttonStyle(.plain) }
            HStack(spacing: 8) {
                TextField("전화번호", text: $model.phoneNumber).textFieldStyle(.plain).font(.system(size: 28, weight: .medium, design: .rounded)).multilineTextAlignment(.center).frame(width: 280)
                Button { if !model.phoneNumber.isEmpty { model.phoneNumber.removeLast() } } label: { Image(systemName: "delete.left") }.buttonStyle(.plain).foregroundStyle(.secondary).disabled(model.phoneNumber.isEmpty)
            }.padding(.vertical, 10).overlay(alignment: .bottom) { Divider() }
            LazyVGrid(columns: Array(repeating: GridItem(.fixed(68)), count: 3), spacing: 12) {
                ForEach(keys, id: \.0) { digit, letters in
                    Button { model.phoneNumber += digit } label: { VStack(spacing: 1) { Text(digit).font(.system(size: 25, weight: .medium, design: .rounded)); Text(letters).font(.system(size: 8, weight: .semibold)).tracking(1) }.frame(width: 58, height: 58).background(Color.primary.opacity(0.08), in: Circle()).contentShape(Circle()) }.buttonStyle(.plain)
                }
            }
            Button(action: placeCall) { Image(systemName: "phone.fill").font(.title2).foregroundStyle(.white).frame(width: 62, height: 62).background(Color.green, in: Circle()).shadow(color: .green.opacity(0.25), radius: 8, y: 3) }.buttonStyle(.plain).disabled(model.phoneNumber.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !isConnected).help("전화")
            Text("전화").font(.caption).foregroundStyle(.secondary)
            Spacer(minLength: 0)
        }.padding(28).frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var messageDetail: some View {
        let conversation = model.recentMessages.filter { $0.address == selectedNumber }.sorted { $0.date < $1.date }
        return VStack(spacing: 0) {
            HStack(spacing: 10) { ContactAvatar(name: contactName(selectedNumber), photoURL: contactPhoto(selectedNumber)); VStack(alignment: .leading) { Text(contactName(selectedNumber)).fontWeight(.semibold); Text(selectedNumber).font(.caption).foregroundStyle(.secondary) }; Spacer(); Button(action: model.openDialer) { Image(systemName: "phone") }.disabled(!isConnected).help("전화 화면 열기") }.padding(14)
            Divider()
            ScrollViewReader { proxy in ScrollView { LazyVStack(spacing: 12) { ForEach(conversation) { message in HStack { if message.isOutgoing { Spacer(minLength: 96) }; VStack(alignment: message.isOutgoing ? .trailing : .leading, spacing: 5) { Text(message.body).textSelection(.enabled); Text(message.date, style: .time).font(.caption2).opacity(0.7) }.foregroundStyle(message.isOutgoing ? Color.white : Color.primary).padding(.horizontal, 13).padding(.vertical, 10).background(message.isOutgoing ? Color.green : Color.secondary.opacity(0.14), in: RoundedRectangle(cornerRadius: 16)); if !message.isOutgoing { Spacer(minLength: 96) } }.id(message.id) } }.padding(18) }.onAppear { if let last = conversation.last { proxy.scrollTo(last.id, anchor: .bottom) } } }
            Divider()
            HStack(alignment: .bottom, spacing: 10) { TextEditor(text: $model.messageBody).frame(minHeight: 38, maxHeight: 90).padding(5).background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 8)).overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.primary.opacity(0.1))).disabled(!isConnected); Button(action: model.composeMessage) { Image(systemName: "paperplane.fill") }.buttonStyle(.borderedProminent).controlSize(.large).disabled(!isConnected || model.messageBody.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty).help("Galaxy 메시지 앱에서 확인") }.padding(12)
        }.frame(maxWidth: .infinity)
    }

    private func select(_ number: String, rowID: String) { selectedNumber = number; selectedRowID = rowID; showDialPad = false; model.selectPhoneNumber(number) }
    private func placeCall() {
        model.openDialer()
        withAnimation { showCallToast = true }
        Task { try? await Task.sleep(nanoseconds: 3_000_000_000); await MainActor.run { withAnimation { showCallToast = false } } }
    }
    private func contactName(_ number: String) -> String { model.contacts.first(where: { normalized($0.number) == normalized(number) })?.name ?? number }
    private func contactPhoto(_ number: String) -> URL? { model.contactPhotoURL(for: number) }
    private func normalized(_ number: String) -> String { number.filter(\.isNumber).suffix(10).description }
    private func durationText(_ seconds: Int) -> String { seconds == 0 ? "연결 안 됨" : seconds >= 60 ? "\(seconds / 60)분 \(seconds % 60)초" : "\(seconds)초" }
    private func exactDate(_ date: Date) -> String { date.formatted(date: .complete, time: .standard) }
    private func contactIndexKey(_ name: String) -> String {
        guard let scalar = name.unicodeScalars.first else { return "#" }
        let value = Int(scalar.value)
        if (0xAC00...0xD7A3).contains(value) { return ["ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"][(value - 0xAC00) / 588] }
        let letter = String(scalar).uppercased()
        return letter.range(of: "^[A-Z]$", options: .regularExpression) == nil ? "#" : letter
    }
}

private struct ContactAvatar: View {
    let name: String; var photoURL: URL? = nil; var large = false
    private var tint: Color { [.indigo, .teal, .orange, .pink, .purple][Int(UInt(bitPattern: name.hashValue) % 5)] }
    var body: some View {
        Group {
            if let photoURL, let image = NSImage(contentsOf: photoURL) {
                Image(nsImage: image).resizable().scaledToFill()
            } else {
                ZStack { Circle().fill(tint.opacity(0.14)); Text(String(name.prefix(1))).font(large ? .title : .headline).fontWeight(.semibold).foregroundStyle(tint) }
            }
        }.frame(width: large ? 72 : 38, height: large ? 72 : 38).clipShape(Circle())
    }
}

private struct AppIconView: View {
    let package: String; let url: URL?; let fallback: String; var systemFallback = "app.fill"
    var body: some View {
        Group {
            if let url, let image = NSImage(contentsOf: url) { Image(nsImage: image).resizable().scaledToFit() }
            else { ZStack { RoundedRectangle(cornerRadius: 9).fill(Color.secondary.opacity(0.12)); if fallback.isEmpty { Image(systemName: systemFallback).foregroundStyle(.secondary) } else { Text(fallback).font(.headline).foregroundStyle(.secondary) } } }
        }.frame(width: 38, height: 38).clipShape(RoundedRectangle(cornerRadius: 9))
    }
}

private struct TransferView: View {
    @EnvironmentObject private var model: AppModel
    @State private var selectedRemote: RemoteFile?
    @State private var isDropTarget = false
    @State private var direction = 0
    private var isConnected: Bool { model.devices.contains { $0.serial == model.selectedSerial } }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            CompactTabSwitcher(selection: $direction, items: [("Galaxy로 보내기", "arrow.up.circle.fill"), ("Mac으로 가져오기", "arrow.down.circle.fill")])
            if direction == 0 {
                Card("Galaxy로 보내기", icon: "arrow.up.circle") {
                VStack(spacing: 14) {
                    Image(systemName: isDropTarget ? "arrow.down.doc.fill" : "doc.on.doc").font(.system(size: 38)).foregroundStyle(.blue)
                    Text("Finder 파일을 여기에 놓거나, Finder에서 ⌘C한 뒤 아래 버튼을 누르세요.").foregroundStyle(.secondary)
                    HStack {
                        Button(action: model.chooseAndTransfer) { Label("파일·폴더 선택", systemImage: "plus") }.buttonStyle(.borderedProminent)
                        Button { _ = model.pasteFilesFromClipboard() } label: { Label("Mac 클립보드 붙여넣기", systemImage: "doc.on.clipboard") }
                    }
                    if model.isTransferring { ProgressView().frame(maxWidth: 420); Text(model.transferStatus).font(.caption); Button("전송 취소", action: model.cancelTransfer) }
                }.frame(maxWidth: .infinity).padding(.vertical, 22)
                    .background(isDropTarget ? Color.blue.opacity(0.12) : Color.clear, in: RoundedRectangle(cornerRadius: 10))
                    .onDrop(of: [UTType.fileURL], isTargeted: $isDropTarget, perform: acceptDrop)
                    .disabled(!isConnected)
                    .overlay { if !isConnected { DisconnectedOverlay() } }
                }
            } else {
                Card("Mac으로 가져오기", icon: "arrow.down.circle") {
                    if model.remoteFiles.isEmpty {
                        VStack(spacing: 10) {
                            Image(systemName: "folder").font(.system(size: 34)).foregroundStyle(.blue)
                            Text("불러온 파일 없음").fontWeight(.medium)
                            Text(isConnected ? "하단 새로고침을 눌러 Galaxy의 Download 폴더를 확인하세요." : "Galaxy를 연결하면 파일 목록을 불러올 수 있습니다.").font(.caption).foregroundStyle(.secondary)
                        }.frame(maxWidth: .infinity, minHeight: 180)
                    } else {
                        List(model.remoteFiles, selection: $selectedRemote) { file in
                            Label(file.name, systemImage: file.isDirectory ? "folder" : "doc").tag(file)
                        }.frame(minHeight: 240)
                    }
                    HStack {
                        Spacer()
                        Button("Mac에 저장") { if let selectedRemote { model.downloadRemoteFile(selectedRemote) } }
                            .buttonStyle(.borderedProminent).disabled(selectedRemote == nil || !isConnected)
                    }
                }
            }
        }
    }

    private func acceptDrop(_ providers: [NSItemProvider]) -> Bool {
        guard isConnected else { return false }
        loadFileURLs(from: providers) { model.transfer(urls: $0) }
        return !providers.isEmpty
    }
}

private func loadFileURLs(from providers: [NSItemProvider], completion: @escaping ([URL]) -> Void) {
    let group = DispatchGroup()
    let collector = URLCollector()
    for provider in providers {
        group.enter()
        provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, _ in
            defer { group.leave() }
            let url: URL?
            if let data = item as? Data { url = URL(dataRepresentation: data, relativeTo: nil) }
            else if let value = item as? URL { url = value }
            else if let value = item as? NSURL { url = value as URL }
            else { url = nil }
            if let url { collector.append(url) }
        }
    }
    group.notify(queue: .main) { completion(collector.values) }
}

private final class URLCollector: @unchecked Sendable {
    private let lock = NSLock()
    private var storage: [URL] = []
    func append(_ url: URL) { lock.lock(); storage.append(url); lock.unlock() }
    var values: [URL] { lock.lock(); defer { lock.unlock() }; return storage }
}

private struct NotificationsView: View {
    @EnvironmentObject private var model: AppModel
    @State private var tab = 0
    private var isConnected: Bool { model.devices.contains { $0.serial == model.selectedSerial } }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            CompactTabSwitcher(selection: $tab, items: [("Galaxy 알림", "bell.fill"), ("알림 설정", "slider.horizontal.3")])
            if tab == 1 { Card("알림 설정", icon: "bell.badge") {
                VStack(spacing: 0) {
                    NotificationRow(title: "전화", subtitle: "Galaxy 수신 전화 알림", icon: "phone", isOn: $model.phoneNotificationsEnabled)
                    Divider(); NotificationRow(title: "문자", subtitle: "Samsung 메시지와 Google 메시지", icon: "message", isOn: $model.messageNotificationsEnabled)
                    Divider(); NotificationRow(title: "애플리케이션", subtitle: "Galaxy의 나머지 앱 알림", icon: "app.badge", isOn: $model.appNotificationsEnabled)
                }
                .onChange(of: model.phoneNotificationsEnabled) { _ in model.notificationSettingsChanged() }
                .onChange(of: model.messageNotificationsEnabled) { _ in model.notificationSettingsChanged() }
                .onChange(of: model.appNotificationsEnabled) { _ in model.notificationSettingsChanged() }
            } } else { Card("Galaxy 알림", icon: "iphone.and.arrow.forward") {
                HStack { Text("Galaxy 알림창에 남아 있는 항목").foregroundStyle(.secondary); Spacer(); Button("모두 지우기", role: .destructive, action: model.dismissAllNotifications).disabled(model.activeNotifications.isEmpty || !isConnected) }
                if model.activeNotifications.isEmpty { VStack(spacing: 8) { Image(systemName: "bell.slash").font(.system(size: 30)).foregroundStyle(.secondary); Text("남아 있는 알림 없음").foregroundStyle(.secondary) }.frame(maxWidth: .infinity).padding(28) }
                else { VStack(spacing: 0) { ForEach(model.activeNotifications) { item in HStack(spacing: 14) { AppIconView(package: item.package, url: model.appIconURLs[item.package], fallback: "", systemFallback: item.kind == .call ? "phone.fill" : item.kind == .message ? "message.fill" : "app.fill").onAppear { if isConnected { model.requestAppIcon(package: item.package) } }; VStack(alignment: .leading, spacing: 4) { Text(item.title.isEmpty ? item.package : item.title).fontWeight(.semibold); Text(item.body).font(.subheadline).foregroundStyle(.secondary).lineLimit(2); Text(item.package).font(.caption2).foregroundStyle(.tertiary) }; Spacer(); Button { model.dismissNotification(item) } label: { Image(systemName: "xmark.circle.fill").foregroundStyle(.secondary) }.buttonStyle(.plain).disabled(!isConnected).help("Galaxy에서 알림 지우기") }.padding(.vertical, 13); Divider() } } }
                if !model.notificationDeliveryStatus.isEmpty { Text(model.notificationDeliveryStatus).font(.caption).foregroundStyle(.secondary) }
            } }
        }.onAppear { if isConnected && tab == 0 { model.loadActiveNotifications() } }
            .onChange(of: tab) { value in if value == 0 && isConnected { model.loadActiveNotifications() } }
    }
}

private struct DisconnectedOverlay: View {
    var body: some View {
        VStack(spacing: 8) {
            Image(systemName: "iphone.slash").font(.title2)
            Text("Galaxy 연결 필요").fontWeight(.semibold)
            Text("기기를 연결하면 이 작업을 사용할 수 있습니다.").font(.caption)
        }.foregroundStyle(.secondary).padding(18).background(.regularMaterial, in: RoundedRectangle(cornerRadius: 12))
    }
}

private struct SettingsView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 26) {
            SettingsGroup("화면 품질", icon: "display") {
                VStack(alignment: .leading, spacing: 14) {
                    Grid(alignment: .leading, horizontalSpacing: 18, verticalSpacing: 14) {
                        GridRow {
                            Text("해상도").fontWeight(.medium).frame(width: 72, alignment: .leading)
                            HStack(spacing: 8) {
                                PresetButton(title: "720p", selected: model.settings.width == 1280 && model.settings.height == 720) { model.applyDisplayPreset(width: 1280, height: 720) }
                                PresetButton(title: "1080p", selected: model.settings.width == 1920 && model.settings.height == 1080) { model.applyDisplayPreset(width: 1920, height: 1080) }
                                PresetButton(title: model.nativeDisplayPresetTitle, selected: model.isNativeDisplayPresetSelected, action: model.applyNativeDisplayPreset)
                            }.frame(width: 360, alignment: .leading)
                        }
                        GridRow {
                            Text("프레임").fontWeight(.medium).frame(width: 72, alignment: .leading)
                            HStack(spacing: 8) { PresetButton(title: "30 FPS", selected: model.settings.fps == 30) { model.settings.fps = 30 }; PresetButton(title: "60 FPS", selected: model.settings.fps == 60) { model.settings.fps = 60 }; PresetButton(title: "120 FPS", selected: model.settings.fps == 120) { model.settings.fps = 120 } }.frame(width: 360, alignment: .leading)
                        }
                    }
                    Text("60 FPS가 기본 권장값입니다. 120 FPS는 기기·연결·Mac 성능에 따라 실제 프레임이 낮아질 수 있습니다.").font(.caption).foregroundStyle(.secondary)
                    DisclosureGroup("전문가 수동 설정") {
                        Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 12) {
                            GridRow { Text("해상도"); HStack { NumberField(value: $model.settings.width, label: "너비"); Text("×"); NumberField(value: $model.settings.height, label: "높이") } }
                            GridRow { Text("화면 밀도"); NumberField(value: $model.settings.dpi, label: "DPI") }
                            GridRow { Text("비트레이트"); NumberField(value: $model.settings.bitrate, label: "Mbps") }
                        }.padding(.top, 10)
                    }
                }
                HStack { Spacer(); Button("설정 저장", action: model.save).buttonStyle(.borderedProminent) }
            }
            SettingsGroup("연결과 Mac 동작", icon: "gearshape") {
                VStack(alignment: .leading, spacing: 0) {
                    SettingsRow("마지막 무선 주소로 자동 재연결") { Toggle("", isOn: $model.automaticReconnect).labelsHidden() }
                    Divider()
                    SettingsRow("Flow Bridge 로그인 자동 실행") { Toggle("", isOn: Binding(get: { model.launchAtLogin }, set: { value in if value != model.launchAtLogin { model.toggleLaunchAtLogin() } })).labelsHidden() }
                    Divider()
                    SettingsRow("앱 표시 위치") {
                        HStack(spacing: 6) {
                            PresenceButton("Dock + 메뉴 막대", value: .dockAndMenuBar)
                            PresenceButton("메뉴 막대만", value: .menuBarOnly)
                            PresenceButton("Dock만", value: .dockOnly)
                        }.frame(width: 390)
                    }
                    Divider()
                    SettingsRow("앱을 시작할 때 메인 창 열기") { Toggle("", isOn: $model.openMainWindowAtLaunch).labelsHidden().onChange(of: model.openMainWindowAtLaunch) { _ in model.presentationSettingsChanged() } }
                    Divider()
                    SettingsRow("화면 제어 바 위치") {
                        Picker("화면 제어 바 위치", selection: $model.controlBarPosition) { Text("화면 상단").tag(ControlBarPosition.top); Text("화면 하단").tag(ControlBarPosition.bottom) }.pickerStyle(.segmented).labelsHidden().frame(maxWidth: 280).onChange(of: model.controlBarPosition) { _ in model.controlBarPositionChanged() }
                    }
                    Divider()
                    SettingsRow("사용하지 않을 때 자동 숨김") { HStack(spacing: 8) { TextField("분", value: $model.autoHideMinutes, format: .number).frame(width: 54); Text("분 · 0이면 사용 안 함").foregroundStyle(.secondary) } }
                    Divider()
                    SettingsRow("키보드 입력") { HStack(spacing: 8) {
                        Button(model.keyboardCorrectionEnabled ? "오른쪽 Shift 보정 끄기" : "오른쪽 Shift 보정 켜기", action: model.toggleKeyboardCorrection)
                        Button(model.shiftEnterMode ? "일반 Enter로 전환" : "Shift+Enter로 전환", action: model.toggleShiftEnter)
                    } }
                }.toggleStyle(.switch)
            }
        }
    }
}

private struct DiagnosticsView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("선택 기기", icon: "info.circle") {
                HStack(alignment: .top) {
                    Text(model.diagnostics.isEmpty ? "갱신을 눌러 기기와 실행 환경을 확인하세요." : model.diagnostics).font(.system(.body, design: .monospaced)).textSelection(.enabled)
                    Spacer(); Button("로그 저장", action: model.saveLog).buttonStyle(.borderedProminent)
                }
            }
            Card("화면 복구 도구", icon: "cross.case") {
                Text("비정상 종료 후 Galaxy에 남은 데스크톱 가상 화면을 안전하게 복구합니다.").foregroundStyle(.secondary)
                HStack {
                    Button("설치·업데이트", action: model.installCompanion).buttonStyle(.borderedProminent)
                    Button("복구 권한 부여", action: model.grantCompanionPermission)
                    Spacer(); Button("삭제", role: .destructive, action: model.uninstallCompanion)
                }
                Text("번들 공식 v2.0.0 APK의 SHA-256을 설치 전후에 검증합니다.").font(.caption).foregroundStyle(.secondary)
            }
        }
    }
}

private struct CompactTabSwitcher: View {
    @Binding var selection: Int
    let items: [(String, String)]
    var body: some View {
        HStack(spacing: 4) {
            ForEach(Array(items.enumerated()), id: \.offset) { index, item in
                Button { withAnimation(.easeInOut(duration: 0.15)) { selection = index } } label: {
                    HStack(spacing: 8) {
                        Image(systemName: item.1)
                        Text(localized(item.0)).fontWeight(.semibold)
                    }
                    .foregroundStyle(selection == index ? Color.white : Color.secondary)
                    .frame(maxWidth: .infinity, minHeight: 38)
                    .background(selection == index ? Color.accentColor : Color.clear, in: RoundedRectangle(cornerRadius: 8))
                    .contentShape(RoundedRectangle(cornerRadius: 8))
                }.buttonStyle(.plain)
            }
        }
        .padding(4)
        .frame(maxWidth: .infinity)
        .background(Color.primary.opacity(0.055), in: RoundedRectangle(cornerRadius: 11))
        .overlay(RoundedRectangle(cornerRadius: 11).stroke(Color.primary.opacity(0.08)))
    }
}

private struct Card<Content: View>: View {
    let title: String; let icon: String; @ViewBuilder let content: Content
    init(_ title: String, icon: String, @ViewBuilder content: () -> Content) { self.title = title; self.icon = icon; self.content = content() }
    var body: some View {
        VStack(alignment: .leading, spacing: 16) { Label(localized(title), systemImage: icon).font(.headline); content }
            .padding(20).frame(maxWidth: .infinity, alignment: .leading)
            .background(Color.primary.opacity(0.028), in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.primary.opacity(0.09), lineWidth: 1))
    }
}

private struct ScreenModeButton: View {
    let title: String; let subtitle: String; let icon: String; let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 10) {
                Image(systemName: icon).font(.title3).foregroundStyle(Color.accentColor).frame(width: 24)
                VStack(alignment: .leading, spacing: 2) { Text(title).fontWeight(.semibold); Text(subtitle).font(.caption2).foregroundStyle(.secondary) }
            }.frame(width: 142, alignment: .leading).padding(.vertical, 5).contentShape(Rectangle())
        }.buttonStyle(.bordered).controlSize(.large)
    }
}

private struct Surface<Content: View>: View {
    @ViewBuilder let content: Content
    init(@ViewBuilder content: () -> Content) { self.content = content() }
    var body: some View { content.padding(20).frame(maxWidth: .infinity, alignment: .leading).background(Color.primary.opacity(0.028), in: RoundedRectangle(cornerRadius: 14)).overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.primary.opacity(0.09))) }
}

private struct PresenceButton: View {
    @EnvironmentObject private var model: AppModel
    let title: String; let value: AppPresenceMode
    init(_ title: String, value: AppPresenceMode) { self.title = title; self.value = value }
    @ViewBuilder
    var body: some View {
        if model.presenceMode == value { Button(title, action: select).buttonStyle(.borderedProminent).frame(maxWidth: .infinity) }
        else { Button(title, action: select).buttonStyle(.bordered).frame(maxWidth: .infinity) }
    }
    private func select() { model.presenceMode = value; model.presentationSettingsChanged() }
}

private struct SettingsRow<Control: View>: View {
    let title: String; @ViewBuilder let control: Control
    init(_ title: String, @ViewBuilder control: () -> Control) { self.title = title; self.control = control() }
    var body: some View { HStack(spacing: 20) { Text(title).fontWeight(.medium); Spacer(minLength: 28); control }.padding(.vertical, 13) }
}

private struct SettingsGroup<Content: View>: View {
    let title: String
    let icon: String
    @ViewBuilder let content: Content
    init(_ title: String, icon: String, @ViewBuilder content: () -> Content) { self.title = title; self.icon = icon; self.content = content() }
    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label(localized(title), systemImage: icon).font(.title3.weight(.semibold)).padding(.leading, 4)
            VStack(alignment: .leading, spacing: 14) { content }
                .padding(.horizontal, 18).padding(.vertical, 8)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Color.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 14))
                .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.primary.opacity(0.08)))
        }
    }
}

private struct LaunchTile: View {
    let title: String; let subtitle: String; let icon: String; let tint: Color; let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 14) {
                Image(systemName: icon).font(.system(size: 28)).foregroundStyle(tint).frame(width: 42)
                VStack(alignment: .leading, spacing: 3) { Text(localized(title)).font(.headline); Text(localized(subtitle)).font(.caption).foregroundStyle(.secondary) }
                Spacer(); Image(systemName: "play.fill").foregroundStyle(tint)
            }.padding(16).contentShape(Rectangle())
        }.buttonStyle(.plain).frame(maxWidth: .infinity)
            .background(tint.opacity(0.09), in: RoundedRectangle(cornerRadius: 10))
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(tint.opacity(0.18)))
    }
}

private struct NotificationRow: View {
    let title: String; let subtitle: String; let icon: String; @Binding var isOn: Bool
    var body: some View {
        HStack(spacing: 14) {
            Image(systemName: icon).frame(width: 28).foregroundStyle(.blue)
            VStack(alignment: .leading, spacing: 2) { Text(localized(title)).fontWeight(.medium); Text(localized(subtitle)).font(.caption).foregroundStyle(.secondary) }
            Spacer(); Toggle("", isOn: $isOn).labelsHidden().toggleStyle(.switch)
        }.padding(.vertical, 12)
    }
}

private struct NumberField: View {
    @Binding var value: Int; let label: String
    var body: some View { TextField(label, value: $value, format: .number).frame(width: 92).textFieldStyle(.roundedBorder) }
}

private struct PresetButton: View {
    let title: String; let selected: Bool; let action: () -> Void
    @ViewBuilder var body: some View {
        if selected { Button(title, action: action).buttonStyle(.borderedProminent) }
        else { Button(title, action: action).buttonStyle(.bordered) }
    }
}
