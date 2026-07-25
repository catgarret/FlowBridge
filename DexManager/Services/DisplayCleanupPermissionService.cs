using System;
using System.IO;
using System.Text.RegularExpressions;
using DexManager.Utils;

namespace DexManager.Services
{
    internal enum DisplayCleanupPermissionState
    {
        NoDevice,
        NotInstalled,
        VerificationFailed,
        Ready,
        Granted,
        Error
    }

    internal sealed class DisplayCleanupPermissionStatus
    {
        public DisplayCleanupPermissionState State { get; set; }
        public string Detail { get; set; }
        public string Serial { get; set; }
    }

    internal sealed class DisplayCleanupPermissionService
    {
        public const string PackageName =
            "io.github.mazemei.dxdisplaycleanup";
        public const string PermissionName =
            "android.permission.WRITE_SECURE_SETTINGS";

        private const string ExpectedCertificateSha256 =
            "AD615803C63760439750C36801E8152AB8664C60EE481EF1473F1DF5E80733BE";

        private readonly AdbService _adbService;

        public DisplayCleanupPermissionService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public DisplayCleanupPermissionStatus Inspect()
        {
            var serial = _adbService.TargetSerial;
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return Status(
                    DisplayCleanupPermissionState.NoDevice,
                    string.Empty,
                    serial);
            }

            try
            {
                var packageDump = _adbService.ShellForSerial(
                    serial,
                    "dumpsys package " + PackageName,
                    false);
                var packagePath = _adbService.ShellForSerial(
                    serial,
                    "pm path " + PackageName,
                    false);
                if (!packageDump.IsSuccess || !packagePath.IsSuccess ||
                    string.IsNullOrWhiteSpace(packagePath.StandardOutput))
                {
                    return Status(
                        DisplayCleanupPermissionState.NotInstalled,
                        CombineError(packageDump, packagePath),
                        serial);
                }

                var remoteApkPath = ParseBaseApkPath(
                    packagePath.StandardOutput);
                if (string.IsNullOrWhiteSpace(remoteApkPath) ||
                    !Regex.IsMatch(
                        packageDump.StandardOutput ?? string.Empty,
                        @"\bapkSigningVersion=2\b",
                        RegexOptions.CultureInvariant))
                {
                    return Status(
                        DisplayCleanupPermissionState.VerificationFailed,
                        "Installed package is not the expected v2-signed APK.",
                        serial);
                }

                var certificate = ReadInstalledCertificate(
                    serial,
                    remoteApkPath);
                if (!string.Equals(
                    certificate,
                    ExpectedCertificateSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Status(
                        DisplayCleanupPermissionState.VerificationFailed,
                        "Installed APK signing certificate does not match.",
                        serial);
                }

                return Status(
                    HasPermission(packageDump.StandardOutput)
                        ? DisplayCleanupPermissionState.Granted
                        : DisplayCleanupPermissionState.Ready,
                    string.Empty,
                    serial);
            }
            catch (Exception ex)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    ex.Message,
                    serial);
            }
        }

        public DisplayCleanupPermissionStatus Grant(
            DisplayCleanupPermissionStatus verifiedStatus)
        {
            if (verifiedStatus == null ||
                verifiedStatus.State !=
                    DisplayCleanupPermissionState.Ready ||
                string.IsNullOrWhiteSpace(verifiedStatus.Serial))
            {
                return Status(
                    DisplayCleanupPermissionState.VerificationFailed,
                    "The installed cleanup app has not been verified.",
                    verifiedStatus == null
                        ? string.Empty
                        : verifiedStatus.Serial);
            }

            var serial = verifiedStatus.Serial;
            if (!string.Equals(
                serial,
                _adbService.TargetSerial,
                StringComparison.OrdinalIgnoreCase) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return Status(
                    DisplayCleanupPermissionState.NoDevice,
                    string.Empty,
                    serial);
            }

            // Re-verify immediately before granting so a package replacement
            // or target-device change cannot reuse an earlier UI state.
            var current = Inspect();
            if (current.State == DisplayCleanupPermissionState.Granted)
                return current;
            if (current.State != DisplayCleanupPermissionState.Ready ||
                !string.Equals(
                    current.Serial,
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            var grantResult = _adbService.ShellForSerial(
                serial,
                "pm grant " + PackageName + " " + PermissionName,
                true);
            if (!grantResult.IsSuccess)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    CombineOutput(grantResult),
                    serial);
            }

            var verified = Inspect();
            if (verified.State != DisplayCleanupPermissionState.Granted)
            {
                // Close the package-replacement race window: if the app no
                // longer verifies immediately after pm grant, revoke the
                // permission before reporting failure.
                _adbService.ShellForSerial(
                    serial,
                    "pm revoke " + PackageName + " " + PermissionName,
                    true);
                return Status(
                    DisplayCleanupPermissionState.Error,
                    "Post-grant verification failed. The permission was revoked.",
                    serial);
            }
            return verified;
        }

        private string ReadInstalledCertificate(
            string serial,
            string remoteApkPath)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "DXManager",
                "apk-verification",
                Guid.NewGuid().ToString("N"));
            var localApkPath = Path.Combine(directory, "base.apk");
            try
            {
                Directory.CreateDirectory(directory);
                var pull = _adbService.PullForSerial(
                    serial,
                    remoteApkPath,
                    localApkPath,
                    false);
                if (!pull.IsSuccess || !File.Exists(localApkPath))
                    throw new InvalidOperationException(
                        "Could not read the installed cleanup APK: " +
                        CombineOutput(pull));
                return ApkSigningCertificateReader
                    .ReadSingleV2CertificateSha256(localApkPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch
                {
                    // Verification result must not be changed by temp cleanup.
                }
            }
        }

        private static string ParseBaseApkPath(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return string.Empty;
            var lines = output.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith(
                    "package:",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var path = line.Substring("package:".Length).Trim();
                if (path.EndsWith(
                    "/base.apk",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return string.Empty;
        }

        private static bool HasPermission(string packageDump)
        {
            if (string.IsNullOrWhiteSpace(packageDump)) return false;
            return Regex.IsMatch(
                packageDump,
                @"android\.permission\.WRITE_SECURE_SETTINGS\s*:\s*granted=true\b",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase);
        }

        private static DisplayCleanupPermissionStatus Status(
            DisplayCleanupPermissionState state,
            string detail,
            string serial)
        {
            return new DisplayCleanupPermissionStatus
            {
                State = state,
                Detail = detail ?? string.Empty,
                Serial = serial ?? string.Empty
            };
        }

        private static string CombineError(params Models.ProcessResult[] results)
        {
            foreach (var result in results)
            {
                var value = CombineOutput(result);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return string.Empty;
        }

        private static string CombineOutput(Models.ProcessResult result)
        {
            if (result == null) return string.Empty;
            var value = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            return (value ?? string.Empty).Trim();
        }
    }
}
