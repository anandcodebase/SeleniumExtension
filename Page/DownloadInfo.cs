using System;
using System.IO;

namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Information about a file download initiated during a test.
    /// Returned by <see cref="SeleniumPage.ExpectDownload"/>.
    /// Implements <see cref="IDisposable"/>: disposes of the temporary file if it
    /// was written to a temp directory and has not been moved.
    /// </summary>
    public sealed class DownloadInfo : IDisposable
    {
        private bool _disposed;

        /// <summary>Full path to the downloaded file on disk.</summary>
        public string Path { get; }

        /// <summary>Original suggested filename from the browser, if available.</summary>
        public string SuggestedFilename { get; }

        /// <summary>File size in bytes, or <c>-1</c> if unavailable.</summary>
        public long Size
        {
            get
            {
                try { return new FileInfo(Path).Length; }
                catch { return -1; }
            }
        }

        internal DownloadInfo(string path, string suggestedFilename)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            SuggestedFilename = suggestedFilename ?? System.IO.Path.GetFileName(path) ?? "";
        }

        /// <summary>
        /// Copies (or moves) the downloaded file to <paramref name="destinationPath"/>.
        /// Overwrites an existing file at the destination.
        /// </summary>
        public void SaveAs(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentNullException(nameof(destinationPath));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(destinationPath))!);
            File.Copy(Path, destinationPath, overwrite: true);
        }

        /// <summary>Deletes the temporary downloaded file if it still exists.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (File.Exists(Path)) File.Delete(Path); } catch { /* ignore */ }
        }
    }
}
