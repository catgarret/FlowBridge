using System;
using System.Collections.Generic;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.MultiDeviceTests
{
    internal static class Program
    {
        private static int _passed;

        private static int Main()
        {
            var tests = new Action[]
            {
                MergesUsbAndWirelessForSamePhysicalDevice,
                HonorsExplicitAuthorizedTransport,
                KeepsDifferentPhysicalDevicesSeparate,
                CreatesTemporaryIdentityWhenStableIdentityIsMissing,
                IgnoresTimestampOnlyRefreshes,
                PublishesMeaningfulStatusChanges,
                RemovesMissingTransportsOnReconcile,
                ReturnsDefensiveSnapshots,
                PrefersStableAuthorizedDuplicateObservation,
                PreservesKnownDisplayName,
                PreservesKnownIdentityWhenTransportCannotBeQueried,
                RequiresExplicitSerialForDeviceCommands,
                KeepsCleanupCommandsScopedToRequestedDevice,
                InterleavedDeviceCommandsDoNotShareTarget,
                DeviceCancellationMatchesOnlyRequestedSerial,
                CreatesIndependentRuntimeSessions,
                KeepsRuntimeUpdatesScopedToOnePhysicalDevice,
                SharesRuntimeAcrossUsbAndWirelessTransports,
                MigratesTemporaryRuntimeIdentity,
                PreservesRuntimeStateAcrossDisconnect,
                IgnoresUnchangedRuntimeReconciles,
                BindsOneServiceInstancePerPhysicalDevice,
                KeepsBoundRuntimeWhenPreferredTransportChanges
            };

            try
            {
                foreach (var test in tests)
                {
                    test();
                    _passed++;
                    Console.WriteLine("PASS " + test.Method.Name);
                }

                Console.WriteLine(
                    "All multi-device foundation tests passed: " + _passed);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "FAIL after " + _passed + " tests: " + ex.Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void MergesUsbAndWirelessForSamePhysicalDevice()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });

            Equal(1, snapshot.Devices.Count, "same identity must merge");
            Equal(2, snapshot.Devices[0].Transports.Count, "both transports must remain");
            Equal(
                "USB-A",
                snapshot.Devices[0].SelectPreferredTransport(null).Serial,
                "USB must be the default transport");
        }

        private static void HonorsExplicitAuthorizedTransport()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });

            Equal(
                "192.168.0.2:5555",
                snapshot.Devices[0]
                    .SelectPreferredTransport("192.168.0.2:5555").Serial,
                "explicit authorized transport must win");
        }

        private static void KeepsDifferentPhysicalDevicesSeparate()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy", "USB-A", DeviceTransportKind.Usb),
                Device("phone-b", "Galaxy", "USB-B", DeviceTransportKind.Usb)
            });

            Equal(2, snapshot.Devices.Count, "display name must not merge devices");
            NotNull(snapshot.FindByIdentity("phone-a"), "phone-a missing");
            NotNull(snapshot.FindByIdentity("phone-b"), "phone-b missing");
        }

        private static void CreatesTemporaryIdentityWhenStableIdentityIsMissing()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(null, null, "USB-PENDING", DeviceTransportKind.Usb)
            });

            Equal(
                "transport:USB-PENDING",
                snapshot.Devices[0].Identity,
                "temporary identity must be transport-scoped");
            True(
                PhysicalDeviceRegistry.IsTemporaryIdentity(
                    snapshot.Devices[0].Identity),
                "temporary identity must be recognizable");
        }

        private static void IgnoresTimestampOnlyRefreshes()
        {
            var registry = new PhysicalDeviceRegistry();
            var eventCount = 0;
            registry.SnapshotChanged += delegate { eventCount++; };
            var first = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            var second = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });

            Equal(1L, first.Generation, "first discovery must increment generation");
            Equal(first.Generation, second.Generation, "refresh must not increment generation");
            Equal(1, eventCount, "refresh must not publish a duplicate event");
        }

        private static void PublishesMeaningfulStatusChanges()
        {
            var registry = new PhysicalDeviceRegistry();
            var eventCount = 0;
            registry.SnapshotChanged += delegate { eventCount++; };
            registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            var changed = registry.Reconcile(new[]
            {
                Device(
                    "phone-a",
                    "Galaxy A",
                    "USB-A",
                    DeviceTransportKind.Usb,
                    AdbDeviceStatus.Offline)
            });

            Equal(2L, changed.Generation, "status change must increment generation");
            Equal(2, eventCount, "status change must publish an event");
            True(!changed.Devices[0].IsConnected, "offline device must not be connected");
        }

        private static void RemovesMissingTransportsOnReconcile()
        {
            var registry = new PhysicalDeviceRegistry();
            registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });

            Equal(1, snapshot.Devices[0].Transports.Count, "missing transport must be removed");
            Equal(
                "192.168.0.2:5555",
                snapshot.Devices[0].Transports[0].Serial,
                "remaining transport is wrong");
        }

        private static void ReturnsDefensiveSnapshots()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            snapshot.Devices.Clear();

            Equal(1, registry.Current.Devices.Count, "caller must not mutate registry state");
        }

        private static void PrefersStableAuthorizedDuplicateObservation()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(null, null, "USB-A", DeviceTransportKind.Usb, AdbDeviceStatus.Offline),
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });

            Equal(1, snapshot.Devices.Count, "duplicate serial must collapse");
            Equal("phone-a", snapshot.Devices[0].Identity, "stable identity must win");
            True(snapshot.Devices[0].IsConnected, "authorized observation must win");
        }

        private static void PreservesKnownDisplayName()
        {
            var registry = new PhysicalDeviceRegistry();
            registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", null, "USB-A", DeviceTransportKind.Usb)
            });

            Equal("Galaxy A", snapshot.Devices[0].DisplayName, "known name must be cached");
        }

        private static void PreservesKnownIdentityWhenTransportCannotBeQueried()
        {
            var registry = new PhysicalDeviceRegistry();
            registry.Reconcile(new[]
            {
                Device(
                    "phone-a",
                    "Galaxy A",
                    "192.168.0.2:5555",
                    DeviceTransportKind.Wireless)
            });
            var snapshot = registry.Reconcile(new[]
            {
                Device(
                    null,
                    null,
                    "192.168.0.2:5555",
                    DeviceTransportKind.Wireless,
                    AdbDeviceStatus.Offline)
            });

            Equal(
                "phone-a",
                snapshot.Devices[0].Identity,
                "known transport must remain attached to its physical device");
            Equal(
                "Galaxy A",
                snapshot.Devices[0].DisplayName,
                "known device name must remain available while offline");
        }

        private static void RequiresExplicitSerialForDeviceCommands()
        {
            Throws<ArgumentException>(delegate
            {
                AdbCommandBuilder.ForDevice(
                    string.Empty,
                    "shell get-state");
            }, "device command without a serial must be rejected");
        }

        private static void KeepsCleanupCommandsScopedToRequestedDevice()
        {
            var first = AdbCommandBuilder.ForDevice(
                "PHONE-A",
                "shell settings delete global overlay_display_devices");
            var second = AdbCommandBuilder.ForDevice(
                "PHONE-B",
                "shell settings delete global overlay_display_devices");

            True(first.StartsWith("-s \"PHONE-A\" "),
                "first cleanup must target PHONE-A");
            True(first.IndexOf("PHONE-B", StringComparison.Ordinal) < 0,
                "first cleanup must not contain PHONE-B");
            True(second.StartsWith("-s \"PHONE-B\" "),
                "second cleanup must target PHONE-B");
            True(second.IndexOf("PHONE-A", StringComparison.Ordinal) < 0,
                "second cleanup must not contain PHONE-A");
        }

        private static void InterleavedDeviceCommandsDoNotShareTarget()
        {
            for (var index = 0; index < 1000; index++)
            {
                var serial = index % 2 == 0 ? "PHONE-A" : "PHONE-B";
                var other = index % 2 == 0 ? "PHONE-B" : "PHONE-A";
                var command = AdbCommandBuilder.ForDevice(
                    serial,
                    "shell echo " + index);
                True(command.StartsWith("-s \"" + serial + "\" "),
                    "interleaved command changed its requested target");
                True(command.IndexOf(other, StringComparison.Ordinal) < 0,
                    "interleaved command leaked another target");
            }
        }

        private static void DeviceCancellationMatchesOnlyRequestedSerial()
        {
            True(
                DeviceSerialScope.Matches("PHONE-A", "phone-a"),
                "requested device cancellation must match its own serial");
            True(
                !DeviceSerialScope.Matches("PHONE-A", "PHONE-B"),
                "requested device cancellation must not match another serial");
            True(
                !DeviceSerialScope.Matches(string.Empty, "PHONE-A"),
                "empty cancellation scope must not match a device");
        }

        private static void CreatesIndependentRuntimeSessions()
        {
            var registry = CreateRuntimeRegistry();
            var snapshot = registry.Current;
            Equal(2, snapshot.Sessions.Count,
                "each physical device needs its own runtime session");
            NotNull(snapshot.FindByIdentity("phone-a"),
                "phone-a runtime missing");
            NotNull(snapshot.FindByIdentity("phone-b"),
                "phone-b runtime missing");
        }

        private static void KeepsRuntimeUpdatesScopedToOnePhysicalDevice()
        {
            var registry = CreateRuntimeRegistry();
            registry.SetDexSession("USB-A", new ManagedDisplaySession
            {
                Serial = "USB-A",
                DisplayId = 12,
                ScrcpyProcessId = 101,
                DisplayLease = new VirtualDisplayLease
                {
                    Serial = "USB-A",
                    DisplayId = 12,
                    OwnsOverlaySetting = true
                }
            });
            registry.SetSingleWindow(
                "USB-B", 1, 21, 202, new IntPtr(303), true, false);
            registry.SetCompanionAttached("USB-B", true);
            registry.SetPcToPhoneTransferState("USB-B", 1, 4);
            registry.SetPhonePowerState(
                "USB-A", true, true, "0", true);

            var snapshot = registry.Current;
            var first = snapshot.FindByIdentity("phone-a");
            var second = snapshot.FindByIdentity("phone-b");
            True(first.Dex.IsRunning, "phone-a DeX state missing");
            Equal(0, first.SingleWindows.Count,
                "phone-b single window leaked into phone-a");
            True(first.PhonePower.ScreenOffRequested,
                "phone-a power state missing");
            True(!second.Dex.IsRunning,
                "phone-a DeX state leaked into phone-b");
            Equal(1, second.SingleWindows.Count,
                "phone-b single window state missing");
            True(second.Companion.IsAttached,
                "phone-b companion state missing");
            Equal(4, second.Transfers.QueuedPcToPhoneItems,
                "phone-b transfer queue state missing");
        }

        private static void SharesRuntimeAcrossUsbAndWirelessTransports()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "10.0.0.2:5555", DeviceTransportKind.Wireless)
            }));
            runtime.SetCompanionAttached("10.0.0.2:5555", true);

            var snapshot = runtime.Current;
            Equal(1, snapshot.Sessions.Count,
                "USB and wireless transports must share one runtime");
            True(snapshot.FindByTransportSerial("USB-A")
                    .Companion.IsAttached,
                "wireless update must be visible through USB alias");
        }

        private static void MigratesTemporaryRuntimeIdentity()
        {
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.SetPhonePowerState(
                "USB-A", true, false, string.Empty, true);
            var physical = new PhysicalDeviceRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            }));

            var snapshot = runtime.Current;
            Equal(1, snapshot.Sessions.Count,
                "temporary runtime must migrate, not duplicate");
            var migrated = snapshot.FindByIdentity("phone-a");
            NotNull(migrated, "stable runtime identity missing");
            True(migrated.PhonePower.ScreenOffRequested,
                "temporary runtime state was lost during migration");
        }

        private static void PreservesRuntimeStateAcrossDisconnect()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            }));
            runtime.SetPcToPhoneTransferState("USB-A", 1, 2);
            runtime.Reconcile(physical.Reset());

            var session = runtime.Current.FindByIdentity("phone-a");
            NotNull(session, "disconnected runtime must remain for cleanup");
            True(!session.IsConnected,
                "disconnected runtime must be marked offline");
            Equal(2, session.Transfers.QueuedPcToPhoneItems,
                "disconnect must not erase cleanup evidence");
        }

        private static void IgnoresUnchangedRuntimeReconciles()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            var devices = new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            };
            runtime.Reconcile(physical.Reconcile(devices));
            var before = runtime.Current;
            var eventCount = 0;
            runtime.Changed += delegate { eventCount++; };

            runtime.Reconcile(physical.Reconcile(devices));

            var after = runtime.Current;
            Equal(0, eventCount,
                "timestamp-only runtime refresh must not publish an event");
            Equal(before.Generation, after.Generation,
                "timestamp-only runtime refresh must not change generation");
            Equal(
                before.Sessions[0].Revision,
                after.Sessions[0].Revision,
                "timestamp-only runtime refresh must not change revision");
        }

        private static void BindsOneServiceInstancePerPhysicalDevice()
        {
            var registry = CreateRuntimeRegistry();
            var firstServices = Guid.NewGuid();
            var secondServices = Guid.NewGuid();
            var eventCount = 0;
            registry.Changed += delegate { eventCount++; };

            registry.BindServiceInstance("USB-A", firstServices);
            registry.BindServiceInstance("USB-A", firstServices);
            registry.BindServiceInstance("USB-B", secondServices);

            var snapshot = registry.Current;
            Equal(
                firstServices,
                snapshot.FindByIdentity("phone-a").ServiceInstanceId,
                "phone-a service binding missing");
            Equal(
                secondServices,
                snapshot.FindByIdentity("phone-b").ServiceInstanceId,
                "phone-b service binding missing");
            Equal(2, eventCount,
                "rebinding the same service must not publish an event");
            Throws<InvalidOperationException>(delegate
            {
                registry.BindServiceInstance("USB-A", secondServices);
            }, "a physical device must not be rebound to another service set");
        }

        private static void KeepsBoundRuntimeWhenPreferredTransportChanges()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "10.0.0.2:5555", DeviceTransportKind.Wireless),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            }));
            var phoneAService = Guid.NewGuid();
            var phoneBService = Guid.NewGuid();
            runtime.BindServiceInstance("USB-A", phoneAService);
            runtime.BindServiceInstance("USB-B", phoneBService);
            runtime.SetDexSession("USB-A", new ManagedDisplaySession
            {
                Serial = "USB-A",
                DisplayId = 21,
                ScrcpyProcessId = 101
            });

            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "10.0.0.2:5555", DeviceTransportKind.Wireless),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            }));

            var snapshot = runtime.Current;
            var phoneA = snapshot.FindByIdentity("phone-a");
            var phoneB = snapshot.FindByIdentity("phone-b");
            Equal(phoneAService, phoneA.ServiceInstanceId,
                "phone-a tab must retain its runtime after transport change");
            Equal("10.0.0.2:5555", phoneA.ActiveTransportSerial,
                "phone-a tab must select the remaining wireless transport");
            True(phoneA.Dex.IsRunning,
                "phone-a runtime evidence must survive transport change");
            Equal(phoneBService, phoneB.ServiceInstanceId,
                "phone-b runtime binding must remain isolated");
            True(!phoneB.Dex.IsRunning,
                "phone-a session must not leak into phone-b tab");
        }

        private static DeviceRuntimeSessionRegistry CreateRuntimeRegistry()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            }));
            return runtime;
        }

        private static DiscoveredDeviceTransport Device(
            string identity,
            string name,
            string serial,
            DeviceTransportKind kind,
            AdbDeviceStatus status = AdbDeviceStatus.Device)
        {
            return new DiscoveredDeviceTransport
            {
                DeviceIdentity = identity,
                DisplayName = name,
                Serial = serial,
                Kind = kind,
                Status = status,
                RawStatus = status.ToString().ToLowerInvariant()
            };
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + "; expected=" + expected + ", actual=" + actual);
            }
        }

        private static void NotNull(object value, string message)
        {
            if (value == null) throw new InvalidOperationException(message);
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Throws<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
