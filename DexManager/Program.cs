using System;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager
{
    internal static class Program
    {
        private const string SingleInstanceName =
            "DexManager-73D79582-CC69-4AEC-A24E-F3755E77A32C";
        private static Mutex _singleInstanceMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(
                true,
                SingleInstanceName,
                out createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var logService = new LogService();

            try
            {
                var settingsService = new SettingsService(logService);
                var settings = settingsService.Load();
                LocalizationService.Apply(settings.Language);
                logService.SetLogDirectory(
                    settingsService.ResolvePath(settings.Paths.LogFolder));
                logService.Info(
                    LocalizationService.Get("Log.Program.Starting"));

                var processRunner = new ProcessRunner(logService);
                var pathService = new PathService(
                    settingsService,
                    logService,
                    processRunner);
                var autoStartService = new AutoStartService(logService);
                try
                {
                    autoStartService.Apply(
                        settings.Features.StartWithWindows);
                }
                catch (Exception ex)
                {
                    logService.Error(
                        LocalizationService.Get(
                            "Log.Main.AutoStartApplyFailed"),
                        ex);
                }
                var adbPath = pathService.SelectAdbPath(
                    settings,
                    settings.Timing.ProcessTimeoutMs);
                Environment.SetEnvironmentVariable(
                    "ADB",
                    adbPath,
                    EnvironmentVariableTarget.Process);
                logService.Info(LocalizationService.Format(
                    "Log.Program.ScrcpyAdbPath",
                    adbPath));
                var adbService = new AdbService(
                    adbPath,
                    settings.Timing.ProcessTimeoutMs,
                    processRunner,
                    logService);
                var physicalDeviceRegistry =
                    new PhysicalDeviceRegistry();
                var runtimeSessions =
                    new DeviceRuntimeSessionRegistry();
                physicalDeviceRegistry.SnapshotChanged += delegate(
                    object sender,
                    Models.DeviceRegistrySnapshotChangedEventArgs eventArgs)
                {
                    runtimeSessions.Reconcile(eventArgs.Current);
                };
                var wirelessAdbService = new WirelessAdbService(
                    adbService,
                    settingsService,
                    settings,
                    logService);
                wirelessAdbService.InitializeTarget();
                var scrcpyPath = settingsService.ResolvePath(settings.Paths.ScrcpyPath);
                var scrcpyLaunchCoordinator =
                    new ScrcpyLaunchCoordinator();
                var runtimeServiceFactory =
                    new DeviceRuntimeServiceFactory(
                    scrcpyPath,
                    adbPath,
                    settings.Timing.ProcessTimeoutMs,
                    processRunner,
                    adbService,
                    scrcpyLaunchCoordinator,
                    settingsService,
                    settings,
                    logService,
                    runtimeSessions);
                var activeRuntime = runtimeServiceFactory.Create();
                var fileTransferCoordinator =
                    activeRuntime.FileTransfers;
                var phoneTransferReceiver =
                    activeRuntime.PhoneTransfers;
                var scrcpyService = activeRuntime.Scrcpy;
                var singleWindowService = activeRuntime.SingleWindows;
                var screenOffService = activeRuntime.ScreenOff;
                var orchestrator = activeRuntime.Dex;
                var deviceMonitor = new DeviceMonitorService(
                    adbService,
                    wirelessAdbService,
                    physicalDeviceRegistry,
                    logService,
                    settings.Timing.DeviceMonitorIntervalMs,
                    settings.Timing.DisconnectMonitorIntervalMs);
                var hotkeyService = new HotkeyService(
                    logService,
                    settings);
                var captureService = new CaptureService(
                    adbService,
                    settingsService,
                    settings,
                    logService);
                var captureCoordinator = new CaptureCoordinator(
                    hotkeyService,
                    captureService,
                    scrcpyService,
                    singleWindowService,
                    settings,
                    logService);
                var autoHideService = new AutoHideService(
                    scrcpyService,
                    singleWindowService,
                    logService,
                    settings.Timing.AutoHideIdleSeconds);
                var environmentCheckService = new EnvironmentCheckService(
                    adbService,
                    scrcpyService,
                    pathService,
                    logService,
                    settingsService,
                    settings,
                    () => wirelessAdbService.SelectedSerial);
                var keyMappingService = new KeyMappingService(
                    scrcpyService,
                    singleWindowService,
                    adbService,
                    settings,
                    settings.KeyMappings,
                    logService);

                Application.Run(new MainForm(
                    settingsService,
                    settings,
                    logService,
                    adbService,
                    wirelessAdbService,
                    scrcpyService,
                    singleWindowService,
                    screenOffService,
                    scrcpyLaunchCoordinator,
                    deviceMonitor,
                    orchestrator,
                    captureCoordinator,
                    autoHideService,
                    autoStartService,
                    environmentCheckService,
                    keyMappingService,
                    fileTransferCoordinator,
                    phoneTransferReceiver,
                    runtimeSessions,
                    activeRuntime,
                    physicalDeviceRegistry,
                    runtimeServiceFactory,
                    pathService,
                    captureService,
                    IsAutoRun(args)));
            }
            catch (Exception ex)
            {
                logService.Error(
                    LocalizationService.Get("Log.Program.InitFailed"),
                    ex);
                MessageBox.Show(
                    LocalizationService.Format(
                        "Program.InitFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
        }

        private static bool IsAutoRun(string[] args)
        {
            if (args == null) return false;

            foreach (var argument in args)
            {
                if (string.Equals(
                    argument,
                    "--autorun",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
