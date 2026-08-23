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
                        }.frame(maxWidth: 920, alignment: .leading).padding(28)
                    }
                }
                statusBar
            }.background(Color(nsColor: .windowBackgroundColor))
        }
        .frame(minWidth: 980, minHeight: 680)
        .overlay {
            if isGlobalDropTarget {
                RoundedRectangle(cornerRadius: 16).strokeBorder(Color.accentColor, style: StrokeStyle(lineWidth: 3, dash: [8]))
                    .background(Color.accentColor.opacity(0.08)).padding(8).allowsHitTesting(false)
            }
        }
        .onDrop(of: [UTType.fileURL], isTargeted: $isGlobalDropTarget, perform: acceptGlobalDrop)
        .onPasteCommand(of: [UTType.fileURL]) { _ in
            if model.pasteFilesFromClipboard() { section = .transfer }
        }
    }

    private func acceptGlobalDrop(_ providers: [NSItemProvider]) -> Bool {
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
            Button(action: refreshCurrentSection) { Label("새로고침", systemImage: "arrow.clockwise") }.disabled(model.isBusy)
        }
    }

    private func refreshCurrentSection() {
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

    private var statusBar: some View {
        HStack(spacing: 10) {
            if model.isBusy { ProgressView().controlSize(.small) }
            Text(LocalizedStringKey(model.status)).font(.caption).foregroundStyle(.secondary).textSelection(.enabled).lineLimit(1)
            Spacer()
            if model.isTransferring { Text(model.transferStatus).font(.caption); Button("취소", action: model.cancelTransfer).controlSize(.small) }
            Button("로그 저장", action: model.saveLog).controlSize(.small)
        }.padding(.horizontal, 16).frame(height: 38).background(.bar).overlay(alignment: .top) { Divider() }
    }

    private var pageDescription: String {
        switch section ?? .home {
        case .home: return "Galaxy 연결과 화면 실행을 한곳에서 관리합니다."
        case .phone: return "Galaxy 주소록, 최근 통화와 메시지를 확인하고 상대를 선택해 바로 이어서 작업합니다."
        case .apps: return "지정한 앱을 단축키로 실행하거나 검색해 변경합니다."
        case .transfer: return "파일과 폴더를 Galaxy의 Download 폴더로 보냅니다."
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
                    ForEach(model.devices) { candidate in Button(model.deviceLabel(candidate)) { model.selectedSerial = candidate.serial; model.applyDeviceSettings() } }
                } label: {
                    HStack { Image(systemName: "iphone.gen3"); VStack(alignment: .leading, spacing: 2) { Text(model.deviceLabel(device).components(separatedBy: "  ·  ").first ?? device.displayName).font(.caption.weight(.semibold)); Text("연결됨 · \(device.serial)").font(.caption2).foregroundStyle(.secondary).lineLimit(1) }; Spacer(); Image(systemName: "chevron.up.chevron.down").font(.caption2) }.contentShape(Rectangle())
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
                    Spacer(); Button("업데이트 확인", action: model.checkForUpdates)
                    Link("GitHub 릴리스 열기", destination: URL(string: "https://github.com/catgarret/FlowBridge/releases")!)
                }
            }
            Card("오픈소스와 라이선스", icon: "doc.text") {
                Text("Flow Bridge의 macOS 코드는 MIT License로 배포됩니다. maze-mei의 MIT 라이선스 프로젝트 DX Manager를 일부 기반으로 하며, 원저작권 표시와 라이선스 전문을 보존합니다.")
                Text("scrcpy, Android Debug Bridge, SDL, FFmpeg와 동봉 구성요소에는 각각의 오픈소스 라이선스가 적용됩니다.")
                    .foregroundStyle(.secondary)
                HStack {
                    Link("Flow Bridge 소스", destination: URL(string: "https://github.com/catgarret/FlowBridge")!)
                    Link("원본 DX Manager", destination: URL(string: "https://github.com/maze-mei/DX-Manager")!)
                    Link("scrcpy", destination: URL(string: "https://github.com/Genymobile/scrcpy")!)
                    Link("전체 라이선스 보기", destination: URL(string: "https://github.com/catgarret/FlowBridge#attribution-licenses-and-trademarks--출처라이선스상표")!)
                }
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
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("기기 연결", icon: "cable.connector") {
                VStack(spacing: 12) {
                    if let device = model.devices.first(where: { $0.serial == model.selectedSerial }) {
                        HStack(spacing: 16) {
                            ZStack { RoundedRectangle(cornerRadius: 12).fill(Color.blue.gradient); Image(systemName: "iphone.gen3").font(.system(size: 28)).foregroundStyle(.white) }.frame(width: 58, height: 72)
                            VStack(alignment: .leading, spacing: 5) {
                                if isEditingAlias {
                                    HStack(spacing: 8) { TextField(device.displayName, text: $model.deviceAlias).textFieldStyle(.roundedBorder).frame(maxWidth: 260).onSubmit { model.saveDeviceAlias(); isEditingAlias = false }; Button("완료") { model.saveDeviceAlias(); isEditingAlias = false }.buttonStyle(.borderedProminent) }
                                } else {
                                    Button { isEditingAlias = true } label: { HStack(spacing: 6) { Text(model.deviceAlias.isEmpty ? device.displayName : model.deviceAlias).font(.title3.weight(.semibold)); Image(systemName: "pencil").font(.caption).foregroundStyle(.secondary) } }.buttonStyle(.plain).help("기기 별칭 지정 또는 수정")
                                }
                                HStack(spacing: 6) { Circle().fill(.green).frame(width: 8, height: 8); Text("연결됨").font(.subheadline.weight(.medium)); Text(device.serial.contains(":") ? "Wi-Fi" : "USB").font(.caption).padding(.horizontal, 7).padding(.vertical, 2).background(Color.secondary.opacity(0.12), in: Capsule()) }
                                Text(model.deviceAlias.isEmpty ? device.serial : "\(device.displayName) · \(device.serial)").font(.caption).foregroundStyle(.secondary)
                            }
                            Spacer()
                            if model.devices.count > 1 {
                                Picker("기기 전환", selection: $model.selectedSerial) { ForEach(model.devices) { Text(model.deviceLabel($0)).tag($0.serial) } }.frame(maxWidth: 260).onChange(of: model.selectedSerial) { _ in model.applyDeviceSettings() }
                            }
                            DeviceLaunchButtons()
                        }.padding(16).background(Color.green.opacity(0.07), in: RoundedRectangle(cornerRadius: 12)).overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.green.opacity(0.18)))
                        DisclosureGroup("다른 기기 추가 또는 연결 방식 변경") { ConnectionSetupView().padding(.top, 10) }
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
            if model.sessionPhase == .launching {
                HStack(spacing: 8) { ProgressView(); Text("화면 연결 준비 중").font(.subheadline).foregroundStyle(.secondary) }
            } else if model.sessionPhase == .running {
                Label("화면 실행 중", systemImage: "checkmark.circle.fill").font(.subheadline).foregroundStyle(.green)
            }
            HStack(spacing: 6) {
                Text("실행 시 밝기 최저 조절")
                Button { showBrightnessHelp.toggle() } label: { Image(systemName: "questionmark.circle").foregroundStyle(.secondary) }.buttonStyle(.plain).help(brightnessHelp).popover(isPresented: $showBrightnessHelp, arrowEdge: .bottom) { Text(brightnessHelp).frame(width: 280, alignment: .leading).padding(14) }
                Spacer(); Toggle("", isOn: $model.turnPhoneScreenOffOnStart).labelsHidden().toggleStyle(.switch).onChange(of: model.turnPhoneScreenOffOnStart) { _ in model.brightnessSettingChanged() }
            }
            if model.phoneNeedsUnlock { Label("Galaxy가 잠겨 있습니다. 잠금을 해제하면 보호되지 않은 화면이 표시됩니다.", systemImage: "lock.fill").foregroundStyle(.orange) }
        }
    }
}

