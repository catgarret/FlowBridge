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
                DeviceCancellationMatchesOnlyRequestedSerial
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
