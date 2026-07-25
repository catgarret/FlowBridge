using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.FileTransfer;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class FileTransferCoordinator : IDisposable
    {
        private const int MaximumFileNameBytes = 255;
        private const int MaximumCollisionIndex = 9999;
        private const int CancelBurstMilliseconds = 1000;
        private const int ShortAdbTimeoutMs = 5000;
        private const int FinalCommitRecoveryAttempts = 6;
        private const int StaleCleanupCooldownMilliseconds = 5000;
        private const int ProcessPollMilliseconds = 100;
        private const int MaximumVisibleQueueItems = 5;
        private readonly object _syncRoot = new object();
        private readonly object _targetPreparationRoot = new object();
        private readonly string _realAdbPath;
        private readonly string _proxyPath;
        private readonly string _pipeName;
        private readonly string _pipeToken;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly BlockingCollection<TransferWorkItem> _queue =
            new BlockingCollection<TransferWorkItem>(
                new ConcurrentQueue<TransferWorkItem>());
        private readonly Dictionary<string, TransferSession> _sessions =
            new Dictionary<string, TransferSession>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TransferWorkItem> _requests =
            new Dictionary<string, TransferWorkItem>(
                StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<NamedPipeServerStream> _connectedPipes =
            new HashSet<NamedPipeServerStream>();
        private readonly Thread _acceptThread;
        private readonly Thread _workerThread;
        private NamedPipeServerStream _waitingPipe;
        private TransferWorkItem _activeItem;
        private Process _activeAdbProcess;
        private int _shutdownRequested;
        private int _disposed;
        private bool _proxyMissingLogged;
        private DateTime _lastStaleCleanupUtc = DateTime.MinValue;
        private long _progressSequence;

        public FileTransferCoordinator(
            string realAdbPath,
            AppSettings settings,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(realAdbPath))
                throw new ArgumentException("ADB path is empty.", "realAdbPath");
            _realAdbPath = Path.GetFullPath(realAdbPath);
            _settings = settings ?? throw new ArgumentNullException("settings");
            _logService = logService ?? throw new ArgumentNullException("logService");
            _proxyPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "tools",
                "adb-proxy",
                "DXMAdbProxy.exe");
            _pipeName = "DXManager.Transfer." +
                Process.GetCurrentProcess().Id.ToString(
                    CultureInfo.InvariantCulture) + "." +
                Guid.NewGuid().ToString("N");
            _pipeToken = CreateToken();

            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "DX Manager file-transfer IPC"
            };
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "DX Manager file-transfer worker"
            };
            _acceptThread.Start();
            _workerThread.Start();
        }

        public event EventHandler<FileTransferProgressEventArgs> ProgressChanged;

        public string GetScrcpyPushTarget()
        {
            return NormalizeRemoteDirectory(
                _settings.Paths.FileTransferTargetFolder) + "/";
        }

        public string PrepareScrcpyPushTarget(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return GetScrcpyPushTarget();
            lock (_targetPreparationRoot)
            {
                var directory = NormalizeRemoteDirectory(
                    _settings.Paths.FileTransferTargetFolder);
                if (CanCleanupStaleTransferArtifacts())
                    CleanupStaleRemoteTransferArtifacts(serial, directory);
                try
                {
                    var script = new StringBuilder();
                    script.AppendLine("set -e");
                    AppendDecodedVariable(script, "dir", directory);
                    script.AppendLine("mkdir -p \"$dir\"");
                    script.AppendLine("[ -d \"$dir\" ]");
                    var result = RunCleanupScript(serial, script);
                    if (result.ExitCode == 0) return directory + "/";
                    var detail = string.IsNullOrWhiteSpace(result.ErrorTail)
                        ? result.OutputTail
                        : result.ErrorTail;
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TargetPrepareFailed",
                        directory,
                        detail));
                }
                catch (Exception ex)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TargetPrepareFailed",
                        directory,
                        ex.Message));
                }
                return directory + "/";
            }
        }

        private bool CanCleanupStaleTransferArtifacts()
        {
            lock (_syncRoot)
            {
                return _sessions.Count == 0 &&
                    _requests.Count == 0 &&
                    (DateTime.UtcNow - _lastStaleCleanupUtc)
                        .TotalMilliseconds >=
                        StaleCleanupCooldownMilliseconds;
            }
        }

        private void CleanupStaleRemoteTransferArtifacts(
            string serial,
            string directory)
        {
            try
            {
                var script = new StringBuilder();
                script.AppendLine("set -e");
                AppendDecodedVariable(script, "dir", directory);
                script.AppendLine("rm -f /sdcard/.dxm-file-*.part");
                script.AppendLine("for path in \"$dir\"/.dxm-dir-*.part; do");
                script.AppendLine("  [ -e \"$path\" ] || continue");
                script.AppendLine("  rm -rf \"$path\"");
                script.AppendLine("done");
                script.AppendLine(
                    "rm -f /data/local/tmp/.dxm-commit-*.result");
                var result = RunCleanupScript(serial, script);
                if (result.ExitCode == 0) return;
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    GetCleanupFailure(directory, result)));
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    ex.Message));
            }
            finally
            {
                lock (_syncRoot) _lastStaleCleanupUtc = DateTime.UtcNow;
            }
        }

        public string BeginSession(
            string serial,
            string displayName,
            string remoteDirectory)
        {
            if (string.IsNullOrWhiteSpace(serial)) return string.Empty;
            if (!_settings.Features.ManagedFileTransferEnabled)
                return string.Empty;
            if (Interlocked.CompareExchange(
                    ref _shutdownRequested,
                    0,
                    0) != 0)
            {
                return string.Empty;
            }
            if (!File.Exists(_proxyPath))
            {
                lock (_syncRoot)
                {
                    if (!_proxyMissingLogged)
                    {
                        _proxyMissingLogged = true;
                        _logService.Warning(LocalizationService.Format(
                            "Log.FileTransfer.ProxyMissing",
                            _proxyPath));
                    }
                }
                return string.Empty;
            }

            var id = Guid.NewGuid().ToString("N");
            lock (_syncRoot)
            {
                _sessions[id] = new TransferSession(
                    id,
                    serial,
                    displayName,
                    NormalizeRemoteDirectory(remoteDirectory));
            }
            return id;
        }

        public void ConfigureScrcpyProcess(
            ProcessStartInfo startInfo,
            string sessionId)
        {
            if (startInfo == null) throw new ArgumentNullException("startInfo");
            TransferSession session;
            lock (_syncRoot)
            {
                if (string.IsNullOrWhiteSpace(sessionId) ||
                    !_sessions.TryGetValue(sessionId, out session) ||
                    !session.Active)
                {
                    startInfo.EnvironmentVariables["ADB"] = _realAdbPath;
                    return;
                }
            }

            startInfo.EnvironmentVariables["ADB"] = _proxyPath;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.RealAdbPath] = _realAdbPath;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.PipeName] = _pipeName;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.PipeToken] = _pipeToken;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.SessionId] = session.Id;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.SessionSerial] = session.Serial;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.RemoteDirectory] =
                    session.RemoteDirectory + "/";
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.Enabled] = "1";
        }

        public void BindProcess(string sessionId, int processId)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                if (_sessions.TryGetValue(sessionId ?? string.Empty,
                    out session))
                {
                    session.ProcessId = processId;
                }
            }
        }

        public void BindWindow(string sessionId, IntPtr windowHandle)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                if (_sessions.TryGetValue(sessionId ?? string.Empty,
                    out session))
                {
                    session.WindowHandle = windowHandle;
                }
            }
        }

        public IntPtr GetWindowHandle(string sessionId)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                return _sessions.TryGetValue(sessionId ?? string.Empty,
                    out session)
                    ? session.WindowHandle
                    : IntPtr.Zero;
            }
        }

        public void EndSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            TransferSession session;
            lock (_syncRoot)
            {
                if (!_sessions.TryGetValue(sessionId, out session)) return;
                session.Active = false;
                session.WindowHandle = IntPtr.Zero;
            }
            CancelSessionRequests(sessionId, false);
            lock (_syncRoot) _sessions.Remove(sessionId);
        }

        public void CancelSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            string[] sessions;
            TransferWorkItem[] requests;
            lock (_syncRoot)
            {
                sessions = _sessions.Values
                    .Where(item => string.Equals(
                        item.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Id)
                    .ToArray();
                var sessionSet = new HashSet<string>(
                    sessions,
                    StringComparer.OrdinalIgnoreCase);
                requests = _requests.Values
                    .Where(item => sessionSet.Contains(
                        item.Request.SessionId) &&
                        !item.IsTerminal)
                    .ToArray();
            }
            if (requests.Length > 0)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.DeviceDisconnected",
                    serial,
                    requests.Length));
            }
            foreach (var sessionId in sessions)
                CancelSessionRequests(sessionId, false);
        }

        public void CancelTransfer(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return;
            TransferWorkItem item;
            lock (_syncRoot)
                _requests.TryGetValue(requestId, out item);
            if (item != null)
                CancelSessionRequests(item.Request.SessionId, true);
        }

        public void RequestShutdown()
        {
            if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0) return;

            NamedPipeServerStream waiting;
            NamedPipeServerStream[] connected;
            lock (_syncRoot)
            {
                waiting = _waitingPipe;
                connected = _connectedPipes.ToArray();
                foreach (var session in _sessions.Values)
                    session.Active = false;
            }
            if (waiting != null)
            {
                try { waiting.Dispose(); }
                catch { }
            }
            foreach (var pipe in connected)
            {
                try { pipe.Dispose(); }
                catch { }
            }

            TransferWorkItem active;
            lock (_syncRoot) active = _activeItem;
            if (active != null) CancelItem(active);
            foreach (var item in _queue.ToArray()) CancelItem(item);
            _queue.CompleteAdding();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            RequestShutdown();
            if (Thread.CurrentThread != _acceptThread)
                _acceptThread.Join(2000);
            var workerStopped = true;
            if (Thread.CurrentThread != _workerThread)
                workerStopped = _workerThread.Join(7000);
            if (workerStopped) _queue.Dispose();
        }

        private void AcceptLoop()
        {
            while (Interlocked.CompareExchange(
                ref _shutdownRequested,
                0,
                0) == 0)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = CreatePipeServer();
                    lock (_syncRoot) _waitingPipe = pipe;
                    pipe.WaitForConnection();
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(_waitingPipe, pipe))
                            _waitingPipe = null;
                        _connectedPipes.Add(pipe);
                    }
                    var connectedPipe = pipe;
                    pipe = null;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        HandleClient(connectedPipe);
                    });
                }
                catch (ObjectDisposedException)
                {
                    if (Interlocked.CompareExchange(
                        ref _shutdownRequested,
                        0,
                        0) != 0) return;
                }
                catch (IOException ex)
                {
                    if (Interlocked.CompareExchange(
                        ref _shutdownRequested,
                        0,
                        0) == 0)
                    {
                        _logService.Warning(LocalizationService.Format(
                            "Log.FileTransfer.PipeAcceptFailed",
                            ex.Message));
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
                    if (Interlocked.CompareExchange(
                        ref _shutdownRequested,
                        0,
                        0) == 0)
                    {
                        _logService.Error(
                            LocalizationService.Get(
                                "Log.FileTransfer.PipeServerFailed"),
                            ex);
                        Thread.Sleep(500);
                    }
                }
                finally
                {
                    if (pipe != null) pipe.Dispose();
                }
            }
        }

        private NamedPipeServerStream CreatePipeServer()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var security = new PipeSecurity();
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new PipeAccessRule(
                    identity.User,
                    PipeAccessRights.ReadWrite |
                        PipeAccessRights.CreateNewInstance,
                    AccessControlType.Allow));
                return new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    65536,
                    65536,
                    security);
            }
        }

        private void HandleClient(NamedPipeServerStream pipe)
        {
            TransferWorkItem item = null;
            try
            {
                var request = FileTransferWire.Read<
                    FileTransferRequestMessage>(pipe);
                string validationError;
                TransferSession session;
                if (!TryValidateRequest(
                    request,
                    out session,
                    out validationError))
                {
                    SendImmediateFailure(pipe, validationError, false);
                    return;
                }

                var rejectMessage = string.Empty;
                var rejectCanceled = false;
                lock (_syncRoot)
                {
                    if (Interlocked.CompareExchange(
                            ref _shutdownRequested,
                            0,
                            0) != 0)
                    {
                        rejectMessage = LocalizationService.Get(
                            "FileTransfer.ShuttingDown");
                        rejectCanceled = true;
                    }
                    else if (!session.Active)
                    {
                        rejectMessage = LocalizationService.Get(
                            "FileTransfer.SessionEnded");
                        rejectCanceled = true;
                    }
                    else if (session.CancelBurstUntilUtc > DateTime.UtcNow)
                    {
                        session.CancelBurstUntilUtc = DateTime.UtcNow
                            .AddMilliseconds(CancelBurstMilliseconds);
                        rejectMessage = LocalizationService.Get(
                            "FileTransfer.CanceledByUser");
                        rejectCanceled = true;
                    }
                    else
                    {
                        item = new TransferWorkItem(request, session, pipe);
                        _requests[request.RequestId] = item;
                        _queue.Add(item);
                        Publish(
                            item,
                            FileTransferStage.Queued,
                            -1,
                            string.Empty);
                        ArmClientDisconnectMonitor(item);
                    }
                }
                if (!string.IsNullOrEmpty(rejectMessage))
                {
                    SendImmediateFailure(
                        pipe,
                        rejectMessage,
                        rejectCanceled);
                    return;
                }
                pipe = null;
            }
            catch (InvalidOperationException)
            {
                SendImmediateFailure(
                    pipe,
                    LocalizationService.Get(
                        "FileTransfer.ShuttingDown"),
                    true);
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.RequestRejected",
                    ex.Message));
                SendImmediateFailure(pipe, ex.Message, false);
            }
            finally
            {
                if (pipe != null) DisposeClientPipe(pipe);
            }
        }

        private void DisposeClientPipe(NamedPipeServerStream pipe)
        {
            if (pipe == null) return;
            lock (_syncRoot) _connectedPipes.Remove(pipe);
            try { pipe.Dispose(); }
            catch { }
        }

        private bool TryValidateRequest(
            FileTransferRequestMessage request,
            out TransferSession session,
            out string error)
        {
            session = null;
            error = string.Empty;
            if (request == null ||
                request.Version != FileTransferEnvironment.ProtocolVersion)
            {
                error = LocalizationService.Get(
                    "FileTransfer.ProtocolMismatch");
                return false;
            }
            if (!FixedTimeEquals(request.Token, _pipeToken))
            {
                error = LocalizationService.Get(
                    "FileTransfer.AuthenticationFailed");
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.LocalPath) ||
                (!File.Exists(request.LocalPath) &&
                 !Directory.Exists(request.LocalPath)))
            {
                error = LocalizationService.Get(
                    "FileTransfer.SourceUnavailable");
                return false;
            }

            lock (_syncRoot)
            {
                if (!_sessions.TryGetValue(
                        request.SessionId ?? string.Empty,
                        out session) ||
                    !session.Active)
                {
                    error = LocalizationService.Get(
                        "FileTransfer.SessionEnded");
                    return false;
                }
                if (!string.Equals(
                    session.Serial,
                    request.Serial,
                    StringComparison.OrdinalIgnoreCase))
                {
                    error = LocalizationService.Get(
                        "FileTransfer.DeviceMismatch");
                    return false;
                }
                if (!IsManagedRemoteDirectory(
                    request.RemoteDirectory,
                    session.RemoteDirectory))
                {
                    error = LocalizationService.Get(
                        "FileTransfer.TargetRejected");
                    return false;
                }
            }
            return true;
        }

        private void ArmClientDisconnectMonitor(TransferWorkItem item)
        {
            try
            {
                item.Pipe.BeginRead(
                    item.DisconnectBuffer,
                    0,
                    1,
                    delegate(IAsyncResult result)
                    {
                        try
                        {
                            var read = item.Pipe.EndRead(result);
                            if (read == 0 && !item.IsTerminal)
                                CancelSessionRequests(
                                    item.Request.SessionId,
                                    true);
                        }
                        catch
                        {
                            if (!item.IsTerminal)
                                CancelSessionRequests(
                                    item.Request.SessionId,
                                    true);
                        }
                    },
                    null);
            }
            catch
            {
                CancelSessionRequests(
                    item.Request.SessionId,
                    true);
            }
        }

        private void WorkerLoop()
        {
            try
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    lock (_syncRoot) _activeItem = item;
                    try { ProcessItem(item); }
                    catch (Exception ex)
                    {
                        CleanupRemoteTransferArtifacts(item);
                        if (item.IsCanceled) CompleteCanceled(item);
                        else CompleteFailed(item, ex.Message);
                    }
                    finally
                    {
                        lock (_syncRoot)
                        {
                            if (ReferenceEquals(_activeItem, item))
                                _activeItem = null;
                            _activeAdbProcess = null;
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void ProcessItem(TransferWorkItem item)
        {
            if (item.IsCanceled || !item.Session.Active)
            {
                CompleteCanceled(item);
                return;
            }

            item.StartedUtc = DateTime.UtcNow;
            if (Directory.Exists(item.Request.LocalPath))
            {
                ProcessDirectory(item);
                return;
            }
            ProcessSingleFile(item);
        }

        private void ProcessSingleFile(TransferWorkItem item)
        {
            FileInfo file;
            try { file = new FileInfo(item.Request.LocalPath); }
            catch (Exception ex)
            {
                CompleteFailed(item, ex.Message);
                return;
            }

            var fileName = file.Name;
            string validationError;
            if (!TryValidatePathComponent(fileName, out validationError))
            {
                CompleteFailed(item, validationError);
                return;
            }

            item.DirectoryTransfer = false;
            item.RootName = fileName;
            item.FileName = fileName;
            item.FileSize = file.Length;
            item.TotalSize = file.Length;
            item.Entries = new List<TransferEntry>
            {
                new TransferEntry(
                    file.FullName,
                    fileName,
                    fileName,
                    file.Length,
                    false)
            };
            item.CurrentEntryIndex = 0;

            _logService.Info(LocalizationService.Format(
                "Log.FileTransfer.Starting",
                fileName,
                FormatBytes(file.Length),
                item.Session.Serial));

            string error;
            if (!TryTransferCurrentFile(item, false, out error))
            {
                CleanupRemoteTemporaryFile(item);
                if (item.IsCanceled) CompleteCanceled(item);
                else CompleteFailed(item, error);
                return;
            }

            lock (_syncRoot) item.Session.CompletedCount++;
            item.CurrentEntryIndex = item.Entries.Count;
            CompleteSuccess(item);
        }

        private void ProcessDirectory(TransferWorkItem item)
        {
            string error;
            if (!TryPrepareDirectoryTransfer(item, out error))
            {
                if (item.IsCanceled) CompleteCanceled(item);
                else CompleteFailed(item, error);
                return;
            }

            _logService.Info(LocalizationService.Format(
                "Log.FileTransfer.Starting",
                item.RootName,
                FormatBytes(item.TotalSize),
                item.Session.Serial));

            Publish(item, FileTransferStage.Queued, -1, string.Empty);
            if (!TryCreateRemoteStagingDirectory(item, out error) ||
                !TryCreateRemoteSubdirectories(item, out error))
            {
                CleanupRemoteTransferArtifacts(item);
                if (item.IsCanceled) CompleteCanceled(item);
                else CompleteFailed(item, error);
                return;
            }

            for (var index = 0; index < item.Entries.Count; index++)
            {
                var entry = item.Entries[index];
                if (entry.IsDirectory) continue;
                item.CurrentEntryIndex = index;
                item.FileName = entry.DisplayName;
                item.FileSize = entry.FileSize;
                if (!TryTransferCurrentFile(item, true, out error))
                {
                    CleanupRemoteTransferArtifacts(item);
                    if (item.IsCanceled) CompleteCanceled(item);
                    else CompleteFailed(item, error);
                    return;
                }
                item.CompletedEntryCount++;
                item.CurrentEntryIndex = index + 1;
            }

            item.FileName = item.RootName;
            item.FileSize = item.TotalSize;
            Publish(item, FileTransferStage.Finalizing, -1, string.Empty);
            string finalDirectoryName;
            if (!TryFinalizeRemoteDirectory(
                item,
                out finalDirectoryName,
                out error))
            {
                CleanupRemoteTransferArtifacts(item);
                if (item.IsCanceled) CompleteCanceled(item);
                else CompleteFailed(item, error);
                return;
            }

            item.RemoteStagingDirectory = string.Empty;
            item.FinalFileName = finalDirectoryName;
            var committedCount = Math.Max(1, item.CompletedEntryCount);
            lock (_syncRoot)
            {
                item.Session.CompletedCount += committedCount;
            }
            item.CompletedEntryCount = 0;
            item.CurrentEntryIndex = item.Entries.Count;
            CompleteSuccess(item);
        }

        private bool TryTransferCurrentFile(
            TransferWorkItem item,
            bool intoStagingDirectory,
            out string error)
        {
            error = string.Empty;
            var entry = item.CurrentEntry;
            if (entry == null || entry.IsDirectory ||
                !File.Exists(entry.LocalPath))
            {
                error = LocalizationService.Get(
                    "FileTransfer.SourceUnavailable");
                return false;
            }

            item.RemoteTemporaryPath = "/sdcard/.dxm-file-" +
                Guid.NewGuid().ToString("N") + ".part";
            item.RenameCompleted = false;
            Publish(item, FileTransferStage.Transferring, -1, string.Empty);

            var pushResult = RunAdbPush(item);
            if (item.IsCanceled)
            {
                error = LocalizationService.Get(
                    "FileTransfer.CanceledByUser");
                return false;
            }
            if (pushResult.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(pushResult.ErrorTail)
                    ? LocalizationService.Get("FileTransfer.PushFailed")
                    : pushResult.ErrorTail;
                return false;
            }

            Publish(item, FileTransferStage.Finalizing, -1, string.Empty);
            if (intoStagingDirectory)
            {
                if (!TryMoveTemporaryFileIntoStaging(item, entry, out error))
                    return false;
                item.RemoteTemporaryPath = string.Empty;
                item.RenameCompleted = true;
                return true;
            }

            string finalFileName;
            if (!TryFinalizeRemoteFile(item, out finalFileName, out error))
                return false;
            item.RemoteTemporaryPath = string.Empty;
            item.RenameCompleted = true;
            item.FinalFileName = finalFileName;
            return true;
        }

        private bool TryPrepareDirectoryTransfer(
            TransferWorkItem item,
            out string error)
        {
            error = string.Empty;
            try
            {
                var root = new DirectoryInfo(item.Request.LocalPath);
                if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    error = LocalizationService.Get(
                        "FileTransfer.ReparsePointUnsupported");
                    return false;
                }

                string validationError;
                if (!TryValidatePathComponent(root.Name, out validationError))
                {
                    error = validationError;
                    return false;
                }

                var entries = new List<TransferEntry>();
                var stack = new Stack<DirectoryScanItem>();
                stack.Push(new DirectoryScanItem(root, string.Empty));
                long totalSize = 0;
                while (stack.Count > 0)
                {
                    if (item.IsCanceled ||
                        Interlocked.CompareExchange(
                            ref _shutdownRequested,
                            0,
                            0) != 0)
                    {
                        error = LocalizationService.Get(
                            "FileTransfer.CanceledByUser");
                        return false;
                    }
                    var current = stack.Pop();
                    var children = current.Directory.GetFileSystemInfos();
                    Array.Sort(children, delegate(
                        FileSystemInfo left,
                        FileSystemInfo right)
                    {
                        return StringComparer.OrdinalIgnoreCase.Compare(
                            left.Name,
                            right.Name);
                    });

                    for (var index = children.Length - 1;
                        index >= 0;
                        index--)
                    {
                        if ((index & 63) == 0 &&
                            (item.IsCanceled ||
                             Interlocked.CompareExchange(
                                 ref _shutdownRequested,
                                 0,
                                 0) != 0))
                        {
                            error = LocalizationService.Get(
                                "FileTransfer.CanceledByUser");
                            return false;
                        }
                        var child = children[index];
                        if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            _logService.Warning(LocalizationService.Format(
                                "Log.FileTransfer.ReparseSkipped",
                                child.FullName));
                            continue;
                        }
                        if (!TryValidatePathComponent(
                            child.Name,
                            out validationError))
                        {
                            error = validationError;
                            return false;
                        }

                        var relativePath = string.IsNullOrEmpty(
                            current.RelativePath)
                            ? child.Name
                            : current.RelativePath + "/" + child.Name;
                        var directory = child as DirectoryInfo;
                        if (directory != null)
                        {
                            entries.Add(new TransferEntry(
                                directory.FullName,
                                relativePath,
                                root.Name + "/" + relativePath,
                                0L,
                                true));
                            stack.Push(new DirectoryScanItem(
                                directory,
                                relativePath));
                            continue;
                        }

                        var file = child as FileInfo;
                        if (file == null) continue;
                        var fileSize = file.Length;
                        totalSize = totalSize > long.MaxValue - fileSize
                            ? long.MaxValue
                            : totalSize + fileSize;
                        entries.Add(new TransferEntry(
                            file.FullName,
                            relativePath,
                            root.Name + "/" + relativePath,
                            fileSize,
                            false));
                    }
                }

                entries.Sort(delegate(TransferEntry left, TransferEntry right)
                {
                    if (left.IsDirectory != right.IsDirectory)
                        return left.IsDirectory ? -1 : 1;
                    return StringComparer.OrdinalIgnoreCase.Compare(
                        left.RelativePath,
                        right.RelativePath);
                });
                item.DirectoryTransfer = true;
                item.RootName = root.Name;
                item.FileName = root.Name;
                item.FileSize = totalSize;
                item.TotalSize = totalSize;
                item.Entries = entries;
                item.CurrentEntryIndex = FindNextFileIndex(entries, 0);
                item.RemoteStagingDirectory =
                    item.Session.RemoteDirectory + "/.dxm-dir-" +
                    Guid.NewGuid().ToString("N") + ".part";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static int FindNextFileIndex(
            IList<TransferEntry> entries,
            int startIndex)
        {
            for (var index = Math.Max(startIndex, 0);
                index < entries.Count;
                index++)
            {
                if (!entries[index].IsDirectory) return index;
            }
            return entries.Count;
        }

        private static bool TryValidatePathComponent(
            string value,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, ".", StringComparison.Ordinal) ||
                string.Equals(value, "..", StringComparison.Ordinal))
            {
                error = LocalizationService.Get(
                    "FileTransfer.InvalidFileName");
                return false;
            }
            if (Encoding.UTF8.GetByteCount(value) > MaximumFileNameBytes)
            {
                error = LocalizationService.Format(
                    "FileTransfer.FileNameTooLong",
                    MaximumFileNameBytes);
                return false;
            }
            return true;
        }

        private bool TryCreateRemoteStagingDirectory(
            TransferWorkItem item,
            out string error)
        {
            var script = new StringBuilder();
            script.AppendLine("set -e");
            AppendDecodedVariable(
                script,
                "base",
                item.Session.RemoteDirectory);
            AppendDecodedVariable(
                script,
                "staging",
                item.RemoteStagingDirectory);
            script.AppendLine("mkdir -p \"$base\"");
            script.AppendLine("[ -d \"$base\" ]");
            script.AppendLine("[ ! -e \"$staging\" ]");
            script.AppendLine("mkdir \"$staging\"");
            return TryRunPreparationScript(item, script.ToString(), out error);
        }

        private bool TryCreateRemoteSubdirectories(
            TransferWorkItem item,
            out string error)
        {
            var directories = item.Entries
                .Where(entry => entry.IsDirectory)
                .ToArray();
            if (directories.Length == 0)
            {
                error = string.Empty;
                return true;
            }

            var script = new StringBuilder();
            script.AppendLine("set -e");
            AppendDecodedVariable(
                script,
                "root",
                item.RemoteStagingDirectory);
            foreach (var directory in directories)
            {
                AppendDecodedVariable(
                    script,
                    "rel",
                    directory.RelativePath);
                script.AppendLine("mkdir -p \"$root/$rel\"");
            }
            var timeout = Math.Min(
                60000,
                Math.Max(ShortAdbTimeoutMs, directories.Length * 50));
            var result = RunShellScript(item, script.ToString(), timeout);
            if (item.IsCanceled)
            {
                error = LocalizationService.Get(
                    "FileTransfer.CanceledByUser");
                return false;
            }
            if (result.ExitCode == 0)
            {
                error = string.Empty;
                return true;
            }
            error = string.IsNullOrWhiteSpace(result.ErrorTail)
                ? LocalizationService.Get("FileTransfer.FolderCreateFailed")
                : result.ErrorTail;
            return false;
        }

        private bool TryMoveTemporaryFileIntoStaging(
            TransferWorkItem item,
            TransferEntry entry,
            out string error)
        {
            var script = new StringBuilder();
            script.AppendLine("set -e");
            AppendDecodedVariable(
                script,
                "root",
                item.RemoteStagingDirectory);
            AppendDecodedVariable(script, "rel", entry.RelativePath);
            AppendDecodedVariable(
                script,
                "tmp",
                item.RemoteTemporaryPath);
            script.AppendLine("dest=\"$root/$rel\"");
            script.AppendLine("parent=${dest%/*}");
            script.AppendLine("mkdir -p \"$parent\"");
            script.AppendLine("[ ! -e \"$dest\" ]");
            script.AppendLine("mv \"$tmp\" \"$dest\"");
            script.AppendLine("[ ! -e \"$tmp\" ]");
            var result = RunShellScript(
                item,
                script.ToString(),
                ShortAdbTimeoutMs);
            if (item.IsCanceled)
            {
                error = LocalizationService.Get(
                    "FileTransfer.CanceledByUser");
                return false;
            }
            if (result.ExitCode == 0)
            {
                error = string.Empty;
                return true;
            }
            error = string.IsNullOrWhiteSpace(result.ErrorTail)
                ? LocalizationService.Get("FileTransfer.RenameFailed")
                : result.ErrorTail;
            return false;
        }

        private bool TryRunPreparationScript(
            TransferWorkItem item,
            string script,
            out string error)
        {
            var result = RunShellScript(item, script, ShortAdbTimeoutMs);
            if (item.IsCanceled)
            {
                error = LocalizationService.Get(
                    "FileTransfer.CanceledByUser");
                return false;
            }
            if (result.ExitCode == 0)
            {
                error = string.Empty;
                return true;
            }
            error = string.IsNullOrWhiteSpace(result.ErrorTail)
                ? LocalizationService.Get("FileTransfer.FolderCreateFailed")
                : result.ErrorTail;
            return false;
        }

        private static void AppendDecodedVariable(
            StringBuilder builder,
            string variable,
            string value)
        {
            builder.Append(variable)
                .Append("=\"$(printf '%s' '")
                .Append(ToBase64(value))
                .AppendLine("' | base64 -d)\"");
        }

        private AdbExecutionResult RunAdbPush(TransferWorkItem item)
        {
            var arguments = WindowsCommandLine.Build(new[]
            {
                "-s",
                item.Session.Serial,
                "push",
                item.CurrentEntry.LocalPath,
                item.RemoteTemporaryPath
            });
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _realAdbPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(_realAdbPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            })
            {
                var output = new BoundedTextBuffer();
                var error = new BoundedTextBuffer();
                process.Start();
                SetActiveProcess(item, process);
                var stdoutThread = StartReaderThread(
                    process.StandardOutput,
                    output,
                    null);
                var stderrThread = StartReaderThread(
                    process.StandardError,
                    error,
                    null);

                while (!process.WaitForExit(ProcessPollMilliseconds))
                {
                    if (item.IsCanceled ||
                        Interlocked.CompareExchange(
                            ref _shutdownRequested,
                            0,
                            0) != 0)
                    {
                        TryKill(process);
                    }
                }
                stdoutThread.Join(2000);
                stderrThread.Join(2000);
                ClearActiveProcess(process);
                return new AdbExecutionResult(
                    process.ExitCode,
                    output.Value,
                    error.Value);
            }
        }

        private Thread StartReaderThread(
            StreamReader reader,
            BoundedTextBuffer buffer,
            Action<int> progress)
        {
            var thread = new Thread(new ThreadStart(delegate
            {
                var chunk = new char[512];
                var progressTail = string.Empty;
                try
                {
                    int read;
                    while ((read = reader.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        var text = new string(chunk, 0, read);
                        buffer.Append(text);
                        if (progress != null)
                        {
                            var progressText = progressTail + text;
                            var consumed = 0;
                            foreach (Match match in Regex.Matches(
                                progressText,
                                @"(?<!\d)(\d{1,3})%"))
                            {
                                int value;
                                if (int.TryParse(
                                    match.Groups[1].Value,
                                    NumberStyles.Integer,
                                    CultureInfo.InvariantCulture,
                                    out value) && value >= 0 && value <= 100)
                                {
                                    progress(value);
                                }
                                consumed = match.Index + match.Length;
                            }
                            progressTail = consumed > 0
                                ? progressText.Substring(consumed)
                                : progressText;
                            if (progressTail.Length > 16)
                                progressTail = progressTail.Substring(
                                    progressTail.Length - 16);
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }))
            {
                IsBackground = true,
                Name = "DX Manager ADB output reader"
            };
            thread.Start();
            return thread;
        }

        private bool TryFinalizeRemoteFile(
            TransferWorkItem item,
            out string finalFileName,
            out string error)
        {
            finalFileName = string.Empty;
            error = string.Empty;
            var name = item.FileName;
            var extension = Path.GetExtension(name) ?? string.Empty;
            var stem = extension.Length == 0
                ? name
                : name.Substring(0, name.Length - extension.Length);
            string collisionStem;
            string collisionExtension;
            PrepareCollisionNameParts(
                name,
                stem,
                extension,
                out collisionStem,
                out collisionExtension);
            if (!TryBeginFinalCommit(item, out error)) return false;
            item.RemoteCommitMarkerPath = "/data/local/tmp/.dxm-commit-" +
                Guid.NewGuid().ToString("N") + ".result";
            item.FinalCommitRecoveryPending = true;
            var script = BuildFinalizeScript(
                item.Session.RemoteDirectory,
                item.RemoteTemporaryPath,
                item.RemoteCommitMarkerPath,
                name,
                collisionStem,
                collisionExtension);
            try
            {
                var result = RunShellScript(
                    item,
                    script,
                    ShortAdbTimeoutMs,
                    true);
                result = RecoverFinalCommitResult(item, result);
                item.FinalCommitRecoveryPending = !HasFinalCommitResult(result);
                return TryReadCollisionResult(
                    item,
                    result,
                    name,
                    collisionStem,
                    collisionExtension,
                    "FileTransfer.RenameFailed",
                    out finalFileName,
                    out error);
            }
            finally
            {
                EndFinalCommit(item);
                if (!item.FinalCommitRecoveryPending)
                    CleanupRemoteCommitMarker(item);
            }
        }

        private bool TryFinalizeRemoteDirectory(
            TransferWorkItem item,
            out string finalDirectoryName,
            out string error)
        {
            var name = item.RootName;
            string collisionStem;
            string collisionExtension;
            PrepareCollisionNameParts(
                name,
                name,
                string.Empty,
                out collisionStem,
                out collisionExtension);
            if (!TryBeginFinalCommit(item, out error))
            {
                finalDirectoryName = string.Empty;
                return false;
            }
            item.RemoteCommitMarkerPath = "/data/local/tmp/.dxm-commit-" +
                Guid.NewGuid().ToString("N") + ".result";
            item.FinalCommitRecoveryPending = true;
            var script = BuildFinalizeScript(
                item.Session.RemoteDirectory,
                item.RemoteStagingDirectory,
                item.RemoteCommitMarkerPath,
                name,
                collisionStem,
                collisionExtension);
            try
            {
                var result = RunShellScript(
                    item,
                    script,
                    ShortAdbTimeoutMs,
                    true);
                result = RecoverFinalCommitResult(item, result);
                item.FinalCommitRecoveryPending = !HasFinalCommitResult(result);
                return TryReadCollisionResult(
                    item,
                    result,
                    name,
                    collisionStem,
                    collisionExtension,
                    "FileTransfer.FolderFinalizeFailed",
                    out finalDirectoryName,
                    out error);
            }
            finally
            {
                EndFinalCommit(item);
                if (!item.FinalCommitRecoveryPending)
                    CleanupRemoteCommitMarker(item);
            }
        }

        private bool TryBeginFinalCommit(
            TransferWorkItem item,
            out string error)
        {
            lock (_syncRoot)
            {
                if (item.IsCanceled ||
                    !item.Session.Active ||
                    Interlocked.CompareExchange(
                        ref _shutdownRequested,
                        0,
                        0) != 0)
                {
                    error = LocalizationService.Get(
                        "FileTransfer.CanceledByUser");
                    return false;
                }
                item.BeginCommit();
            }
            error = string.Empty;
            return true;
        }

        private void EndFinalCommit(TransferWorkItem item)
        {
            lock (_syncRoot) item.EndCommit();
        }

        private AdbExecutionResult RecoverFinalCommitResult(
            TransferWorkItem item,
            AdbExecutionResult originalResult)
        {
            if (Regex.IsMatch(
                    originalResult.OutputTail ?? string.Empty,
                    @"DXM_INDEX=(\d+)",
                    RegexOptions.CultureInvariant) ||
                string.IsNullOrWhiteSpace(item.RemoteCommitMarkerPath))
            {
                return originalResult;
            }

            try
            {
                var temporaryPath = item.DirectoryTransfer
                    ? item.RemoteStagingDirectory
                    : item.RemoteTemporaryPath;
                if (string.IsNullOrWhiteSpace(temporaryPath))
                    return originalResult;

                var script = new StringBuilder();
                script.AppendLine("set -e");
                AppendDecodedVariable(
                    script,
                    "marker",
                    item.RemoteCommitMarkerPath);
                AppendDecodedVariable(script, "tmp", temporaryPath);
                script.AppendLine("attempt=0");
                script.Append("while [ \"$attempt\" -lt ")
                    .Append(FinalCommitRecoveryAttempts.ToString(
                        CultureInfo.InvariantCulture))
                    .AppendLine(" ]; do");
                script.AppendLine("  if [ -f \"$marker\" ]; then");
                script.AppendLine("    record=\"$(cat \"$marker\")\"");
                script.AppendLine("    index=\"\"");
                script.AppendLine("    case \"$record\" in");
                script.AppendLine("      C:*) index=\"${record#C:}\" ;;");
                script.AppendLine("      P:*)");
                script.AppendLine("        if [ ! -e \"$tmp\" ]; then");
                script.AppendLine("          index=\"${record#P:}\"");
                script.AppendLine("        fi");
                script.AppendLine("        ;;");
                script.AppendLine("    esac");
                script.AppendLine("    case \"$index\" in");
                script.AppendLine("      ''|*[!0-9]*) ;;");
                script.AppendLine("      *) printf 'DXM_INDEX=%s\\n' \"$index\"; exit 0 ;;");
                script.AppendLine("    esac");
                script.AppendLine("  fi");
                script.AppendLine("  attempt=$((attempt + 1))");
                script.Append("  [ \"$attempt\" -ge ")
                    .Append(FinalCommitRecoveryAttempts.ToString(
                        CultureInfo.InvariantCulture))
                    .AppendLine(" ] || sleep 1");
                script.AppendLine("done");
                script.AppendLine("exit 72");
                var recovered = RunCleanupScript(
                    item.Session.Serial,
                    script.ToString(),
                    (FinalCommitRecoveryAttempts * 1000) + 2000);
                if (!Regex.IsMatch(
                    recovered.OutputTail ?? string.Empty,
                    @"DXM_INDEX=(\d+)",
                    RegexOptions.CultureInvariant))
                {
                    return originalResult;
                }

                _logService.Info(LocalizationService.Format(
                    "Log.FileTransfer.CommitRecovered",
                    item.FileName));
                return recovered;
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.CommitRecoveryFailed",
                    ex.Message));
                return originalResult;
            }
        }

        private static bool HasFinalCommitResult(AdbExecutionResult result)
        {
            return result != null && Regex.IsMatch(
                result.OutputTail ?? string.Empty,
                @"DXM_INDEX=(\d+)",
                RegexOptions.CultureInvariant);
        }

        private static bool TryReadCollisionResult(
            TransferWorkItem item,
            AdbExecutionResult result,
            string name,
            string collisionStem,
            string collisionExtension,
            string failureResource,
            out string finalName,
            out string error)
        {
            finalName = string.Empty;
            error = string.Empty;
            var match = Regex.Match(
                result.OutputTail ?? string.Empty,
                @"DXM_INDEX=(\d+)",
                RegexOptions.CultureInvariant);
            if (match.Success)
            {
                int collisionIndex;
                if (!int.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out collisionIndex) ||
                    collisionIndex < 0 ||
                    collisionIndex > MaximumCollisionIndex)
                {
                    error = LocalizationService.Get(
                        "FileTransfer.RenameResultInvalid");
                    return false;
                }
                finalName = collisionIndex == 0
                    ? name
                    : collisionStem + " (" + collisionIndex.ToString(
                        CultureInfo.InvariantCulture) + ")" +
                        collisionExtension;
                if (Encoding.UTF8.GetByteCount(finalName) >
                    MaximumFileNameBytes)
                {
                    error = LocalizationService.Format(
                        "FileTransfer.FileNameTooLong",
                        MaximumFileNameBytes);
                    return false;
                }
                return true;
            }

            if (item.IsCanceled)
            {
                error = LocalizationService.Get(
                    "FileTransfer.CanceledByUser");
                return false;
            }
            if (result.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(result.ErrorTail)
                    ? LocalizationService.Get(failureResource)
                    : result.ErrorTail;
                return false;
            }

            error = LocalizationService.Get(
                "FileTransfer.RenameResultMissing");
            return false;
        }

        private static void PrepareCollisionNameParts(
            string name,
            string stem,
            string extension,
            out string collisionStem,
            out string collisionExtension)
        {
            var suffixBytes = Encoding.UTF8.GetByteCount(
                " (" + MaximumCollisionIndex.ToString(
                    CultureInfo.InvariantCulture) + ")");
            var stemBudget = MaximumFileNameBytes - suffixBytes -
                Encoding.UTF8.GetByteCount(extension ?? string.Empty);
            if (stemBudget > 0)
            {
                collisionStem = TruncateUtf8(stem, stemBudget);
                if (!string.IsNullOrEmpty(collisionStem))
                {
                    collisionExtension = extension ?? string.Empty;
                    return;
                }
            }

            collisionExtension = string.Empty;
            collisionStem = TruncateUtf8(
                name,
                MaximumFileNameBytes - suffixBytes);
        }

        private static string TruncateUtf8(string value, int maximumBytes)
        {
            if (string.IsNullOrEmpty(value) || maximumBytes <= 0)
                return string.Empty;
            if (Encoding.UTF8.GetByteCount(value) <= maximumBytes)
                return value;

            var builder = new StringBuilder();
            var usedBytes = 0;
            var elements = StringInfo.GetTextElementEnumerator(value);
            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                var elementBytes = Encoding.UTF8.GetByteCount(element);
                if (usedBytes + elementBytes > maximumBytes) break;
                builder.Append(element);
                usedBytes += elementBytes;
            }
            return builder.ToString();
        }

        private static string BuildFinalizeScript(
            string directory,
            string temporaryPath,
            string markerPath,
            string name,
            string stem,
            string extension)
        {
            var builder = new StringBuilder();
            builder.AppendLine("set -e");
            AppendDecodedVariable(builder, "dir", directory);
            AppendDecodedVariable(builder, "tmp", temporaryPath);
            AppendDecodedVariable(builder, "marker", markerPath);
            AppendDecodedVariable(builder, "name", name);
            AppendDecodedVariable(builder, "stem", stem);
            AppendDecodedVariable(builder, "ext", extension);
            builder.AppendLine("mkdir -p \"$dir\"");
            builder.AppendLine("[ -d \"$dir\" ] || exit 51");
            builder.AppendLine("rm -f \"$marker\"");
            builder.AppendLine("candidate=\"$name\"");
            builder.AppendLine("index=0");
            builder.AppendLine("while :; do");
            builder.AppendLine("  if [ ! -e \"$dir/$candidate\" ]; then");
            builder.AppendLine("    printf 'P:%s\\n' \"$index\" > \"$marker\"");
            builder.AppendLine("    mv -n \"$tmp\" \"$dir/$candidate\"");
            builder.AppendLine("    if [ ! -e \"$tmp\" ]; then");
            builder.AppendLine("      printf 'C:%s\\n' \"$index\" > \"$marker\"");
            builder.AppendLine("      printf 'DXM_INDEX=%s\\n' \"$index\"");
            builder.AppendLine("      exit 0");
            builder.AppendLine("    fi");
            builder.AppendLine("  fi");
            builder.AppendLine("  index=$((index + 1))");
            builder.Append("  [ \"$index\" -le ")
                .Append(MaximumCollisionIndex.ToString(
                    CultureInfo.InvariantCulture))
                .AppendLine(" ] || exit 52");
            builder.AppendLine("  candidate=\"$stem ($index)$ext\"");
            builder.AppendLine("done");
            return builder.ToString();
        }

        private AdbExecutionResult RunShellScript(
            TransferWorkItem item,
            string script,
            int timeoutMs)
        {
            return RunShellScript(item, script, timeoutMs, false);
        }

        private AdbExecutionResult RunShellScript(
            TransferWorkItem item,
            string script,
            int timeoutMs,
            bool ignoreCancellation)
        {
            var arguments = WindowsCommandLine.Build(new[]
            {
                "-s", item.Session.Serial, "shell", "sh"
            });
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _realAdbPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(_realAdbPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            })
            {
                process.Start();
                SetActiveProcess(item, process);
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var stopwatch = Stopwatch.StartNew();
                var inputThread = StartStandardInputWriter(process, script);
                while (!process.WaitForExit(ProcessPollMilliseconds))
                {
                    var shutdownRequested = Interlocked.CompareExchange(
                        ref _shutdownRequested,
                        0,
                        0) != 0;
                    if ((!ignoreCancellation &&
                         (item.IsCanceled || shutdownRequested)) ||
                        stopwatch.ElapsedMilliseconds >= timeoutMs)
                    {
                        TryKill(process);
                    }
                }
                inputThread.Join(1000);
                ClearActiveProcess(process);
                return new AdbExecutionResult(
                    process.ExitCode,
                    GetTaskResult(output),
                    GetTaskResult(error));
            }
        }

        private static Thread StartStandardInputWriter(
            Process process,
            string script)
        {
            var thread = new Thread(delegate()
            {
                try
                {
                    var bytes = Encoding.ASCII.GetBytes(
                        (script ?? string.Empty) + "\n");
                    process.StandardInput.BaseStream.Write(
                        bytes,
                        0,
                        bytes.Length);
                    process.StandardInput.BaseStream.Flush();
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    try { process.StandardInput.Close(); }
                    catch { }
                }
            })
            {
                IsBackground = true,
                Name = "DX Manager ADB input writer"
            };
            thread.Start();
            return thread;
        }

        private void CleanupRemoteCommitMarker(TransferWorkItem item)
        {
            if (item.FinalCommitRecoveryPending) return;
            if (string.IsNullOrWhiteSpace(item.RemoteCommitMarkerPath)) return;
            var markerPath = item.RemoteCommitMarkerPath;
            var cleaned = false;
            try
            {
                var script = new StringBuilder();
                AppendDecodedVariable(script, "path", markerPath);
                script.AppendLine("case \"$path\" in");
                script.AppendLine(
                    "  /data/local/tmp/.dxm-commit-*.result) rm -f \"$path\" ;;");
                script.AppendLine("  *) exit 61 ;;");
                script.AppendLine("esac");
                var result = RunCleanupScript(
                    item.Session.Serial,
                    script.ToString());
                if (result.ExitCode != 0)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TempCleanupFailed",
                        GetCleanupFailure(markerPath, result)));
                }
                else
                {
                    cleaned = true;
                }
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    ex.Message));
            }
            finally
            {
                if (cleaned) item.RemoteCommitMarkerPath = string.Empty;
            }
        }

        private void CleanupRemoteTemporaryFile(TransferWorkItem item)
        {
            if (item.FinalCommitRecoveryPending) return;
            if (string.IsNullOrWhiteSpace(item.RemoteTemporaryPath)) return;
            try
            {
                var script = new StringBuilder();
                AppendDecodedVariable(
                    script,
                    "path",
                    item.RemoteTemporaryPath);
                script.AppendLine("rm -f \"$path\"");
                var result = RunCleanupScript(item.Session.Serial, script);
                if (result.ExitCode == 0)
                {
                    item.RemoteTemporaryPath = string.Empty;
                }
                else
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TempCleanupFailed",
                        GetCleanupFailure(item.RemoteTemporaryPath, result)));
                }
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    ex.Message));
            }
        }

        private void CleanupRemoteTransferArtifacts(TransferWorkItem item)
        {
            if (item.FinalCommitRecoveryPending) return;
            CleanupRemoteCommitMarker(item);
            CleanupRemoteTemporaryFile(item);
            if (string.IsNullOrWhiteSpace(item.RemoteStagingDirectory)) return;
            try
            {
                var script = new StringBuilder();
                AppendDecodedVariable(
                    script,
                    "path",
                    item.RemoteStagingDirectory);
                script.AppendLine("case \"$path\" in");
                script.AppendLine("  */.dxm-dir-*.part) rm -rf \"$path\" ;;");
                script.AppendLine("  *) exit 61 ;;");
                script.AppendLine("esac");
                var result = RunCleanupScript(
                    item.Session.Serial,
                    script.ToString());
                if (result.ExitCode == 0)
                {
                    item.RemoteStagingDirectory = string.Empty;
                }
                else
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TempCleanupFailed",
                        GetCleanupFailure(
                            item.RemoteStagingDirectory,
                            result)));
                }
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    ex.Message));
            }
        }

        private static string GetCleanupFailure(
            string path,
            AdbExecutionResult result)
        {
            var detail = string.IsNullOrWhiteSpace(result.ErrorTail)
                ? result.OutputTail
                : result.ErrorTail;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = "ADB exit code " + result.ExitCode.ToString(
                    CultureInfo.InvariantCulture);
            }
            return (path ?? string.Empty) + ": " + detail;
        }

        private AdbExecutionResult RunCleanupScript(
            string serial,
            StringBuilder script)
        {
            return RunCleanupScript(serial, script == null
                ? string.Empty
                : script.ToString());
        }

        private AdbExecutionResult RunCleanupScript(
            string serial,
            string script)
        {
            return RunCleanupScript(serial, script, ShortAdbTimeoutMs);
        }

        private AdbExecutionResult RunCleanupScript(
            string serial,
            string script,
            int timeoutMs)
        {
            var arguments = WindowsCommandLine.Build(new[]
            {
                "-s", serial, "shell", "sh"
            });
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _realAdbPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(_realAdbPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var inputThread = StartStandardInputWriter(process, script);
                var timedOut = !process.WaitForExit(timeoutMs);
                if (timedOut)
                {
                    TryKill(process);
                    process.WaitForExit(1000);
                }
                inputThread.Join(1000);
                if (!process.HasExited)
                {
                    return new AdbExecutionResult(
                        -1,
                        string.Empty,
                        string.Empty);
                }
                return new AdbExecutionResult(
                    timedOut ? -1 : process.ExitCode,
                    GetTaskResult(output),
                    GetTaskResult(error));
            }
        }

        private void CompleteSuccess(TransferWorkItem item)
        {
            MarkTerminalAndUnregister(item);
            Publish(item, FileTransferStage.Completed, -1, string.Empty);
            SendResponse(item, new FileTransferResponseMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Success = true,
                ExitCode = 0,
                FinalFileName = item.FinalFileName,
                Message = string.Empty
            });
            _logService.Info(LocalizationService.Format(
                "Log.FileTransfer.Completed",
                item.FileName,
                item.FinalFileName));
        }

        private void CompleteFailed(TransferWorkItem item, string message)
        {
            if (item == null || item.IsTerminal) return;
            item.CompletedEntryCount = 0;
            item.CurrentEntryIndex = item.Entries.Count;
            lock (_syncRoot) item.Session.FailedCount++;
            MarkTerminalAndUnregister(item);
            Publish(item, FileTransferStage.Failed, -1, message);
            SendResponse(item, new FileTransferResponseMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Success = false,
                ExitCode = 1,
                Message = message ?? string.Empty
            });
            _logService.Warning(LocalizationService.Format(
                "Log.FileTransfer.Failed",
                item.FileName,
                message));
        }

        private void CompleteCanceled(TransferWorkItem item)
        {
            if (item == null || item.IsTerminal) return;
            item.CompletedEntryCount = 0;
            item.CurrentEntryIndex = item.Entries.Count;
            MarkTerminalAndUnregister(item);
            var message = LocalizationService.Get(
                "FileTransfer.CanceledByUser");
            Publish(item, FileTransferStage.Canceled, -1, message);
            SendResponse(item, new FileTransferResponseMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Success = false,
                Canceled = true,
                ExitCode = 1,
                Message = message
            });
            _logService.Info(LocalizationService.Format(
                "Log.FileTransfer.Canceled",
                item.FileName));
        }

        private void MarkTerminalAndUnregister(TransferWorkItem item)
        {
            lock (_syncRoot)
            {
                item.MarkTerminal();
                _requests.Remove(item.Request.RequestId);
            }
        }

        private void SendResponse(
            TransferWorkItem item,
            FileTransferResponseMessage response)
        {
            try { FileTransferWire.Write(item.Pipe, response); }
            catch { }
            finally
            {
                DisposeClientPipe(item.Pipe);
            }
        }

        private static void SendImmediateFailure(
            NamedPipeServerStream pipe,
            string message,
            bool canceled)
        {
            if (pipe == null) return;
            try
            {
                FileTransferWire.Write(pipe, new FileTransferResponseMessage
                {
                    Version = FileTransferEnvironment.ProtocolVersion,
                    Success = false,
                    Canceled = canceled,
                    ExitCode = 1,
                    Message = message ?? string.Empty
                });
            }
            catch { }
        }

        private void Publish(
            TransferWorkItem item,
            FileTransferStage stage,
            int percent,
            string message)
        {
            TransferWorkItem primary;
            int completed;
            int failed;
            int queued;
            List<FileTransferQueueEntry> visibleQueue;
            long sequence;
            lock (_syncRoot)
            {
                sequence = ++_progressSequence;
                item.CurrentStage = stage;
                item.CurrentPercent = percent;
                item.CurrentMessage = message ?? string.Empty;
                primary = _activeItem != null &&
                    !_activeItem.IsTerminal
                    ? _activeItem
                    : item;
                completed = primary.Session.CompletedCount +
                    primary.CompletedEntryCount;
                failed = primary.Session.FailedCount;
                visibleQueue = BuildVisibleQueue(primary);
                queued = CountQueuedItems(primary);
            }
            var progress = new FileTransferProgress(
                sequence,
                primary.Request.RequestId,
                primary.Request.SessionId,
                primary.CurrentStage,
                GetDisplayName(primary),
                primary.FinalFileName,
                primary.FileSize,
                primary.CurrentPercent,
                completed,
                failed,
                queued,
                visibleQueue,
                primary.StartedUtc,
                primary.DirectoryTransfer,
                Math.Max(
                    1,
                    primary.Entries.Count(entry => !entry.IsDirectory)),
                primary.CurrentMessage);
            var handler = ProgressChanged;
            if (handler == null) return;
            try
            {
                handler(this, new FileTransferProgressEventArgs(progress));
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.ProgressSubscriberFailed",
                    ex.Message));
            }
        }

        private List<FileTransferQueueEntry> BuildVisibleQueue(
            TransferWorkItem primary)
        {
            var result = new List<FileTransferQueueEntry>(
                MaximumVisibleQueueItems);
            if (primary.Entries != null && primary.Entries.Count > 0 &&
                primary.CurrentEntryIndex < primary.Entries.Count)
            {
                for (var index = Math.Max(primary.CurrentEntryIndex, 0);
                    index < primary.Entries.Count &&
                    result.Count < MaximumVisibleQueueItems;
                    index++)
                {
                    var entry = primary.Entries[index];
                    if (entry.IsDirectory) continue;
                    result.Add(new FileTransferQueueEntry(
                        entry.DisplayName,
                        entry.FileSize,
                        result.Count == 0));
                }
            }
            if (result.Count == 0)
            {
                result.Add(new FileTransferQueueEntry(
                    GetDisplayName(primary),
                    primary.FileSize,
                    true));
            }

            foreach (var queuedItem in _queue.ToArray())
            {
                if (result.Count >= MaximumVisibleQueueItems) break;
                if (ReferenceEquals(queuedItem, primary) ||
                    queuedItem.IsCanceled ||
                    queuedItem.IsTerminal)
                {
                    continue;
                }
                result.Add(new FileTransferQueueEntry(
                    GetSourceDisplayName(queuedItem.Request.LocalPath),
                    TryGetSourceSize(queuedItem.Request.LocalPath),
                    false));
            }
            return result;
        }

        private int CountQueuedItems(TransferWorkItem primary)
        {
            var count = 0;
            if (!primary.IsTerminal && primary.Entries != null)
            {
                var skippedActive = false;
                for (var index = Math.Max(primary.CurrentEntryIndex, 0);
                    index < primary.Entries.Count;
                    index++)
                {
                    if (primary.Entries[index].IsDirectory) continue;
                    if (!skippedActive)
                    {
                        skippedActive = true;
                        continue;
                    }
                    count++;
                }
            }
            foreach (var queuedItem in _queue.ToArray())
            {
                if (ReferenceEquals(queuedItem, primary) ||
                    queuedItem.IsCanceled ||
                    queuedItem.IsTerminal)
                {
                    continue;
                }
                count++;
            }
            return count;
        }

        private static string GetDisplayName(TransferWorkItem item)
        {
            return string.IsNullOrWhiteSpace(item.FileName)
                ? GetSourceDisplayName(item.Request.LocalPath)
                : item.FileName;
        }

        private static string GetSourceDisplayName(string path)
        {
            var trimmed = (path ?? string.Empty).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? trimmed : name;
        }

        private static long TryGetSourceSize(string path)
        {
            try
            {
                return File.Exists(path)
                    ? new FileInfo(path).Length
                    : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        private void CancelSessionRequests(
            string sessionId,
            bool setBurst)
        {
            TransferWorkItem[] requests;
            lock (_syncRoot)
            {
                if (setBurst)
                {
                    TransferSession session;
                    if (_sessions.TryGetValue(
                        sessionId ?? string.Empty,
                        out session))
                    {
                        session.CancelBurstUntilUtc = DateTime.UtcNow
                            .AddMilliseconds(CancelBurstMilliseconds);
                    }
                }
                requests = _requests.Values
                    .Where(item => string.Equals(
                        item.Request.SessionId,
                        sessionId,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            foreach (var item in requests) CancelItem(item);
        }

        private void CancelItem(TransferWorkItem item)
        {
            if (item == null) return;
            Process process = null;
            lock (_syncRoot)
            {
                if (item.IsTerminal) return;
                item.Cancel();
                if (ReferenceEquals(_activeItem, item) &&
                    !item.IsCommitInProgress)
                {
                    process = _activeAdbProcess;
                }
            }
            if (process != null) TryKill(process);
        }

        private void SetActiveProcess(
            TransferWorkItem item,
            Process process)
        {
            var shouldKill = false;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeItem, item))
                    _activeAdbProcess = process;
                shouldKill = item.IsCanceled &&
                    !item.IsCommitInProgress;
            }
            if (shouldKill) TryKill(process);
        }

        private void ClearActiveProcess(Process process)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeAdbProcess, process))
                    _activeAdbProcess = null;
            }
        }

        private static void TryKill(Process process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        private static bool IsManagedRemoteDirectory(
            string value,
            string expected)
        {
            return string.Equals(
                NormalizeRemoteDirectory(value),
                NormalizeRemoteDirectory(expected),
                StringComparison.Ordinal);
        }

        private static string NormalizeRemoteDirectory(string value)
        {
            var normalized = (value ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
            while (normalized.Contains("//"))
                normalized = normalized.Replace("//", "/");
            normalized = normalized.TrimEnd('/');
            if (!normalized.StartsWith(
                    "/sdcard/",
                    StringComparison.Ordinal) &&
                !normalized.StartsWith(
                    "/storage/emulated/0/",
                    StringComparison.Ordinal))
            {
                normalized = FileTransferEnvironment
                    .DefaultRemoteDirectory
                    .TrimEnd('/');
            }
            return normalized;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
            var difference = leftBytes.Length ^ rightBytes.Length;
            var length = Math.Max(leftBytes.Length, rightBytes.Length);
            for (var index = 0; index < length; index++)
            {
                var leftValue = index < leftBytes.Length
                    ? leftBytes[index]
                    : (byte)0;
                var rightValue = index < rightBytes.Length
                    ? rightBytes[index]
                    : (byte)0;
                difference |= leftValue ^ rightValue;
            }
            return difference == 0;
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string FormatBytes(long bytes)
        {
            var value = (double)Math.Max(bytes, 0L);
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var unit = 0;
            while (value >= 1024D && unit < units.Length - 1)
            {
                value /= 1024D;
                unit++;
            }
            return value.ToString(
                unit == 0 ? "0" : "0.##",
                CultureInfo.InvariantCulture) + " " + units[unit];
        }

        private static string GetTaskResult(
            System.Threading.Tasks.Task<string> task)
        {
            try { return task.GetAwaiter().GetResult(); }
            catch { return string.Empty; }
        }

        private sealed class TransferSession
        {
            internal TransferSession(
                string id,
                string serial,
                string displayName,
                string remoteDirectory)
            {
                Id = id;
                Serial = serial;
                DisplayName = displayName ?? string.Empty;
                RemoteDirectory = NormalizeRemoteDirectory(remoteDirectory);
                Active = true;
            }

            internal string Id { get; private set; }
            internal string Serial { get; private set; }
            internal string DisplayName { get; private set; }
            internal string RemoteDirectory { get; private set; }
            internal bool Active { get; set; }
            internal int ProcessId { get; set; }
            internal IntPtr WindowHandle { get; set; }
            internal int CompletedCount { get; set; }
            internal int FailedCount { get; set; }
            internal DateTime CancelBurstUntilUtc { get; set; }
        }

        private sealed class TransferWorkItem
        {
            private int _canceled;
            private int _commitInProgress;
            private int _terminal;

            internal TransferWorkItem(
                FileTransferRequestMessage request,
                TransferSession session,
                NamedPipeServerStream pipe)
            {
                Request = request;
                Session = session;
                Pipe = pipe;
                DisconnectBuffer = new byte[1];
                Entries = new List<TransferEntry>();
                CurrentStage = FileTransferStage.Queued;
            }

            internal FileTransferRequestMessage Request { get; private set; }
            internal TransferSession Session { get; private set; }
            internal NamedPipeServerStream Pipe { get; private set; }
            internal byte[] DisconnectBuffer { get; private set; }
            internal string FileName { get; set; }
            internal string FinalFileName { get; set; }
            internal string RemoteCommitMarkerPath { get; set; }
            internal string RemoteTemporaryPath { get; set; }
            internal string RemoteStagingDirectory { get; set; }
            internal string RootName { get; set; }
            internal long FileSize { get; set; }
            internal long TotalSize { get; set; }
            internal bool RenameCompleted { get; set; }
            internal bool FinalCommitRecoveryPending { get; set; }
            internal bool DirectoryTransfer { get; set; }
            internal List<TransferEntry> Entries { get; set; }
            internal int CurrentEntryIndex { get; set; }
            internal FileTransferStage CurrentStage { get; set; }
            internal string CurrentMessage { get; set; }
            internal int CurrentPercent { get; set; }
            internal int CompletedEntryCount { get; set; }
            internal DateTime StartedUtc { get; set; }
            internal TransferEntry CurrentEntry
            {
                get
                {
                    return CurrentEntryIndex >= 0 &&
                        CurrentEntryIndex < Entries.Count
                        ? Entries[CurrentEntryIndex]
                        : null;
                }
            }
            internal bool IsCanceled
            {
                get { return Interlocked.CompareExchange(ref _canceled, 0, 0) != 0; }
            }
            internal bool IsCommitInProgress
            {
                get
                {
                    return Interlocked.CompareExchange(
                        ref _commitInProgress,
                        0,
                        0) != 0;
                }
            }
            internal bool IsTerminal
            {
                get { return Interlocked.CompareExchange(ref _terminal, 0, 0) != 0; }
            }

            internal void Cancel()
            {
                Interlocked.Exchange(ref _canceled, 1);
            }

            internal void BeginCommit()
            {
                Interlocked.Exchange(ref _commitInProgress, 1);
            }

            internal void EndCommit()
            {
                Interlocked.Exchange(ref _commitInProgress, 0);
            }

            internal void MarkTerminal()
            {
                Interlocked.Exchange(ref _terminal, 1);
            }
        }

        private sealed class TransferEntry
        {
            internal TransferEntry(
                string localPath,
                string relativePath,
                string displayName,
                long fileSize,
                bool isDirectory)
            {
                LocalPath = localPath;
                RelativePath = relativePath;
                DisplayName = displayName;
                FileSize = fileSize;
                IsDirectory = isDirectory;
            }

            internal string LocalPath { get; private set; }
            internal string RelativePath { get; private set; }
            internal string DisplayName { get; private set; }
            internal long FileSize { get; private set; }
            internal bool IsDirectory { get; private set; }
        }

        private sealed class DirectoryScanItem
        {
            internal DirectoryScanItem(
                DirectoryInfo directory,
                string relativePath)
            {
                Directory = directory;
                RelativePath = relativePath;
            }

            internal DirectoryInfo Directory { get; private set; }
            internal string RelativePath { get; private set; }
        }

        private sealed class AdbExecutionResult
        {
            internal AdbExecutionResult(
                int exitCode,
                string outputTail,
                string errorTail)
            {
                ExitCode = exitCode;
                OutputTail = outputTail ?? string.Empty;
                ErrorTail = errorTail ?? string.Empty;
            }

            internal int ExitCode { get; private set; }
            internal string OutputTail { get; private set; }
            internal string ErrorTail { get; private set; }
        }

        private sealed class BoundedTextBuffer
        {
            private const int MaximumCharacters = 65536;
            private readonly object _syncRoot = new object();
            private readonly StringBuilder _builder = new StringBuilder();

            internal void Append(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                lock (_syncRoot)
                {
                    _builder.Append(value);
                    if (_builder.Length > MaximumCharacters)
                        _builder.Remove(
                            0,
                            _builder.Length - MaximumCharacters);
                }
            }

            internal string Value
            {
                get
                {
                    lock (_syncRoot) return _builder.ToString().Trim();
                }
            }
        }
    }
}