private struct DeviceLaunchButtons: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        Group {
            if model.sessionPhase == .launching {
                Button("취소", role: .destructive, action: model.stop)
            } else if model.sessionPhase == .running {
                Button("화면 종료", role: .destructive, action: model.stop)
            } else {
                VStack(spacing: 8) {
                    Button(action: model.startDeX) { Label("DEX 모드", systemImage: "display") }.buttonStyle(.borderedProminent)
                    Button(action: model.startPhoneMirror) { Label("휴대폰 미러링", systemImage: "iphone") }
                }.controlSize(.regular).fixedSize()
            }
        }
    }
}

private struct AppsView: View {
    @EnvironmentObject private var model: AppModel
    @State private var showAppPicker = false
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack { Spacer(); Picker("실행 모드", selection: $model.appLaunchMode) { Text("DEX 모드").tag(AppLaunchMode.desktopWindow); Text("휴대폰 미러링 모드").tag(AppLaunchMode.phoneScreen) }.pickerStyle(.segmented).labelsHidden().frame(width: 420).onChange(of: model.appLaunchMode) { _ in model.appLaunchModeChanged() }; Spacer() }
            Card("앱 바로 실행 지정", icon: "bolt.square") {
                VStack(spacing: 0) {
                    ForEach(0..<3, id: \.self) { slot in
                        let name = model.installedApps.first(where: { $0.package == model.packageNames[slot] })?.name ?? (model.packageNames[slot] == "com.android.settings" ? "설정" : "지정 안 됨")
                        HStack(spacing: 14) {
                            Text("\(slot + 1)").font(.headline).foregroundStyle(.white).frame(width: 30, height: 30).background(Color.accentColor, in: Circle())
                            VStack(alignment: .leading, spacing: 2) { Text(name).font(.headline).lineLimit(1); Text("⌘\(slot + 1)").font(.caption).foregroundStyle(.secondary) }
                            Spacer(); Button("실행") { model.startApp(slot: slot) }.buttonStyle(.borderedProminent).disabled(model.packageNames[slot].isEmpty).keyboardShortcut(KeyEquivalent(Character(String(slot + 1))), modifiers: .command)
                        }.padding(.vertical, 13)
                        if slot < 2 { Divider() }
                    }
                }
                HStack { Spacer(); Button { showAppPicker = true } label: { Label("앱 검색", systemImage: "magnifyingglass") }.buttonStyle(.borderedProminent) }.padding(.top, 6)
            }
        }
        .sheet(isPresented: $showAppPicker) { AppPickerSheet().environmentObject(model) }
    }
}

