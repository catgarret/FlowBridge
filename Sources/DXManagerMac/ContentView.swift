import SwiftUI
import DXManagerCore

private func localized(_ key: String) -> String { NSLocalizedString(key, comment: "") }

private enum AppSection: String, CaseIterable, Identifiable {
    case home = "홈", phone = "전화·문자", apps = "앱 창", transfer = "파일 전송", notifications = "알림", settings = "설정", diagnostics = "진단"
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
        }
    }
}

struct ContentView: View {
    @EnvironmentObject private var model: AppModel
    @State private var section: AppSection? = .home

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
                        }
                    }.frame(maxWidth: 920, alignment: .leading).padding(28)
                }
                statusBar
            }.background(Color(nsColor: .windowBackgroundColor))
        }
        .frame(minWidth: 980, minHeight: 680)
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
        case .apps: return "자주 사용하는 Android 앱을 각각 독립 창으로 실행합니다."
        case .transfer: return "파일과 폴더를 Galaxy의 Download 폴더로 보냅니다."
        case .notifications: return "전화·문자·앱 알림을 Mac 알림 센터로 전달합니다."
        case .settings: return "화면 품질, 자동 연결과 Mac 동작을 설정합니다."
        case .diagnostics: return "기기 정보와 DX Companion 상태를 확인합니다."
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
                    HStack {
                        TextField(localized("무선 ADB 주소  예: 172.30.1.3:44065"), text: $model.wirelessEndpoint)
                        Button("연결", action: model.connectWireless).buttonStyle(.borderedProminent)
                    }
                    DisclosureGroup("처음 무선 연결할 때만 페어링") {
                        HStack {
                            TextField(localized("페어링 IP:포트"), text: $model.pairingEndpoint)
                            SecureField(localized("6자리 코드"), text: $model.pairingCode).frame(width: 120)
                            Button("페어링", action: model.pairWireless)
                        }.padding(.top, 10)
                    }.font(.subheadline)
                }
            }
            Card("화면 열기", icon: "macwindow.on.rectangle") {
                HStack(spacing: 14) {
                    LaunchTile(title: "Samsung DeX", subtitle: "데스크톱 화면", icon: "display", tint: .blue, action: model.startDeX)
                    LaunchTile(title: "휴대폰 미러링", subtitle: "기본 화면 그대로", icon: "iphone", tint: .purple, action: model.startPhoneMirror)
                }
                HStack {
                    Button(role: .destructive, action: model.stop) { Label("모든 화면 중지 및 정리", systemImage: "stop.fill") }
                    Spacer()
                    Button { model.sendKeyEvent(224, label: "화면 켜기") } label: { Label("화면 켜기", systemImage: "sun.max") }
                    Button { model.sendKeyEvent(223, label: "화면 끄기") } label: { Label("화면 끄기", systemImage: "moon") }
                    Button { model.sendKeyEvent(26, label: "전원") } label: { Label("전원", systemImage: "power") }
                }.padding(.top, 4)
            }
            Card("빠른 작업", icon: "bolt") {
                HStack {
                    Button(action: model.chooseAndTransfer) { Label("파일·폴더 보내기", systemImage: "arrow.up.doc") }
                    Button(action: model.captureRegion) { Label("화면 영역 캡처", systemImage: "viewfinder") }
                    Button(action: model.loadPackages) { Label("앱 목록 갱신", systemImage: "square.grid.2x2") }
                }
            }
        }
    }
}

private struct AppsView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        Card("독립 앱 창", icon: "macwindow.badge.plus") {
            VStack(spacing: 0) {
                ForEach(0..<3, id: \.self) { slot in
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Text("앱 \(slot + 1)").font(.headline).frame(width: 52, alignment: .leading)
                            Picker("실행할 앱", selection: $model.packageNames[slot]) {
                                if !model.installedApps.contains(where: { $0.package == model.packageNames[slot] }) {
                                    Text(model.packageNames[slot] == "com.android.settings" ? "설정" : "앱을 선택해 주세요").tag(model.packageNames[slot])
                                }
                                ForEach(model.installedApps) { app in Text(app.name).tag(app.package) }
                            }.labelsHidden().frame(maxWidth: .infinity)
                            Button("실행") { model.startApp(slot: slot) }.buttonStyle(.borderedProminent)
                        }
                        HStack {
                            Button("현재 화면 설정 저장") { model.saveAppProfile(slot: slot) }
                            Button("저장 설정 적용") { model.applyAppProfile(slot: slot) }
                        }.controlSize(.small)
                    }.padding(.vertical, 14)
                    if slot < 2 { Divider() }
                }
            }
            HStack {
                Button(action: model.loadPackages) { Label("Galaxy 앱 목록 갱신", systemImage: "arrow.clockwise") }
                Spacer(); Text("각 앱은 별도 scrcpy 가상 화면으로 열립니다.").font(.caption).foregroundStyle(.secondary)
            }.padding(.top, 8)
        }
    }
}

private struct PhoneView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Card("전화 걸기", icon: "phone") {
                Text("Mac에서 번호를 입력하면 Galaxy 전화 화면에 바로 전달됩니다.").foregroundStyle(.secondary)
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
            Card("수신 확인", icon: "bell") {
                Text("전화와 문자 알림을 켜면 Galaxy에 새로 도착한 항목을 Mac 알림 센터에서 확인할 수 있습니다.").foregroundStyle(.secondary)
                HStack {
                    Toggle("전화 알림", isOn: $model.phoneNotificationsEnabled)
                    Toggle("문자 알림", isOn: $model.messageNotificationsEnabled)
                }.toggleStyle(.switch)
                    .onChange(of: model.phoneNotificationsEnabled) { _ in model.notificationSettingsChanged() }
                    .onChange(of: model.messageNotificationsEnabled) { _ in model.notificationSettingsChanged() }
            }
        }
    }
}

private struct TransferView: View {
    @EnvironmentObject private var model: AppModel
    var body: some View {
        Card("Mac에서 Galaxy로", icon: "arrow.up.circle") {
            VStack(spacing: 16) {
                Image(systemName: "doc.on.doc").font(.system(size: 42)).foregroundStyle(.blue)
                Text("파일 또는 폴더를 선택하면 Galaxy의 Download 폴더로 전송합니다.").multilineTextAlignment(.center).foregroundStyle(.secondary)
                Button(action: model.chooseAndTransfer) { Label("파일·폴더 선택", systemImage: "plus") }.buttonStyle(.borderedProminent).controlSize(.large)
                if model.isTransferring { ProgressView().frame(maxWidth: 420); Text(model.transferStatus).font(.caption); Button("전송 취소", action: model.cancelTransfer) }
            }.frame(maxWidth: .infinity).padding(.vertical, 36)
        }
    }
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
            Card("DX Companion", icon: "cross.case") {
                Text("비정상 종료 후 Galaxy에 남은 DeX 가상 화면을 안전하게 복구합니다.").foregroundStyle(.secondary)
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
