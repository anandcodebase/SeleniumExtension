using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Watches a directory for new files that appear after a download-triggering action.
    /// Used by <see cref="SeleniumPage.ExpectDownload"/> to detect and capture downloaded files.
    /// </summary>
    internal sealed class DownloadWatcher : IDisposable
    {
        private readonly string _directory;
        private readonly HashSet<string> _snapshot;
        private readonly FileSystemWatcher _watcher;
        private volatile string? _detectedPath;
        private readonly ManualResetEventSlim _fileArrived = new ManualResetEventSlim(false);

        internal DownloadWatcher(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);

            // Snapshot existing files so we only detect new ones
            _snapshot = new HashSet<string>(
                Directory.GetFiles(directory),
                StringComparer.OrdinalIgnoreCase);

            _watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Created += OnFile;
            _watcher.Renamed += OnRename;
        }

        private void OnFile(object sender, FileSystemEventArgs e)
        {
            if (!_snapshot.Contains(e.FullPath) && !IsPartialFile(e.Name))
            {
                _detectedPath = e.FullPath;
                _fileArrived.Set();
            }
        }

        private void OnRename(object sender, RenamedEventArgs e)
        {
            if (!IsPartialFile(e.Name))
            {
                _detectedPath = e.FullPath;
                _fileArrived.Set();
            }
        }

        /// <summary>
        /// Waits up to <paramref name="timeoutMs"/> milliseconds for a new file to appear.
        /// Returns a <see cref="DownloadInfo"/> for the first detected file.
        /// </summary>
        internal DownloadInfo WaitForDownload(int timeoutMs = 30_000)
        {
            if (!_fileArrived.Wait(timeoutMs))
                throw new TimeoutException($"No download detected in {_directory} within {timeoutMs / 1000}s.");

            // Wait a moment for the file write to complete
            Thread.Sleep(500);

            var path = _detectedPath!;

            // If no FileSystemWatcher event fired but a new file appeared (race), fall back to scan
            if (string.IsNullOrEmpty(path))
            {
                var newFiles = Directory.GetFiles(_directory)
                    .Where(f => !_snapshot.Contains(f) && !IsPartialFile(Path.GetFileName(f)))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                    .ToList();
                if (newFiles.Count == 0)
                    throw new TimeoutException($"No download detected in {_directory}.");
                path = newFiles[0];
            }

            return new DownloadInfo(path, Path.GetFileName(path));
        }

        private static bool IsPartialFile(string? name)
        {
            if (name == null) return false;
            var lower = name.ToLowerInvariant();
            return lower.EndsWith(".crdownload") // Chrome in-progress
                || lower.EndsWith(".part")       // Firefox in-progress
                || lower.EndsWith(".download");  // Safari in-progress
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _fileArrived.Dispose();
        }
    }
}