private struct AppPickerSheet: View {
    @EnvironmentObject private var model: AppModel
    @Environment(\.dismiss) private var dismiss
    var body: some View {
        VStack(spacing: 0) {
            HStack { Text("앱 검색").font(.title2.weight(.semibold)); Spacer(); Button("완료") { dismiss() } }.padding(20)
            Divider()
            TextField("앱 이름 검색", text: $model.appSearch).textFieldStyle(.roundedBorder).padding(16)
            let matches = model.installedApps.filter { model.appSearch.isEmpty || $0.name.localizedCaseInsensitiveContains(model.appSearch) || $0.package.localizedCaseInsensitiveContains(model.appSearch) }
            let grouped = Dictionary(grouping: Array(matches.prefix(300))) { indexKey($0.name) }.sorted { $0.key.localizedStandardCompare($1.key) == .orderedAscending }
            ScrollViewReader { proxy in
                ZStack(alignment: .trailing) {
                    List { ForEach(grouped, id: \.key) { letter, apps in Section(letter) { ForEach(apps) { app in HStack { VStack(alignment: .leading, spacing: 2) { Text(app.name).fontWeight(.medium); Text(app.package).font(.caption2).foregroundStyle(.secondary) }; Spacer(); HStack(spacing: 5) { ForEach(0..<3) { slot in Button("\(slot + 1)") { model.toggleFavorite(package: app.package, slot: slot) }.buttonStyle(.borderedProminent).tint(model.packageNames[slot] == app.package ? Color.accentColor : Color.secondary.opacity(0.35)).help("⌘\(slot + 1) 바로 실행 지정") } }; Button("실행") { model.startApp(package: app.package) } } } }.id(letter) } }
                    if model.appSearch.isEmpty { VStack(spacing: 1) { ForEach(grouped.map(\.key), id: \.self) { letter in Button(letter) { withAnimation { proxy.scrollTo(letter, anchor: .top) } }.buttonStyle(.plain).font(.caption2.weight(.semibold)).foregroundStyle(.blue) } }.padding(.trailing, 6) }
                }
            }
            HStack { Spacer(); Text("\(matches.count)개 앱").foregroundStyle(.secondary) }.padding(16)
        }.frame(width: 620, height: 600)
    }

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
    var body: some View { HStack(spacing: 14) { Image(systemName: icon).font(.title3).foregroundStyle(.blue).frame(width: 28); VStack(alignment: .leading, spacing: 3) { Text(title).fontWeight(.medium); Text(subtitle).font(.caption).foregroundStyle(.secondary) }; Spacer(); accessory }.padding(14).background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 10)).overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.primary.opacity(0.07))) }
}

