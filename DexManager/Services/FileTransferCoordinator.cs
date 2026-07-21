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
        private const int ProcessPollMilliseconds = 100;
        private const int ProgressThrottleMilliseconds = 150;
        private readonly object _syncRoot = new object();
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
        private readonly Thread _acceptThread;
        private readonly Thread _workerThread;
        private NamedPipeServerStream _waitingPipe;
        private TransferWorkItem _activeItem;
        private Process _activeAdbProcess;
        private int _shutdownRequested;
        private int _disposed;
        private bool _proxyMissingLogged;

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

        public string BeginSession(string serial, string displayName)
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
                    displayName);
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
                    FileTransferEnvironment.DefaultRemoteDirectory;
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
            lock (_syncRoot)
            {
                sessions = _sessions.Values
                    .Where(item => string.Equals(
                        item.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Id)
                    .ToArray();
            }
            foreach (var sessionId in sessions)
                CancelSessionRequests(sessionId, false);
        }

        public void CancelTransfer(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return;
            TransferWorkItem active;
            lock (_syncRoot) active = _activeItem;
            if (active != null && string.Equals(
                active.Request.RequestId,
                requestId,
                StringComparison.OrdinalIgnoreCase))
            {
                CancelSessionRequests(
                    active.Request.SessionId,
                    true);
                return;
            }

            foreach (var item in _queue.ToArray())
            {
                if (!string.Equals(
                    item.Request.RequestId,
                    requestId,
                    StringComparison.OrdinalIgnoreCase)) continue;
                CancelSessionRequests(
                    item.Request.SessionId,
                    true);
                break;
            }
        }

        public void RequestShutdown()
        {
            if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0) return;

            NamedPipeServerStream waiting;
            lock (_syncRoot)
            {
                waiting = _waitingPipe;
                foreach (var session in _sessions.Values)
                    session.Active = false;
            }
            if (waiting != null)
            {
                try { waiting.Dispose(); }
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

                if (IsCancelBurstActive(session))
                {
                    ExtendCancelBurst(session);
                    SendImmediateFailure(
                        pipe,
                        LocalizationService.Get(
                            "FileTransfer.CanceledByUser"),
                        true);
                    return;
                }

                item = new TransferWorkItem(request, session, pipe);
                ArmClientDisconnectMonitor(item);
                _queue.Add(item);
                Publish(item, FileTransferStage.Queued, -1, string.Empty);
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
                if (pipe != null) pipe.Dispose();
            }
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
                !File.Exists(request.LocalPath))
            {
                error = LocalizationService.Get(
                    "FileTransfer.SourceUnavailable");
                return false;
            }
            if (!IsManagedRemoteDirectory(request.RemoteDirectory))
            {
                error = LocalizationService.Get(
                    "FileTransfer.TargetRejected");
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
                        CleanupRemoteTemporaryFile(item);
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

            var file = new FileInfo(item.Request.LocalPath);
            var fileName = file.Name;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                CompleteFailed(item, LocalizationService.Get(
                    "FileTransfer.InvalidFileName"));
                return;
            }
            if (Encoding.UTF8.GetByteCount(fileName) > MaximumFileNameBytes)
            {
                CompleteFailed(item, LocalizationService.Format(
                    "FileTransfer.FileNameTooLong",
                    MaximumFileNameBytes));
                return;
            }

            item.FileName = fileName;
            item.FileSize = file.Length;
            item.RemoteTemporaryPath =
                "/sdcard/Download/.dxm-" +
                Guid.NewGuid().ToString("N") + ".part";
            _logService.Info(LocalizationService.Format(
                "Log.FileTransfer.Starting",
                fileName,
                FormatBytes(file.Length),
                item.Session.Serial));
            Publish(item, FileTransferStage.Transferring, 0, string.Empty);

            var pushResult = RunAdbPush(item);
            if (item.IsCanceled)
            {
                CleanupRemoteTemporaryFile(item);
                CompleteCanceled(item);
                return;
            }
            if (pushResult.ExitCode != 0)
            {
                CleanupRemoteTemporaryFile(item);
                CompleteFailed(item,
                    string.IsNullOrWhiteSpace(pushResult.ErrorTail)
                        ? LocalizationService.Get(
                            "FileTransfer.PushFailed")
                        : pushResult.ErrorTail);
                return;
            }

            Publish(item, FileTransferStage.Finalizing, 100, string.Empty);
            string finalFileName;
            string renameError;
            if (!TryFinalizeRemoteFile(
                item,
                out finalFileName,
                out renameError))
            {
                CleanupRemoteTemporaryFile(item);
                if (item.IsCanceled) CompleteCanceled(item);
                else CompleteFailed(item, renameError);
                return;
            }

            item.RenameCompleted = true;
            item.FinalFileName = finalFileName;
            CompleteSuccess(item);
        }

        private AdbExecutionResult RunAdbPush(TransferWorkItem item)
        {
            var arguments = WindowsCommandLine.Build(new[]
            {
                "-s",
                item.Session.Serial,
                "push",
                "-p",
                item.Request.LocalPath,
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
                    delegate(int percent)
                    {
                        PublishProgressThrottled(item, percent);
                    });

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
            var script = BuildFinalizeScript(
                item.RemoteTemporaryPath,
                name,
                collisionStem,
                collisionExtension);
            var result = RunShellScript(item, script, ShortAdbTimeoutMs);
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
                finalFileName = collisionIndex == 0
                    ? name
                    : collisionStem + " (" + collisionIndex.ToString(
                        CultureInfo.InvariantCulture) + ")" +
                        collisionExtension;
                if (Encoding.UTF8.GetByteCount(finalFileName) >
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
                    ? LocalizationService.Get(
                        "FileTransfer.RenameFailed")
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
            string temporaryPath,
            string name,
            string stem,
            string extension)
        {
            var builder = new StringBuilder();
            builder.AppendLine("set -e");
            builder.AppendLine("dir='/sdcard/Download'");
            builder.Append("tmp='").Append(temporaryPath).AppendLine("'");
            builder.Append("name=\"$(printf '%s' '")
                .Append(ToBase64(name))
                .AppendLine("' | base64 -d)\"");
            builder.Append("stem=\"$(printf '%s' '")
                .Append(ToBase64(stem))
                .AppendLine("' | base64 -d)\"");
            builder.Append("ext=\"$(printf '%s' '")
                .Append(ToBase64(extension))
                .AppendLine("' | base64 -d)\"");
            builder.AppendLine("candidate=\"$name\"");
            builder.AppendLine("index=0");
            builder.AppendLine("while [ -e \"$dir/$candidate\" ]; do");
            builder.AppendLine("  index=$((index + 1))");
            builder.Append("  [ \"$index\" -le ")
                .Append(MaximumCollisionIndex.ToString(
                    CultureInfo.InvariantCulture))
                .AppendLine(" ] || exit 52");
            builder.AppendLine("  candidate=\"$stem ($index)$ext\"");
            builder.AppendLine("done");
            builder.AppendLine("mv -n \"$tmp\" \"$dir/$candidate\"");
            builder.AppendLine("[ ! -e \"$tmp\" ] || exit 53");
            builder.AppendLine("printf 'DXM_INDEX=%s\\n' \"$index\"");
            return builder.ToString();
        }

        private AdbExecutionResult RunShellScript(
            TransferWorkItem item,
            string script,
            int timeoutMs)
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
                var bytes = Encoding.ASCII.GetBytes(script + "\n");
                process.StandardInput.BaseStream.Write(
                    bytes,
                    0,
                    bytes.Length);
                process.StandardInput.BaseStream.Flush();
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var stopwatch = Stopwatch.StartNew();
                while (!process.WaitForExit(ProcessPollMilliseconds))
                {
                    if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                    {
                        TryKill(process);
                    }
                }
                ClearActiveProcess(process);
                return new AdbExecutionResult(
                    process.ExitCode,
                    GetTaskResult(output),
                    GetTaskResult(error));
            }
        }

        private void CleanupRemoteTemporaryFile(TransferWorkItem item)
        {
            if (string.IsNullOrWhiteSpace(item.RemoteTemporaryPath) ||
                item.RenameCompleted) return;
            try
            {
                var script = "rm -f '" + item.RemoteTemporaryPath + "'\n";
                RunCleanupScript(item.Session.Serial, script);
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    ex.Message));
            }
        }

        private void RunCleanupScript(string serial, string script)
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
                    RedirectStandardError = true
                }
            })
            {
                process.Start();
                var bytes = Encoding.ASCII.GetBytes(script + "\n");
                process.StandardInput.BaseStream.Write(
                    bytes,
                    0,
                    bytes.Length);
                process.StandardInput.BaseStream.Flush();
                process.StandardInput.Close();
                if (!process.WaitForExit(ShortAdbTimeoutMs))
                {
                    TryKill(process);
                    process.WaitForExit(1000);
                }
            }
        }

        private void CompleteSuccess(TransferWorkItem item)
        {
            lock (_syncRoot) item.Session.CompletedCount++;
            item.MarkTerminal();
            Publish(item, FileTransferStage.Completed, 100, string.Empty);
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
            lock (_syncRoot) item.Session.FailedCount++;
            item.MarkTerminal();
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
            item.MarkTerminal();
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

        private void SendResponse(
            TransferWorkItem item,
            FileTransferResponseMessage response)
        {
            try { FileTransferWire.Write(item.Pipe, response); }
            catch { }
            finally
            {
                try { item.Pipe.Dispose(); }
                catch { }
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

        private void PublishProgressThrottled(
            TransferWorkItem item,
            int percent)
        {
            var now = Environment.TickCount;
            if (percent == item.LastPublishedPercent &&
                unchecked(now - item.LastProgressTick) <
                    ProgressThrottleMilliseconds)
            {
                return;
            }
            item.LastPublishedPercent = percent;
            item.LastProgressTick = now;
            Publish(item, FileTransferStage.Transferring, percent, string.Empty);
        }

        private void Publish(
            TransferWorkItem item,
            FileTransferStage stage,
            int percent,
            string message)
        {
            int completed;
            int failed;
            lock (_syncRoot)
            {
                completed = item.Session.CompletedCount;
                failed = item.Session.FailedCount;
            }
            var progress = new FileTransferProgress(
                item.Request.RequestId,
                item.Request.SessionId,
                stage,
                string.IsNullOrWhiteSpace(item.FileName)
                    ? Path.GetFileName(item.Request.LocalPath)
                    : item.FileName,
                item.FinalFileName,
                item.FileSize,
                percent,
                completed,
                failed,
                _queue.Count,
                message);
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

        private void CancelSessionRequests(
            string sessionId,
            bool setBurst)
        {
            if (setBurst) SetCancelBurst(sessionId);
            TransferWorkItem active;
            lock (_syncRoot) active = _activeItem;
            if (active != null && string.Equals(
                active.Request.SessionId,
                sessionId,
                StringComparison.OrdinalIgnoreCase))
            {
                CancelItem(active);
            }
            foreach (var item in _queue.ToArray())
            {
                if (string.Equals(
                    item.Request.SessionId,
                    sessionId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    CancelItem(item);
                }
            }
        }

        private void CancelItem(TransferWorkItem item)
        {
            if (item == null || item.IsTerminal) return;
            item.Cancel();
            Process process = null;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeItem, item))
                    process = _activeAdbProcess;
            }
            if (process != null) TryKill(process);
        }

        private void SetActiveProcess(
            TransferWorkItem item,
            Process process)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeItem, item))
                    _activeAdbProcess = process;
            }
            if (item.IsCanceled) TryKill(process);
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

        private bool IsCancelBurstActive(TransferSession session)
        {
            lock (_syncRoot)
                return session.CancelBurstUntilUtc > DateTime.UtcNow;
        }

        private void SetCancelBurst(string sessionId)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                if (_sessions.TryGetValue(sessionId ?? string.Empty,
                    out session))
                {
                    session.CancelBurstUntilUtc = DateTime.UtcNow.AddMilliseconds(
                        CancelBurstMilliseconds);
                }
            }
        }

        private void ExtendCancelBurst(TransferSession session)
        {
            lock (_syncRoot)
            {
                session.CancelBurstUntilUtc = DateTime.UtcNow.AddMilliseconds(
                    CancelBurstMilliseconds);
            }
        }

        private static bool IsManagedRemoteDirectory(string value)
        {
            return string.Equals(
                (value ?? string.Empty).Replace('\\', '/').TrimEnd('/'),
                "/sdcard/Download",
                StringComparison.Ordinal);
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
                string displayName)
            {
                Id = id;
                Serial = serial;
                DisplayName = displayName ?? string.Empty;
                Active = true;
            }

            internal string Id { get; private set; }
            internal string Serial { get; private set; }
            internal string DisplayName { get; private set; }
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
                LastPublishedPercent = -1;
            }

            internal FileTransferRequestMessage Request { get; private set; }
            internal TransferSession Session { get; private set; }
            internal NamedPipeServerStream Pipe { get; private set; }
            internal byte[] DisconnectBuffer { get; private set; }
            internal string FileName { get; set; }
            internal string FinalFileName { get; set; }
            internal string RemoteTemporaryPath { get; set; }
            internal long FileSize { get; set; }
            internal bool RenameCompleted { get; set; }
            internal int LastPublishedPercent { get; set; }
            internal int LastProgressTick { get; set; }
            internal bool IsCanceled
            {
                get { return Interlocked.CompareExchange(ref _canceled, 0, 0) != 0; }
            }
            internal bool IsTerminal
            {
                get { return Interlocked.CompareExchange(ref _terminal, 0, 0) != 0; }
            }

            internal void Cancel()
            {
                Interlocked.Exchange(ref _canceled, 1);
            }

            internal void MarkTerminal()
            {
                Interlocked.Exchange(ref _terminal, 1);
            }
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
