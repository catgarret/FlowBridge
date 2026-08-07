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
                PreservesKnownIdentityWhenTransportCannotBeQueried
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
    }
}
