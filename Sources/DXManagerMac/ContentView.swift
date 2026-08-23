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
                VStack(alignment: .leading, spacing: 5) {
                    Label(model.selectedSerial.isEmpty ? "연결되지 않음" : "Galaxy 연결됨", systemImage: model.selectedSerial.isEmpty ? "iphone.slash" : "iphone.gen3")
                        .font(.caption.weight(.semibold))
                    Text(model.selectedSerial.isEmpty ? "USB 또는 무선으로 연결해 주세요." : model.selectedSerial)
                        .font(.caption2).foregroundStyle(.secondary).lineLimit(1)
                }.frame(maxWidth: .infinity, alignment: .leading).padding(12).background(.ultraThinMaterial)
            }
        } detail: {
            VStack(spacing: 0) {
                ScrollView {
                    VStack(alignment: .leading, spacing: 18) {
                        pageHeader
                        switch section ?? .home {
                        case .home: HomeView()
                        case .phone: PhoneView()
                        case .apps: AppsView()
                        case .transfer: TransferView()
                        case .notifications: NotificationsView()
                        case .settings: SettingsView()
                        case .diagnostics: DiagnosticsView()
                        case .about: AboutView()
                        }
                    }.frame(maxWidth: 920, alignment: .leading).padding(28)
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
            Button { model.refresh() } label: { Label("새로고침", systemImage: "arrow.clockwise") }.disabled(model.isBusy)
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
        case .phone: return "Mac에서 번호를 입력해 Galaxy 전화와 문자를 시작합니다."
        case .apps: return "앱 이름으로 검색하거나 즐겨찾기 단축키로 바로 실행합니다."
        case .transfer: return "파일과 폴더를 Galaxy의 Download 폴더로 보냅니다."
        case .notifications: return "전화·문자·앱 알림을 Mac 알림 센터로 전달합니다."
        case .settings: return "화면 품질, 자동 연결과 Mac 동작을 설정합니다."
        case .diagnostics: return "기기 정보와 화면 복구 도구 상태를 확인합니다."
        case .about: return "버전, 업데이트, 오픈소스 라이선스와 프로젝트 링크를 확인합니다."
        }
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
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("기기 연결", icon: "cable.connector") {
                VStack(spacing: 12) {
                    Picker("Galaxy", selection: $model.selectedSerial) {
                        if model.devices.isEmpty { Text("연결된 기기 없음").tag("") }
                        ForEach(model.devices) { Text("\($0.displayName)  ·  \($0.serial)").tag($0.serial) }
                    }.onChange(of: model.selectedSerial) { _ in model.applyDeviceSettings() }
                    ConnectionOption(icon: "cable.connector", title: "USB 연결을 무선으로 전환", subtitle: "한 번 승인한 USB 연결을 저장하고 다음부터 자동 연결합니다.") {
                        Button("전환", action: model.prepareWirelessFromUSB).buttonStyle(.borderedProminent)
                    }
                    ConnectionOption(icon: "wifi", title: "USB 없이 처음 연결", subtitle: "Galaxy의 무선 디버깅 페어링 화면을 연 뒤 자동 검색합니다.") {
                        Button("자동 검색", action: model.discoverWirelessSetup)
                    }
                    if !model.pairingEndpoint.isEmpty {
                        HStack {
                            Label(model.pairingEndpoint, systemImage: "dot.radiowaves.left.and.right")
                            Spacer(); SecureField(localized("6자리 코드"), text: $model.pairingCode).frame(width: 130)
                            Button("페어링 완료", action: model.pairWireless).buttonStyle(.borderedProminent)
                        }.padding(12).background(Color.green.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
                    }
                    DisclosureGroup("고급·수동 연결") {
                        VStack(spacing: 10) {
                            HStack { TextField(localized("무선 ADB 주소  예: 172.30.1.3:44065"), text: $model.wirelessEndpoint); Button("직접 연결", action: model.connectWireless) }
                            HStack { TextField(localized("페어링 IP:포트"), text: $model.pairingEndpoint); SecureField(localized("6자리 코드"), text: $model.pairingCode).frame(width: 120); Button("직접 페어링", action: model.pairWireless) }
                        }.padding(.top, 10)
                    }.font(.subheadline)
                }
            }
            Card("화면 열기", icon: "macwindow.on.rectangle") {
                if model.sessionPhase == .launching {
                    HStack(spacing: 12) { ProgressView(); VStack(alignment: .leading) { Text("화면 연결 준비 중").fontWeight(.semibold); Text("Galaxy를 깨우고 영상 창이 실제로 표시되는지 확인하고 있습니다.").font(.caption).foregroundStyle(.secondary) }; Spacer(); Button("취소", role: .destructive, action: model.stop) }
                        .padding(16).background(Color.blue.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
                } else if model.sessionPhase == .running {
                    HStack { Label("화면 실행 중", systemImage: "checkmark.circle.fill").foregroundStyle(.green); Spacer(); Button("화면 종료 및 정리", role: .destructive, action: model.stop) }
                } else {
                    HStack(spacing: 14) {
                        LaunchTile(title: "데스크톱 모드", subtitle: "넓은 화면으로 작업", icon: "display", tint: .blue, action: model.startDeX)
                        LaunchTile(title: "휴대폰 미러링", subtitle: "기본 화면 그대로", icon: "iphone", tint: .purple, action: model.startPhoneMirror)
                    }
                }
                Toggle("화면을 열면 Galaxy 밝기를 최저로 낮추기", isOn: $model.turnPhoneScreenOffOnStart)
                    .toggleStyle(.switch).onChange(of: model.turnPhoneScreenOffOnStart) { _ in model.save() }
                Text("화면을 완전히 끄면 일부 Galaxy에서 영상 전송도 중단될 수 있어 밝기만 낮춥니다. 종료하면 이전 밝기로 복원합니다.").font(.caption).foregroundStyle(.secondary)
                HStack {
                    Button(action: model.volumeDown) { Label("볼륨 낮춤", systemImage: "speaker.minus") }
                    Button(action: model.volumeUp) { Label("볼륨 높임", systemImage: "speaker.plus") }
                    Spacer()
                    Button { model.sendKeyEvent(224, label: "화면 켜기") } label: { Label("화면 켜기", systemImage: "sun.max") }
                    Button { model.sendKeyEvent(223, label: "화면 끄기") } label: { Label("화면 끄기", systemImage: "moon") }
                    Button { model.sendKeyEvent(26, label: "전원") } label: { Label("전원", systemImage: "power") }
                }.padding(.top, 4)
                if model.phoneNeedsUnlock { Label("Galaxy가 잠겨 있습니다. 잠금을 해제하면 보호되지 않은 화면이 표시됩니다.", systemImage: "lock.fill").foregroundStyle(.orange) }
                Text("데스크톱 모드 실행 중 Galaxy에 보이는 오버레이는 가상 화면을 만드는 Android 시스템 창입니다. 실행 중에는 유지되고 종료 시 자동 제거됩니다.").font(.caption).foregroundStyle(.secondary)
            }
            if model.hasActiveSession {
                Card("보호된 화면 안내", icon: "lock.shield") {
                    Text("비밀번호·결제·DRM 화면은 Android 보안 정책으로 Mac에서 검게 표시될 수 있습니다. 우회하지 않으며 Galaxy에서 직접 입력하거나 진행해야 합니다.")
                    HStack {
                        Text("일반 입력 화면이라면 Flow Bridge 창에 키보드로 입력한 뒤 Enter를 눌러도 됩니다.").font(.caption).foregroundStyle(.secondary)
                        Spacer(); Button("휴대폰 화면 켜기") { model.sendKeyEvent(224, label: "화면 켜기") }
                    }
                }
            }
            Card("빠른 작업", icon: "bolt") {
                HStack {
                    Button(action: model.chooseAndTransfer) { Label("파일·폴더 보내기", systemImage: "arrow.up.doc") }
                    Button(action: model.captureRegion) { Label("화면 영역 캡처", systemImage: "viewfinder") }
                    Button(action: model.loadPackages) { Label("앱 목록 갱신", systemImage: "square.grid.2x2") }
                }
            }
            Card("텍스트 클립보드", icon: "doc.on.clipboard") {
                HStack(spacing: 22) {
                    Label("Mac에서 복사 → 화면 창에서 ⌘V", systemImage: "macbook.and.iphone")
                    Label("휴대폰에서 선택 → 화면 창에서 ⌘C", systemImage: "iphone.and.arrow.forward")
                }
                Text("데스크톱 모드, 휴대폰 미러링과 독립 앱 창에서 텍스트가 양방향으로 동기화됩니다.")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
    }
}

private struct AppsView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
        Card("즐겨찾기", icon: "star") {
            VStack(spacing: 0) {
                ForEach(0..<3, id: \.self) { slot in
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Text("⌘\(slot + 1)").font(.headline).frame(width: 52, alignment: .leading)
                            VStack(alignment: .leading, spacing: 2) {
                                Text(model.installedApps.first(where: { $0.package == model.packageNames[slot] })?.name ?? (model.packageNames[slot] == "com.android.settings" ? "설정" : "지정되지 않음")).fontWeight(.medium)
                                Text(model.packageNames[slot].isEmpty ? "아래 검색 결과에서 지정하세요." : model.packageNames[slot]).font(.caption2).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Button("실행") { model.startApp(slot: slot) }.buttonStyle(.borderedProminent).keyboardShortcut(KeyEquivalent(Character(String(slot + 1))), modifiers: .command)
                                .disabled(model.packageNames[slot].isEmpty)
                        }
                        HStack {
                            Button("현재 화면 설정 저장") { model.saveAppProfile(slot: slot) }
                            Button("저장 설정 적용") { model.applyAppProfile(slot: slot) }
                        }.controlSize(.small)
                    }.padding(.vertical, 14)
                    if slot < 2 { Divider() }
                }
            }
            Text("⌘1 · ⌘2 · ⌘3으로 지정한 앱을 즉시 실행합니다.").font(.caption).foregroundStyle(.secondary)
        }
        Card("앱 검색 및 바로 실행", icon: "magnifyingglass") {
            TextField("앱 이름 검색", text: $model.appSearch).textFieldStyle(.roundedBorder)
            let matches = model.installedApps.filter { model.appSearch.isEmpty || $0.name.localizedCaseInsensitiveContains(model.appSearch) || $0.package.localizedCaseInsensitiveContains(model.appSearch) }
            if matches.isEmpty { Text("일치하는 앱이 없습니다.").foregroundStyle(.secondary).frame(maxWidth: .infinity, minHeight: 100) }
            else {
                LazyVStack(spacing: 0) {
                    ForEach(matches.prefix(30)) { app in
                        HStack { VStack(alignment: .leading) { Text(app.name).fontWeight(.medium); Text(app.package).font(.caption2).foregroundStyle(.tertiary) }; Spacer(); Menu("즐겨찾기") { ForEach(0..<3) { slot in Button("\(slot + 1)에 지정") { model.assignFavorite(package: app.package, slot: slot) } } }; Button("열기") { model.startApp(package: app.package) }.buttonStyle(.borderedProminent) }
                            .padding(.vertical, 9)
                        Divider()
                    }
                }
            }
            HStack { Button(action: model.loadPackages) { Label("앱 목록 갱신", systemImage: "arrow.clockwise") }; Spacer(); Text("검색 결과는 최대 30개까지 표시합니다.").font(.caption).foregroundStyle(.secondary) }
        }
        }
    }
}