private struct ConnectionSetupView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(spacing: 10) {
            ConnectionOption(icon: "cable.connector", title: "USB로 기기 추가", subtitle: "Galaxy에서 이 Mac의 USB 디버깅을 허용한 뒤 무선 연결로 전환합니다.") { Button("USB 기기 추가", action: model.prepareWirelessFromUSB).buttonStyle(.borderedProminent) }
            ConnectionOption(icon: "wifi", title: "무선으로 기기 추가", subtitle: "Galaxy의 무선 디버깅 페어링 화면에서 기기를 검색합니다.") { Button("기기 검색", action: model.discoverWirelessSetup) }
            DisclosureGroup("IP 주소로 직접 연결") {
                VStack(spacing: 10) {
                    HStack { TextField(localized("무선 ADB 주소  예: 172.30.1.3:44065"), text: $model.wirelessEndpoint); Button("직접 연결", action: model.connectWireless) }
                    HStack { TextField(localized("페어링 IP:포트"), text: $model.pairingEndpoint); SecureField(localized("6자리 코드"), text: $model.pairingCode).frame(width: 120); Button("직접 페어링", action: model.pairWireless) }
                }.padding(.top, 10)
            }.font(.subheadline)
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
    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Spacer()
                Picker("", selection: $tab) { Label("통화", systemImage: "phone.fill").tag(0); Label("메시지", systemImage: "message.fill").tag(1) }.pickerStyle(.segmented).labelsHidden().controlSize(.large).frame(maxWidth: 440)
                Spacer()
            }.padding(.bottom, 14)
            Divider()
            HStack(spacing: 0) {
                VStack(spacing: 0) {
                    TextField("이름, 번호 또는 내용 검색", text: $model.phoneSearch).textFieldStyle(.roundedBorder).padding(16)
                    if tab == 0 { HStack(spacing: 10) { Picker("통화 목록", selection: $callSource) { Text("최근 통화").tag(0); Text("연락처").tag(1) }.pickerStyle(.segmented); Button { model.phoneNumber = ""; showDialPad = true } label: { Image(systemName: "circle.grid.3x3.fill") }.buttonStyle(.borderedProminent).help("다이얼 열기") }.padding(.horizontal, 16).padding(.bottom, 14) }
                    Divider()
                    if tab == 0 { callSource == 0 ? AnyView(callList) : AnyView(contactList) } else { messageThreadList }
                }.frame(width: 360)
                Divider()
                if selectedNumber.isEmpty { emptyDetail } else if tab == 0 { callDetail } else { messageDetail }
            }.frame(maxWidth: .infinity, maxHeight: .infinity)
        }.frame(maxWidth: .infinity, maxHeight: .infinity)
            .overlay(alignment: .top) { if showCallToast { Label("Galaxy에서 통화를 확인해 주세요.", systemImage: "iphone.gen3").padding(.horizontal, 16).padding(.vertical, 10).background(.regularMaterial, in: Capsule()).shadow(radius: 8).padding(.top, 8).transition(.move(edge: .top).combined(with: .opacity)) } }
            .onAppear { if model.contacts.isEmpty { model.refreshPhoneData() } }
    }

    private var callList: some View {
        let filtered = model.recentCalls.filter { model.phoneSearch.isEmpty || $0.number.contains(model.phoneSearch) || contactName($0.number).localizedCaseInsensitiveContains(model.phoneSearch) }
        return List(filtered.prefix(100)) { call in
            HStack(spacing: 12) { ContactAvatar(name: contactName(call.number), photoURL: contactPhoto(call.number)); VStack(alignment: .leading, spacing: 5) { Text(contactName(call.number)).fontWeight(.medium); HStack(spacing: 4) { Image(systemName: call.type == 1 ? "phone.arrow.down.left" : call.type == 2 ? "phone.arrow.up.right" : "phone.down"); Text(call.number) }.font(.caption).foregroundStyle(call.type == 3 ? .red : .secondary) }; Spacer(); Text(call.date, style: .relative).font(.caption2).foregroundStyle(.secondary) }.padding(.vertical, 7).contentShape(Rectangle()).onTapGesture { select(call.number, rowID: call.id) }.listRowBackground(selectedRowID == call.id ? Color.accentColor.opacity(0.14) : Color.clear)
        }.listStyle(.plain).scrollContentBackground(.hidden).background(Color(nsColor: .windowBackgroundColor))
    }

    private var contactList: some View {
        let filtered = model.contacts.filter { model.phoneSearch.isEmpty || $0.number.contains(model.phoneSearch) || $0.name.localizedCaseInsensitiveContains(model.phoneSearch) }
        return List(filtered.prefix(200)) { contact in
            HStack(spacing: 12) { ContactAvatar(name: contact.name, photoURL: model.contactPhotoURL(for: contact.number)); VStack(alignment: .leading, spacing: 5) { Text(contact.name).fontWeight(.medium); Text(contact.number).font(.caption).foregroundStyle(.secondary) }; Spacer(); Image(systemName: "chevron.right").font(.caption).foregroundStyle(.tertiary) }.padding(.vertical, 7).contentShape(Rectangle()).onTapGesture { select(contact.number, rowID: contact.id) }.listRowBackground(selectedRowID == contact.id ? Color.accentColor.opacity(0.14) : Color.clear)
        }.listStyle(.plain).scrollContentBackground(.hidden).background(Color(nsColor: .windowBackgroundColor))
    }

    private var messageThreadList: some View {
        let latest = Dictionary(grouping: model.recentMessages, by: \.address).compactMap { $0.value.max(by: { $0.date < $1.date }) }.sorted { $0.date > $1.date }
        let filtered = latest.filter { model.phoneSearch.isEmpty || $0.address.contains(model.phoneSearch) || $0.body.localizedCaseInsensitiveContains(model.phoneSearch) || contactName($0.address).localizedCaseInsensitiveContains(model.phoneSearch) }
        return List(filtered) { message in
            HStack(spacing: 12) { ContactAvatar(name: contactName(message.address), photoURL: contactPhoto(message.address)); VStack(alignment: .leading, spacing: 5) { HStack { Text(contactName(message.address)).fontWeight(.medium).lineLimit(1); Spacer(); Text(message.date, style: .relative).font(.caption2).foregroundStyle(.secondary) }; Text(message.body.replacingOccurrences(of: "\n", with: " ")).lineLimit(1).truncationMode(.tail).font(.caption).foregroundStyle(.secondary) } }.padding(.vertical, 7).contentShape(Rectangle()).onTapGesture { select(message.address, rowID: message.address) }.listRowBackground(selectedRowID == message.address ? Color.accentColor.opacity(0.14) : Color.clear)
        }.listStyle(.plain).scrollContentBackground(.hidden).background(Color(nsColor: .windowBackgroundColor))
    }

    private var emptyDetail: some View {
        VStack(spacing: 12) { Image(systemName: tab == 0 ? "phone.circle" : "message.circle").font(.system(size: 54)).foregroundStyle(.secondary); Text(tab == 0 ? "통화 내역이나 연락처를 선택하세요" : "대화를 선택하세요").font(.title3.weight(.semibold)); Text("왼쪽 목록에서 상대를 선택하면 상세 기능이 표시됩니다.").foregroundStyle(.secondary) }.frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var callDetail: some View {
        if showDialPad { return AnyView(dialPad) }
        let calls = model.recentCalls.filter { $0.number == selectedNumber }.sorted { $0.date > $1.date }
        return AnyView(VStack(spacing: 0) {
            VStack(spacing: 8) { ContactAvatar(name: contactName(selectedNumber), photoURL: contactPhoto(selectedNumber), large: true); Text(contactName(selectedNumber)).font(.title2.weight(.semibold)); Text(selectedNumber).foregroundStyle(.secondary); HStack { Button { tab = 1 } label: { Label("메시지", systemImage: "message") }; Button { model.phoneNumber = selectedNumber; showDialPad = true } label: { Label("전화", systemImage: "phone.fill") }.buttonStyle(.borderedProminent) } }.padding(24)
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
            Button(action: placeCall) { Image(systemName: "phone.fill").font(.title2).foregroundStyle(.white).frame(width: 62, height: 62).background(Color.green, in: Circle()).shadow(color: .green.opacity(0.25), radius: 8, y: 3) }.buttonStyle(.plain).disabled(model.phoneNumber.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty).help("전화")
            Text("전화").font(.caption).foregroundStyle(.secondary)
            Spacer(minLength: 0)
        }.padding(28).frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var messageDetail: some View {
        let conversation = model.recentMessages.filter { $0.address == selectedNumber }.sorted { $0.date < $1.date }
        return VStack(spacing: 0) {
            HStack(spacing: 10) { ContactAvatar(name: contactName(selectedNumber), photoURL: contactPhoto(selectedNumber)); VStack(alignment: .leading) { Text(contactName(selectedNumber)).fontWeight(.semibold); Text(selectedNumber).font(.caption).foregroundStyle(.secondary) }; Spacer(); Button(action: model.openDialer) { Image(systemName: "phone") }.help("전화 화면 열기") }.padding(14)
            Divider()
            ScrollViewReader { proxy in ScrollView { LazyVStack(spacing: 10) { ForEach(conversation) { message in HStack { if message.isOutgoing { Spacer(minLength: 80) }; VStack(alignment: message.isOutgoing ? .trailing : .leading, spacing: 4) { Text(message.body).textSelection(.enabled); Text(message.date, style: .time).font(.caption2).foregroundStyle(.secondary) }.padding(10).background(message.isOutgoing ? Color.accentColor.opacity(0.16) : Color.secondary.opacity(0.12), in: RoundedRectangle(cornerRadius: 12)); if !message.isOutgoing { Spacer(minLength: 80) } }.id(message.id) } }.padding(16) }.onAppear { if let last = conversation.last { proxy.scrollTo(last.id, anchor: .bottom) } } }
            Divider()
            HStack(alignment: .bottom, spacing: 10) { TextEditor(text: $model.messageBody).frame(minHeight: 38, maxHeight: 90).padding(5).background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 8)).overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.primary.opacity(0.1))); Button(action: model.composeMessage) { Image(systemName: "paperplane.fill") }.buttonStyle(.borderedProminent).controlSize(.large).help("Galaxy 메시지 앱에서 확인") }.padding(12)
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

private struct TransferView: View {
    @EnvironmentObject private var model: AppModel
    @State private var selectedRemote: RemoteFile?
    @State private var isDropTarget = false
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("Mac에서 Galaxy로", icon: "arrow.up.circle") {
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
            }
            Card("Galaxy에서 Mac으로", icon: "arrow.down.circle") {
                HStack {
                    Text("Galaxy Download").fontWeight(.medium)
                    Spacer()
                }
                if model.remoteFiles.isEmpty {
                    VStack(spacing: 10) {
                        Image(systemName: "folder").font(.system(size: 34)).foregroundStyle(.secondary)
                        Text("불러온 파일 없음").fontWeight(.medium)
                        Text("파일 목록 갱신을 눌러 Galaxy의 Download 폴더를 확인하세요.").font(.caption).foregroundStyle(.secondary)
                    }.frame(maxWidth: .infinity, minHeight: 180)
                } else {
                    List(model.remoteFiles, selection: $selectedRemote) { file in
                        Label(file.name, systemImage: file.isDirectory ? "folder" : "doc").tag(file).onDrag { model.remoteFileProvider(file) }
                    }.frame(minHeight: 240)
                }
                HStack {
                    Text("선택한 파일을 Finder로 드래그하거나 ⌘C 후 Finder에서 ⌘V 하세요.").font(.caption).foregroundStyle(.secondary)
                    Spacer()
                    Button("Mac 클립보드에 복사") { if let selectedRemote { model.copyRemoteFile(selectedRemote) } }
                        .keyboardShortcut("c", modifiers: .command).disabled(selectedRemote == nil)
                    Button("다른 이름으로 저장") { if let selectedRemote { model.downloadRemoteFile(selectedRemote) } }.disabled(selectedRemote == nil)
                }
            }
        }
    }

    private func acceptDrop(_ providers: [NSItemProvider]) -> Bool {
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
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("알림 항목 선택", icon: "bell.badge") {
                VStack(spacing: 0) {
                    NotificationRow(title: "전화", subtitle: "Galaxy 수신 전화 알림", icon: "phone", isOn: $model.phoneNotificationsEnabled)
                    Divider(); NotificationRow(title: "문자", subtitle: "Samsung 메시지와 Google 메시지", icon: "message", isOn: $model.messageNotificationsEnabled)
                    Divider(); NotificationRow(title: "애플리케이션", subtitle: "Galaxy의 나머지 앱 알림", icon: "app.badge", isOn: $model.appNotificationsEnabled)
                }
                .onChange(of: model.phoneNotificationsEnabled) { _ in model.notificationSettingsChanged() }
                .onChange(of: model.messageNotificationsEnabled) { _ in model.notificationSettingsChanged() }
                .onChange(of: model.appNotificationsEnabled) { _ in model.notificationSettingsChanged() }
            }
            Card("Galaxy 알림", icon: "iphone.and.arrow.forward") {
                HStack { Text("Galaxy 알림창에 남아 있는 항목").foregroundStyle(.secondary); Spacer(); Button("모두 지우기", role: .destructive, action: model.dismissAllNotifications).disabled(model.activeNotifications.isEmpty) }
                if model.activeNotifications.isEmpty { VStack(spacing: 8) { Image(systemName: "bell.slash").font(.system(size: 30)).foregroundStyle(.secondary); Text("남아 있는 알림 없음").foregroundStyle(.secondary) }.frame(maxWidth: .infinity).padding(28) }
                else { VStack(spacing: 0) { ForEach(model.activeNotifications) { item in HStack(spacing: 12) { Image(systemName: item.kind == .call ? "phone" : item.kind == .message ? "message" : "app.badge").foregroundStyle(.blue).frame(width: 28); VStack(alignment: .leading, spacing: 3) { Text(item.title.isEmpty ? item.package : item.title).fontWeight(.medium); Text(item.body).font(.caption).foregroundStyle(.secondary).lineLimit(2) }; Spacer(); Button { model.dismissNotification(item) } label: { Image(systemName: "xmark") }.buttonStyle(.borderless).help("Galaxy에서 알림 지우기") }.padding(.vertical, 10); Divider() } } }
            }
        }.onAppear(perform: model.loadActiveNotifications)
    }
}

