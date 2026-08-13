using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DexManager.Services
{
    /// <summary>
    /// Final safety net for helper executables shipped with DX Manager.
    /// Normal service shutdown still owns the primary process lifecycle; this
    /// class removes detached ADB servers and any orphaned bundled helpers after
    /// the WinForms message loop has ended.
    /// </summary>
    public sealed class BundledProcessCleanupService
    {
        private const int ExitWaitMs = 1500;
        private const int SweepCount = 2;
        private readonly object _sync = new object();
        private readonly List<string> _executablePaths =
            new List<string>();
        private readonly LogService _logService;

        public BundledProcessCleanupService(LogService logService)
        {
            _logService = logService ??
                throw new ArgumentNullException("logService");
        }

        public void AddExecutablePath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath)) return;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(executablePath);
            }
            catch
            {
                return;
            }

            lock (_sync)
            {
                foreach (var existing in _executablePaths)
                {
                    if (string.Equals(
                        existing,
                        fullPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                _executablePaths.Add(fullPath);
            }
        }

        public int TerminateRemainingProcesses()
        {
            string[] paths;
            lock (_sync) paths = _executablePaths.ToArray();

            var terminatedProcessIds = new HashSet<int>();
            for (var sweep = 0; sweep < SweepCount; sweep++)
            {
                foreach (var path in paths)
                    TerminateProcessesAtPath(path, terminatedProcessIds);
            }

            if (terminatedProcessIds.Count > 0)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Process.FinalSweepTerminated",
                    terminatedProcessIds.Count));
            }
            return terminatedProcessIds.Count;
        }

        private static void TerminateProcessesAtPath(
            string expectedPath,
            ISet<int> terminatedProcessIds)
        {
            var processName = Path.GetFileNameWithoutExtension(expectedPath);
            if (string.IsNullOrWhiteSpace(processName)) return;

            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                return;
            }

            int currentProcessId;
            using (var currentProcess = Process.GetCurrentProcess())
                currentProcessId = currentProcess.Id;
            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == currentProcessId ||
                            process.HasExited)
                        {
                            continue;
                        }

                        var module = process.MainModule;
                        var actualPath = module == null
                            ? null
                            : module.FileName;
                        if (string.IsNullOrWhiteSpace(actualPath)) continue;

                        actualPath = Path.GetFullPath(actualPath);
                        if (!string.Equals(
                            actualPath,
                            expectedPath,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var processId = process.Id;
                        process.Kill();
                        try { process.WaitForExit(ExitWaitMs); }
                        catch { }
                        terminatedProcessIds.Add(processId);
                    }
                    catch
                    {
                        // The process may exit between enumeration and path
                        // inspection. Application shutdown must still finish.
                    }
                }
            }
        }
    }
}