private struct ConnectionOption<Accessory: View>: View {
    let icon: String; let title: String; let subtitle: String; @ViewBuilder let accessory: Accessory
    init(icon: String, title: String, subtitle: String, @ViewBuilder accessory: () -> Accessory) { self.icon = icon; self.title = title; self.subtitle = subtitle; self.accessory = accessory() }
    var body: some View { HStack(spacing: 14) { Image(systemName: icon).font(.title3).foregroundStyle(.blue).frame(width: 28); VStack(alignment: .leading, spacing: 3) { Text(title).fontWeight(.medium); Text(subtitle).font(.caption).foregroundStyle(.secondary) }; Spacer(); accessory }.padding(14).background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 10)).overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.primary.opacity(0.07))) }
}

private struct PhoneView: View {
    @EnvironmentObject private var model: AppModel
    @State private var tab = 0
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("전화·문자", icon: "phone.connection") {
                HStack { Picker("보기", selection: $tab) { Text("주소록").tag(0); Text("최근 통화").tag(1); Text("메시지").tag(2) }.pickerStyle(.segmented); Button("동기화", action: model.refreshPhoneData).buttonStyle(.borderedProminent) }
                TextField("이름 또는 번호 검색", text: $model.phoneSearch).textFieldStyle(.roundedBorder)
                Group {
                    if tab == 0 {
                        let rows = model.contacts.filter { model.phoneSearch.isEmpty || $0.name.localizedCaseInsensitiveContains(model.phoneSearch) || $0.number.contains(model.phoneSearch) }
                        List(rows.prefix(100)) { item in HStack { VStack(alignment: .leading) { Text(item.name).fontWeight(.medium); Text(item.number).font(.caption).foregroundStyle(.secondary) }; Spacer(); Button("선택") { model.selectPhoneNumber(item.number) } } }
                    } else if tab == 1 {
                        let rows = model.recentCalls.filter { model.phoneSearch.isEmpty || $0.number.contains(model.phoneSearch) }
                        List(rows.prefix(100)) { item in HStack { Image(systemName: item.type == 1 ? "phone.arrow.down.left" : item.type == 2 ? "phone.arrow.up.right" : "phone.down"); VStack(alignment: .leading) { Text(item.number); Text(item.date, style: .relative).font(.caption).foregroundStyle(.secondary) }; Spacer(); Button("선택") { model.selectPhoneNumber(item.number) } } }
                    } else {
                        let rows = model.recentMessages.filter { model.phoneSearch.isEmpty || $0.address.contains(model.phoneSearch) || $0.body.localizedCaseInsensitiveContains(model.phoneSearch) }
                        List(rows.prefix(100)) { item in HStack { VStack(alignment: .leading) { Text(item.address).fontWeight(.medium); Text(item.body).lineLimit(2).font(.caption).foregroundStyle(.secondary) }; Spacer(); Button("답장") { model.selectPhoneNumber(item.address) } } }
                    }
                }.frame(minHeight: 240)
                Text("Galaxy의 로컬 주소록·최근 통화·SMS를 ADB 연결 중에만 읽으며 Flow Bridge가 별도 저장하지 않습니다.").font(.caption).foregroundStyle(.secondary)
            }
            Card("전화 걸기", icon: "phone") {
                HStack {
                    TextField(localized("전화번호"), text: $model.phoneNumber).font(.title3).textFieldStyle(.roundedBorder)
                    Button(action: model.openDialer) { Label("전화 화면 열기", systemImage: "phone.arrow.up.right") }
                        .buttonStyle(.borderedProminent).controlSize(.large)
                }
                HStack {
                    Button { model.sendKeyEvent(5, label: "전화 받기") } label: { Label("받기", systemImage: "phone.fill") }
                    Button(role: .destructive) { model.sendKeyEvent(6, label: "통화 종료") } label: { Label("끊기", systemImage: "phone.down.fill") }
                    Spacer()
                    Text("통화 음성은 Galaxy 또는 Galaxy에 연결된 Bluetooth 헤드셋으로 처리됩니다.").font(.caption).foregroundStyle(.secondary)
                }
            }
            Card("문자 작성", icon: "message") {
                TextField(localized("받는 사람 전화번호"), text: $model.phoneNumber).textFieldStyle(.roundedBorder)
                TextEditor(text: $model.messageBody).font(.body).frame(minHeight: 130)
                    .padding(8).background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 8))
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.primary.opacity(0.12)))
                HStack {
                    Text("작성한 내용은 Galaxy 메시지 앱으로 전달되며 자동 발송하지 않습니다.").font(.caption).foregroundStyle(.secondary)
                    Spacer()
                    Button(action: model.composeMessage) { Label("Galaxy에서 문자 확인", systemImage: "paperplane") }
                        .buttonStyle(.borderedProminent)
                }
            }
        }
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
                    Spacer(); Button("파일 목록 갱신", action: model.loadRemoteFiles)
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
        Card("Mac 알림 센터로 전달", icon: "bell.badge") {
            VStack(spacing: 0) {
                NotificationRow(title: "전화", subtitle: "Galaxy 수신 전화 알림", icon: "phone", isOn: $model.phoneNotificationsEnabled)
                Divider()
                NotificationRow(title: "문자", subtitle: "Samsung 메시지와 Google 메시지", icon: "message", isOn: $model.messageNotificationsEnabled)
                Divider()
                NotificationRow(title: "애플리케이션", subtitle: "Galaxy의 나머지 앱 알림", icon: "app.badge", isOn: $model.appNotificationsEnabled)
            }
            .onChange(of: model.phoneNotificationsEnabled) { _ in model.notificationSettingsChanged() }
            .onChange(of: model.messageNotificationsEnabled) { _ in model.notificationSettingsChanged() }
            .onChange(of: model.appNotificationsEnabled) { _ in model.notificationSettingsChanged() }
            Text("ADB가 연결된 동안 새로 발생한 알림만 전달합니다. 알림 내용은 Flow Bridge가 별도로 저장하지 않습니다.").font(.caption).foregroundStyle(.secondary).padding(.top, 12)
        }
    }
}

