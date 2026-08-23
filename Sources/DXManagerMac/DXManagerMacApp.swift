import SwiftUI

@main
struct DXManagerMacApp: App {
    @StateObject private var model = AppModel()

    var body: some Scene {
        WindowGroup { ContentView().environmentObject(model).onAppear(perform: model.configureLaunchPresentation) }
        .commands { CommandGroup(replacing: .appTermination) { Button("Flow Bridge 종료", action: model.quit).keyboardShortcut("q") } }
        MenuBarExtra("Flow Bridge", systemImage: "rectangle.connected.to.line.below", isInserted: Binding(get: { model.showsMenuBarIcon }, set: { _ in })) {
            Button("Flow Bridge 열기", action: model.showMainWindow)
            Button("데스크톱 모드 시작", action: model.startDeX)
            Button("세션 중지 및 정리", action: model.stop)
            Divider()
            Button("Flow Bridge 종료", action: model.quit)
        }
    }
}
