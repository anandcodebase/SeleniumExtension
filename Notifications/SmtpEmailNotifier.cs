using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace SimpleSeleniumSupport.Notifications
{
    /// <summary>Configuration for <see cref="SmtpEmailNotifier"/>.</summary>
    public sealed class SmtpEmailConfig
    {
        public string  Host        { get; set; } = "localhost";
        public int     Port        { get; set; } = 587;
        public bool    UseSsl      { get; set; } = true;
        public string? Username    { get; set; }
        public string? Password    { get; set; }
        public string  From        { get; set; } = "noreply@example.com";
        /// <summary>Comma-separated recipient addresses.</summary>
        public string  To          { get; set; } = "";
        /// <summary>Email subject template. Use <c>[[RUN_NAME]]</c> and <c>[[STATUS]]</c> tokens.</summary>
        public string  Subject     { get; set; } = "Test Run: [[RUN_NAME]] — [[STATUS]]";
    }

    /// <summary>
    /// Sends a HTML test-run summary email via SMTP.
    /// Uses <see cref="System.Net.Mail.SmtpClient"/> (built into .NET — no extra NuGet required).
    /// </summary>
    public sealed class SmtpEmailNotifier : INotificationProvider
    {
        private readonly SmtpEmailConfig _config;
        private static readonly ILogger _log = LibraryLogger.ForCategory("SimpleSeleniumSupport.Notifications.SmtpEmailNotifier");

        /// <inheritdoc/>
        public string ChannelId => "smtp-email";

        public SmtpEmailNotifier(SmtpEmailConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <inheritdoc/>
        public async Task<bool> SendAsync(NotificationPayload p, CancellationToken ct = default)
        {
            try
            {
                var status  = p.Failed > 0 ? "FAILED" : "PASSED";
                var color   = p.Failed > 0 ? "#dc2626" : "#16a34a";
                var dur     = TimeSpan.FromMilliseconds(p.TotalDurationMs);
                var subject = _config.Subject
                    .Replace("[[RUN_NAME]]", p.RunName)
                    .Replace("[[STATUS]]", status);

                var sb = new StringBuilder();
                sb.Append($"""
                <html><body style="font-family:sans-serif;max-width:600px;margin:auto">
                <h2 style="color:{color}">{p.RunName} — {status}</h2>
                <table style="border-collapse:collapse;width:100%">
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Total</b></td><td style="padding:6px;border:1px solid #e5e7eb">{p.TotalTests}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Passed</b></td><td style="padding:6px;border:1px solid #e5e7eb;color:#16a34a">{p.Passed}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Failed</b></td><td style="padding:6px;border:1px solid #e5e7eb;color:#dc2626">{p.Failed}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Skipped</b></td><td style="padding:6px;border:1px solid #e5e7eb">{p.Skipped}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Pass Rate</b></td><td style="padding:6px;border:1px solid #e5e7eb">{p.PassRate:F1}%</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Duration</b></td><td style="padding:6px;border:1px solid #e5e7eb">{dur:mm\\:ss}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Environment</b></td><td style="padding:6px;border:1px solid #e5e7eb">{p.Environment ?? "—"}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Branch</b></td><td style="padding:6px;border:1px solid #e5e7eb">{p.Branch ?? "—"}</td></tr>
                <tr><td style="padding:6px;border:1px solid #e5e7eb"><b>Build</b></td><td style="padding:6px;border:1px solid #e5e7eb">{p.BuildNumber ?? "—"}</td></tr>
                </table>
                """);

                if (!string.IsNullOrWhiteSpace(p.ReportUrl))
                    sb.Append($"""<p><a href="{p.ReportUrl}" style="color:#2563eb">View Full Report</a></p>""");

                sb.Append("</body></html>");

#pragma warning disable CA1422  // SmtpClient is legacy but functional on all .NET platforms
                using var smtp = new SmtpClient(_config.Host, _config.Port)
                {
                    EnableSsl   = _config.UseSsl,
                    Credentials = _config.Username != null
                        ? new NetworkCredential(_config.Username, _config.Password)
                        : null
                };

                var msg = new MailMessage
                {
                    From       = new MailAddress(_config.From),
                    Subject    = subject,
                    Body       = sb.ToString(),
                    IsBodyHtml = true
                };
                foreach (var to in _config.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    msg.To.Add(to);

                await Task.Run(() => smtp.Send(msg), ct).ConfigureAwait(false);
#pragma warning restore CA1422
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SMTP email notification failed: {Message}", ex.Message);
                return false;
            }
        }
    }
}
