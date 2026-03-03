namespace SimpleSeleniumSupport.Notifications
{
    /// <summary>
    /// Abstraction for sending test-run completion notifications to external channels.
    /// <para>
    /// Implementations must be <b>non-throwing</b> — catch exceptions internally,
    /// log them, and return <see langword="false"/> on failure.
    /// </para>
    /// </summary>
    public interface INotificationProvider
    {
        /// <summary>Unique channel identifier shown in logs (e.g. <c>"slack"</c>, <c>"teams"</c>).</summary>
        string ChannelId { get; }

        /// <summary>
        /// Sends the notification asynchronously.
        /// </summary>
        /// <param name="payload">Run summary data to include in the message.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns><see langword="true"/> on success; <see langword="false"/> on any failure.</returns>
        Task<bool> SendAsync(NotificationPayload payload, CancellationToken ct = default);
    }

    /// <summary>
    /// Unified payload passed to every <see cref="INotificationProvider"/>.
    /// </summary>
    public sealed class NotificationPayload
    {
        public string  RunName         { get; set; } = "Test Run";
        public int     TotalTests      { get; set; }
        public int     Passed          { get; set; }
        public int     Failed          { get; set; }
        public int     Skipped         { get; set; }
        /// <summary>0.0–100.0</summary>
        public double  PassRate        { get; set; }
        public double  TotalDurationMs { get; set; }
        public string? ReportUrl       { get; set; }
        public string? Environment     { get; set; }
        public string? Branch          { get; set; }
        public string? BuildNumber     { get; set; }
        public DateTime RunTimestamp   { get; set; } = DateTime.UtcNow;
        public int     FlakyTestCount  { get; set; }
        public double  WorstFlakiness  { get; set; }
        /// <summary>Arbitrary extra fields included in the notification template.</summary>
        public Dictionary<string, string> CustomFields { get; set; } = new();

        internal static NotificationPayload From(
            IEnumerable<Reporting.TestResult> results,
            string runName,
            string? reportUrl)
        {
            var list    = results.ToList();
            var total   = list.Count;
            var passed  = list.Count(r => r.Status == Reporting.TestStatus.Pass);
            var failed  = list.Count(r => r.Status is Reporting.TestStatus.Fail or Reporting.TestStatus.Error);
            var skipped = list.Count(r => r.Status == Reporting.TestStatus.Skip);

            return new NotificationPayload
            {
                RunName         = runName,
                TotalTests      = total,
                Passed          = passed,
                Failed          = failed,
                Skipped         = skipped,
                PassRate        = total > 0 ? (passed / (double)total) * 100.0 : 0,
                TotalDurationMs = list.Sum(r => r.DurationMs),
                ReportUrl       = reportUrl,
                RunTimestamp    = DateTime.UtcNow
            };
        }
    }
}
