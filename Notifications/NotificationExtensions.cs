using SimpleSeleniumSupport.Reporting;

namespace SimpleSeleniumSupport.Notifications
{
    /// <summary>
    /// Extension methods for sending test-run notifications.
    /// </summary>
    public static class NotificationExtensions
    {
        /// <summary>
        /// Sends a test-run summary notification via the specified provider.
        /// </summary>
        /// <param name="results">Test results from the completed run.</param>
        /// <param name="provider">The notification channel to use.</param>
        /// <param name="runName">Display name for the run (e.g. "Nightly Regression").</param>
        /// <param name="reportUrl">Optional URL linking to the HTML report.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns><see langword="true"/> if the notification was sent successfully.</returns>
        public static Task<bool> NotifyAsync(
            this IEnumerable<TestResult> results,
            INotificationProvider provider,
            string runName = "Test Run",
            string? reportUrl = null,
            CancellationToken ct = default)
        {
            var payload = NotificationPayload.From(results, runName, reportUrl);
            return provider.SendAsync(payload, ct);
        }

        /// <summary>
        /// Sends to multiple notification channels in parallel.
        /// </summary>
        /// <returns>Dictionary of <c>channelId → success</c>.</returns>
        public static async Task<Dictionary<string, bool>> NotifyAllAsync(
            this IEnumerable<TestResult> results,
            IEnumerable<INotificationProvider> providers,
            string runName = "Test Run",
            string? reportUrl = null,
            CancellationToken ct = default)
        {
            var payload = NotificationPayload.From(results, runName, reportUrl);
            var tasks = providers.Select(async p => (p.ChannelId, await p.SendAsync(payload, ct).ConfigureAwait(false)));
            var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
            return outcomes.ToDictionary(o => o.ChannelId, o => o.Item2);
        }
    }
}
