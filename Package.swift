// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "DXManagerMac",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "DXManagerMac", targets: ["DXManagerMac"])],
    targets: [
        .target(name: "DXManagerCore"),
        .executableTarget(name: "DXManagerMac", dependencies: ["DXManagerCore"]),
        .executableTarget(name: "DXManagerCoreTests", dependencies: ["DXManagerCore"])
    ]
)
