using System.Diagnostics;
using OpenQA.Selenium;

namespace SimpleSeleniumSupport.Recording
{
    /// <summary>
    /// Records test execution by polling <see cref="ITakesScreenshot.GetScreenshot"/> and
    /// piping the PNG frames into FFmpeg's stdin for H.264 encoding.
    /// <para>
    /// Works in <b>headless and headed</b> Chrome/Edge/Firefox, locally and on Selenium Grid —
    /// no OS-level display capture required.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>Prerequisite:</b> FFmpeg must be installed and accessible via the path configured
    /// in <see cref="VideoRecordingOptions.FfmpegPath"/> (default: <c>"ffmpeg"</c> on PATH).
    /// </remarks>
    public sealed class LocalVideoRecorder : IVideoRecorder
    {
        private readonly IWebDriver _driver;
        private readonly VideoRecordingOptions _options;

        private Process? _ffmpeg;
        private Task? _captureTask;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        /// <inheritdoc/>
        public string? VideoPath { get; private set; }

        /// <inheritdoc/>
        public bool IsRecording { get; private set; }

        /// <summary>
        /// Creates a new <see cref="LocalVideoRecorder"/>.
        /// Call <see cref="Start"/> (or use the <see cref="VideoRecordingExtensions.StartLocalRecording"/> extension)
        /// to begin capturing.
        /// </summary>
        /// <param name="driver">The WebDriver instance. Must implement <see cref="ITakesScreenshot"/>.</param>
        /// <param name="options">Recording options; <see langword="null"/> uses library defaults.</param>
        /// <exception cref="ArgumentNullException"><paramref name="driver"/> is <see langword="null"/>.</exception>
        public LocalVideoRecorder(IWebDriver driver, VideoRecordingOptions? options = null)
        {
            _driver  = driver ?? throw new ArgumentNullException(nameof(driver));
            _options = options ?? new VideoRecordingOptions();
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">
        /// Thrown when recording is already in progress, FFmpeg is not found,
        /// or the driver does not implement <see cref="ITakesScreenshot"/>.
        /// </exception>
        public void Start()
        {
            if (IsRecording)
                throw new InvalidOperationException("Recording is already in progress. Call Stop() first.");

            if (_driver is not ITakesScreenshot)
                throw new InvalidOperationException(
                    $"The supplied IWebDriver ({_driver.GetType().Name}) does not implement ITakesScreenshot. " +
                    "LocalVideoRecorder requires a driver that can capture screenshots.");

            Directory.CreateDirectory(_options.OutputDirectory);

            var fileName = string.IsNullOrWhiteSpace(_options.TestName)
                ? $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4"
                : $"{SanitiseFileName(_options.TestName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";

            VideoPath = Path.Combine(_options.OutputDirectory, fileName);

            var scaleFilter = _options.Resolution.ToScaleFilter();
            var fps         = _options.FrameRate;

            // Build FFmpeg arguments:
            //   -f image2pipe   — read image frames from stdin
            //   -vcodec png     — each frame is a PNG
            //   -framerate N    — declared input frame rate (matches our polling rate)
            //   -i pipe:0       — read from stdin
            //   -vf "scale=W:H:flags=lanczos"  — resize to target resolution
            //   -c:v libx264    — H.264 encoding
            //   -preset ultrafast — low CPU overhead during capture
            //   -pix_fmt yuv420p  — broadest player compatibility
            var args = $"-y -f image2pipe -vcodec png -framerate {fps} -i pipe:0 " +
                       $"-vf \"{scaleFilter}\" -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{VideoPath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName               = _options.FfmpegPath,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = false,
                RedirectStandardError  = false,
                CreateNoWindow         = true
            };

            try
            {
                _ffmpeg = Process.Start(startInfo);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                throw new InvalidOperationException(
                    $"FFmpeg could not be started from path \"{_options.FfmpegPath}\". " +
                    "Ensure FFmpeg is installed and available on PATH, or set VideoRecordingOptions.FfmpegPath " +
                    "to its full path. Download FFmpeg from https://ffmpeg.org/download.html", ex);
            }

            if (_ffmpeg == null)
                throw new InvalidOperationException("Failed to start FFmpeg process.");

            _cts        = new CancellationTokenSource();
            IsRecording = true;

            var token          = _cts.Token;
            var frameIntervalMs = 1000.0 / fps;
            var screenshotter   = (ITakesScreenshot)_driver;
            var ffmpegStdin     = _ffmpeg.StandardInput.BaseStream;

            _captureTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var frameStart = DateTime.UtcNow;

                    try
                    {
                        var pngBytes = screenshotter.GetScreenshot().AsByteArray;
                        await ffmpegStdin.WriteAsync(pngBytes, 0, pngBytes.Length, token).ConfigureAwait(false);
                        await ffmpegStdin.FlushAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Individual frame failures are non-fatal — log and continue.
                        Console.WriteLine($"[LocalVideoRecorder] Frame capture failed: {ex.Message}");
                    }

                    var elapsed = (DateTime.UtcNow - frameStart).TotalMilliseconds;
                    var delay   = (int)(frameIntervalMs - elapsed);
                    if (delay > 0)
                    {
                        try { await Task.Delay(delay, token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { break; }
                    }
                }
            }, token);
        }

        /// <inheritdoc/>
        public void Stop(bool discard = false)
            => StopAsync(discard).GetAwaiter().GetResult();

        /// <inheritdoc/>
        public async Task StopAsync(bool discard = false, CancellationToken ct = default)
        {
            if (!IsRecording)
                return;

            IsRecording = false;

            // 1. Signal the capture loop to stop.
            _cts?.Cancel();

            // 2. Wait for the capture task to finish draining.
            if (_captureTask != null)
            {
                try { await _captureTask.ConfigureAwait(false); }
                catch { /* capture loop exceptions are non-fatal */ }
            }

            // 3. Close stdin — this signals EOF to FFmpeg so it finalises the file.
            try { _ffmpeg?.StandardInput.Close(); }
            catch { /* may already be closed */ }

            // 4. Wait for FFmpeg to finish encoding (15 s timeout).
            if (_ffmpeg != null)
            {
                var exited = await Task.Run(() => _ffmpeg.WaitForExit(15_000), ct).ConfigureAwait(false);
                if (!exited)
                {
                    Console.WriteLine("[LocalVideoRecorder] FFmpeg did not exit within 15 s — killing process.");
                    try { _ffmpeg.Kill(); } catch { }
                }
            }

            // 5. Discard the file if requested.
            if (discard)
            {
                if (VideoPath != null && File.Exists(VideoPath))
                {
                    try { File.Delete(VideoPath); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LocalVideoRecorder] Could not delete recording: {ex.Message}");
                    }
                }

                VideoPath = null;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (IsRecording)
            {
                try { Stop(); }
                catch { /* best-effort on dispose */ }
            }

            _cts?.Dispose();
            _ffmpeg?.Dispose();
        }

        private static string SanitiseFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }
    }
}
