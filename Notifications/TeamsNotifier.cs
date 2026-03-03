using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport.Logging;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Notifications
{
    /// <summary>
    /// Sends a Microsoft Teams Adaptive Card (v1.5) message via a webhook URL.
    /// </summary>
    public sealed class TeamsNotifier : INotificationProvider
    {
        private readonly string _webhookUrl;
        private static readonly Lazy<HttpClient> _http = new(() => new HttpClient());
        private static readonly ILogger _log = LibraryLogger.ForCategory("SimpleSeleniumSupport.Notifications.TeamsNotifier");

        /// <inheritdoc/>
        public string ChannelId => "teams";

        /// <param name="webhookUrl">Teams Incoming Webhook URL from your Teams channel connector.</param>
        public TeamsNotifier(string webhookUrl)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
                throw new ArgumentException("Teams webhook URL is required.", nameof(webhookUrl));
            _webhookUrl = webhookUrl;
        }

        /// <inheritdoc/>
        public async Task<bool> SendAsync(NotificationPayload p, CancellationToken ct = default)
        {
            try
            {
                var color  = p.Failed > 0 ? "attention" : "good";
                var status = p.Failed > 0 ? "FAILED" : "PASSED";
                var dur    = TimeSpan.FromMilliseconds(p.TotalDurationMs);

                var card = new
                {
                    type        = "message",
                    attachments = new[]
                    {
                        new
                        {
                            contentType = "application/vnd.microsoft.card.adaptive",
                            content     = new
                            {
                                type    = "AdaptiveCard",
                                version = "1.5",
                                body    = new object[]
                                {
                                    new { type = "TextBlock", text = $"{p.RunName} — {status}", weight = "Bolder", size = "Medium", color },
                                    new
                                    {
                                        type  = "FactSet",
                                        facts = new object[]
                                        {
                                            new { title = "Total",      value = p.TotalTests.ToString() },
                                            new { title = "Passed",     value = p.Passed.ToString() },
                                            new { title = "Failed",     value = p.Failed.ToString() },
                                            new { title = "Pass Rate",  value = $"{p.PassRate:F1}%" },
                                            new { title = "Duration",   value = $"{dur:mm\\:ss}" },
                                            new { title = "Environment", value = p.Environment ?? "—" },
                                            new { title = "Branch",     value = p.Branch ?? "—" },
                                            new { title = "Build",      value = p.BuildNumber ?? "—" }
                                        }
                                    }
                                },
                                actions = p.ReportUrl != null ? new object[]
                                {
                                    new { type = "Action.OpenUrl", title = "View Report", url = p.ReportUrl }
                                } : Array.Empty<object>()
                            }
                        }
                    }
                };

                var json    = JsonSerializer.Serialize(card);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp    = await _http.Value.PostAsync(_webhookUrl, content, ct).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Teams notification failed: {Message}", ex.Message);
                return false;
            }
        }
    }
}
