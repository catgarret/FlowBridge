import AppKit
import ApplicationServices

final class KeyboardCorrectionService: @unchecked Sendable {
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    private let lock = NSLock()
    private var targetPIDs = Set<pid_t>()
    private var shiftEnter = false

    func setTargets(_ pids: Set<pid_t>) { lock.lock(); targetPIDs = pids; lock.unlock() }
    func setShiftEnter(_ enabled: Bool) { lock.lock(); shiftEnter = enabled; lock.unlock() }

    func start(prompt: Bool) -> Bool {
        if prompt {
            let options = ["AXTrustedCheckOptionPrompt": true] as CFDictionary
            guard AXIsProcessTrustedWithOptions(options) else { return false }
        } else if !AXIsProcessTrusted() { return false }
        guard tap == nil else { return true }
        let mask = (1 << CGEventType.flagsChanged.rawValue) | (1 << CGEventType.keyDown.rawValue) | (1 << CGEventType.keyUp.rawValue)
        let pointer = Unmanaged.passUnretained(self).toOpaque()
        guard let created = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap,
                                              options: .defaultTap, eventsOfInterest: CGEventMask(mask),
                                              callback: Self.callback, userInfo: pointer) else { return false }
        tap = created
        source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, created, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: created, enable: true)
        return true
    }

    func stop() {
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        if let tap { CGEvent.tapEnable(tap: tap, enable: false) }
        source = nil; tap = nil
    }

    private static let callback: CGEventTapCallBack = { _, type, event, pointer in
        guard let pointer else { return Unmanaged.passUnretained(event) }
        let service = Unmanaged<KeyboardCorrectionService>.fromOpaque(pointer).takeUnretainedValue()
        return service.handle(type: type, event: event)
    }

    private func handle(type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout { if let tap { CGEvent.tapEnable(tap: tap, enable: true) }; return Unmanaged.passUnretained(event) }
        guard let frontPID = NSWorkspace.shared.frontmostApplication?.processIdentifier else { return Unmanaged.passUnretained(event) }
        lock.lock(); let targeted = targetPIDs.contains(frontPID); let useShiftEnter = shiftEnter; lock.unlock()
        guard targeted else { return Unmanaged.passUnretained(event) }
        let keyCode = event.getIntegerValueField(.keyboardEventKeycode)
        if type == .flagsChanged, keyCode == 60 {
            let down = event.flags.contains(.maskShift)
            CGEvent(keyboardEventSource: nil, virtualKey: 56, keyDown: down)?.postToPid(frontPID)
            return nil
        }
        if useShiftEnter, keyCode == 36, type == .keyDown || type == .keyUp {
            guard let replacement = event.copy() else { return Unmanaged.passUnretained(event) }
            replacement.flags.insert(.maskShift)
            replacement.postToPid(frontPID)
            return nil
        }
        return Unmanaged.passUnretained(event)
    }
}