private struct SettingsView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("화면 품질", icon: "display") {
                Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 12) {
                    GridRow { Text("해상도"); HStack { NumberField(value: $model.settings.width, label: "너비"); Text("×"); NumberField(value: $model.settings.height, label: "높이") } }
                    GridRow { Text("화면 밀도"); NumberField(value: $model.settings.dpi, label: "DPI") }
                    GridRow { Text("영상 품질"); HStack { NumberField(value: $model.settings.bitrate, label: "Mbps"); NumberField(value: $model.settings.fps, label: "FPS") } }
                }
                HStack { Spacer(); Button("설정 저장", action: model.save).buttonStyle(.borderedProminent) }
            }
            Card("연결과 Mac 동작", icon: "gearshape") {
                VStack(alignment: .leading, spacing: 14) {
                    Toggle("마지막 무선 주소로 자동 재연결", isOn: $model.automaticReconnect)
                    HStack { Text("Flow Bridge 로그인 자동 실행"); Spacer(); Button(model.launchAtLogin ? "끄기" : "켜기", action: model.toggleLaunchAtLogin) }
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
                    Spacer(); Button("진단 갱신", action: model.loadDiagnostics)
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
            Spacer(); Toggle("", isOn: $isOn).labelsHidden()
        }.padding(.vertical, 12)
    }
}

private struct NumberField: View {
    @Binding var value: Int; let label: String
    var body: some View { TextField(label, value: $value, format: .number).frame(width: 92).textFieldStyle(.roundedBorder) }
}
