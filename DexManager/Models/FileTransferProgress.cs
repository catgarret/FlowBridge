using System;

namespace DexManager.Models
{
    public enum FileTransferStage
    {
        Queued = 0,
        Transferring = 1,
        Finalizing = 2,
        Completed = 3,
        Failed = 4,
        Canceled = 5
    }

    public sealed class FileTransferProgress
    {
        internal FileTransferProgress(
            string requestId,
            string sessionId,
            FileTransferStage stage,
            string fileName,
            string finalFileName,
            long fileSize,
            int percent,
            int completedCount,
            int failedCount,
            int queuedCount,
            string message)
        {
            RequestId = requestId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            Stage = stage;
            FileName = fileName ?? string.Empty;
            FinalFileName = finalFileName ?? string.Empty;
            FileSize = fileSize;
            Percent = percent;
            CompletedCount = completedCount;
            FailedCount = failedCount;
            QueuedCount = queuedCount;
            Message = message ?? string.Empty;
        }

        public string RequestId { get; private set; }
        public string SessionId { get; private set; }
        public FileTransferStage Stage { get; private set; }
        public string FileName { get; private set; }
        public string FinalFileName { get; private set; }
        public long FileSize { get; private set; }
        public int Percent { get; private set; }
        public int CompletedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int QueuedCount { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class FileTransferProgressEventArgs : EventArgs
    {
        internal FileTransferProgressEventArgs(FileTransferProgress progress)
        {
            Progress = progress;
        }

        public FileTransferProgress Progress { get; private set; }
    }
}