private struct SettingsView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("화면 품질", icon: "display") {
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
                            Picker("프레임", selection: $model.settings.fps) { Text("30 FPS").tag(30); Text("60 FPS").tag(60); Text("120 FPS").tag(120) }.pickerStyle(.segmented).labelsHidden().frame(width: 360)
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
            Card("연결과 Mac 동작", icon: "gearshape") {
                VStack(alignment: .leading, spacing: 14) {
                    Toggle("마지막 무선 주소로 자동 재연결", isOn: $model.automaticReconnect)
                    HStack { Text("Flow Bridge 로그인 자동 실행"); Spacer(); Button(model.launchAtLogin ? "끄기" : "켜기", action: model.toggleLaunchAtLogin) }
                    Divider()
                    HStack {
                        Text("앱 표시 위치").frame(width: 130, alignment: .leading)
                        Picker("앱 표시 위치", selection: $model.presenceMode) {
                            Text("Dock + 메뉴 막대").tag(AppPresenceMode.dockAndMenuBar)
                            Text("메뉴 막대만").tag(AppPresenceMode.menuBarOnly)
                            Text("Dock만").tag(AppPresenceMode.dockOnly)
                        }.pickerStyle(.segmented).labelsHidden().onChange(of: model.presenceMode) { _ in model.presentationSettingsChanged() }
                    }
                    Toggle("앱을 시작할 때 메인 창 열기", isOn: $model.openMainWindowAtLaunch).onChange(of: model.openMainWindowAtLaunch) { _ in model.presentationSettingsChanged() }
                    Text("메뉴 막대만 선택하면 Dock 아이콘 없이 백그라운드에서 실행되며, 상단 Flow Bridge 아이콘으로 창을 열 수 있습니다.").font(.caption).foregroundStyle(.secondary)
                    HStack {
                        Text("화면 제어 바 위치").frame(width: 130, alignment: .leading)
                        Picker("화면 제어 바 위치", selection: $model.controlBarPosition) { Text("화면 상단").tag(ControlBarPosition.top); Text("화면 하단").tag(ControlBarPosition.bottom) }.pickerStyle(.segmented).labelsHidden().frame(maxWidth: 280).onChange(of: model.controlBarPosition) { _ in model.controlBarPositionChanged() }
                    }
                    HStack { Text("사용하지 않을 때 자동 숨김"); TextField("분", value: $model.autoHideMinutes, format: .number).frame(width: 54); Text("분 · 0이면 사용 안 함").foregroundStyle(.secondary) }
                    Divider()
                    HStack {
                        Button(model.keyboardCorrectionEnabled ? "오른쪽 Shift 보정 끄기" : "오른쪽 Shift 보정 켜기", action: model.toggleKeyboardCorrection)
                        Button(model.shiftEnterMode ? "일반 Enter로 전환" : "Shift+Enter로 전환", action: model.toggleShiftEnter)
                    }
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
                    Spacer()
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

private struct Card<Content: View>: View {
    let title: String; let icon: String; @ViewBuilder let content: Content
    init(_ title: String, icon: String, @ViewBuilder content: () -> Content) { self.title = title; self.icon = icon; self.content = content() }
    var body: some View {
        VStack(alignment: .leading, spacing: 14) { Label(localized(title), systemImage: icon).font(.headline); content }
            .padding(18).frame(maxWidth: .infinity, alignment: .leading)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 12))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.primary.opacity(0.08)))
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
