using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport.Logging;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Notifications
{
    /// <summary>
    /// Sends a Slack Block Kit message via an Incoming Webhook URL.
    /// </summary>
    public sealed class SlackNotifier : INotificationProvider
    {
        private readonly string _webhookUrl;
        private static readonly Lazy<HttpClient> _http = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ChannelId => "slack";

        /// <param name="webhookUrl">Slack Incoming Webhook URL from your Slack app configuration.</param>
        public SlackNotifier(string webhookUrl)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
                throw new ArgumentException("Slack webhook URL is required.", nameof(webhookUrl));
            _webhookUrl = webhookUrl;
        }

        /// <inheritdoc/>
        public async Task<bool> SendAsync(NotificationPayload p, CancellationToken ct = default)
        {
            try
            {
                var emoji  = p.Failed > 0 ? ":x:" : ":white_check_mark:";
                var status = p.Failed > 0 ? "FAILED" : "PASSED";
                var dur    = TimeSpan.FromMilliseconds(p.TotalDurationMs);

                var blocks = new object[]
                {
                    new { type = "header", text = new { type = "plain_text", text = $"{emoji} {p.RunName} — {status}" } },
                    new
                    {
                        type   = "section",
                        fields = new object[]
                        {
                            new { type = "mrkdwn", text = $"*Total:*\n{p.TotalTests}" },
                            new { type = "mrkdwn", text = $"*Passed:*\n{p.Passed}" },
                            new { type = "mrkdwn", text = $"*Failed:*\n{p.Failed}" },
                            new { type = "mrkdwn", text = $"*Skipped:*\n{p.Skipped}" },
                            new { type = "mrkdwn", text = $"*Pass Rate:*\n{p.PassRate:F1}%" },
                            new { type = "mrkdwn", text = $"*Duration:*\n{dur:mm\\:ss}" }
                        }
                    },
                    new
                    {
                        type     = "context",
                        elements = new object[]
                        {
                            new { type = "mrkdwn", text = $"Env: *{p.Environment ?? "—"}* | Branch: *{p.Branch ?? "—"}* | Build: *{p.BuildNumber ?? "—"}*" }
                        }
                    }
                };

                var payload = p.ReportUrl != null
                    ? new { blocks, text = $"{p.RunName} {status}", attachments = (object?)null }
                    : new { blocks, text = $"{p.RunName} {status}", attachments = (object?)null };

                // Add report link action if available
                var finalPayload = p.ReportUrl != null
                    ? (object)new
                    {
                        blocks = blocks.Concat(new object[]
                        {
                            new
                            {
                                type     = "actions",
                                elements = new[] { new { type = "button", text = new { type = "plain_text", text = "View Report" }, url = p.ReportUrl } }
                            }
                        }),
                        text = $"{p.RunName} {status}"
                    }
                    : (object)new { blocks, text = $"{p.RunName} {status}" };

                var json    = JsonSerializer.Serialize(finalPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp    = await _http.Value.PostAsync(_webhookUrl, content, ct).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LibraryLogger.ForCategory("SimpleSeleniumSupport.Notifications.SlackNotifier")
                    .LogWarning(ex, "Slack notification failed: {Message}", ex.Message);
                return false;
            }
        }
    }
}
