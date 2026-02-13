using SimpleSeleniumSupport.Image;
using System;
using System.Text;

namespace SimpleSeleniumSupport.Reporting
{
    public static class VisualDiffHtmlReport
    {

        public static void Append(
            StringBuilder html,
            VisualBaselineContext ctx,
            ImageComparisonResult result)
        {
            html.AppendLine("<div class='visual-test'>");

            html.AppendLine($"<h3>{Escape(ctx.TestId)}</h3>");

            // Pass/fail badge
            string badge = result.Passed
                ? "<span class='badge pass'>PASS</span>"
                : "<span class='badge fail'>FAIL</span>";

            html.AppendLine($"<p>{badge} <b>Similarity:</b> {result.SimilarityPercent}% " +
                $"(threshold: {result.Threshold}%)</p>");
            html.AppendLine($"<p><b>Status:</b> {BaselineStatus(ctx)}</p>");

            // Metrics table
            html.AppendLine("<table class='metrics'>");
            html.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");

            if (result.SsimPercent.HasValue)
                html.AppendLine($"<tr><td>SSIM</td><td>{result.SsimPercent}%</td></tr>");
            if (result.EdgePercent.HasValue)
                html.AppendLine($"<tr><td>Edge Similarity</td><td>{result.EdgePercent}%</td></tr>");
            if (result.PixelDiffPercent.HasValue)
                html.AppendLine($"<tr><td>Pixel Diff</td><td>{result.PixelDiffPercent}% " +
                    $"({result.PixelDiffCount}/{result.TotalPixels})</td></tr>");

            html.AppendLine("</table>");

            // Reasoning
            html.AppendLine($"<p><b>Reasoning:</b> {Escape(result.Reasoning)}</p>");

            // AI reasoning
            if (!string.IsNullOrWhiteSpace(result.AiReasoning))
            {
                html.AppendLine("<div class='ai-reasoning'>");
                html.AppendLine("<h4>AI Analysis</h4>");
                html.AppendLine($"<p>{Escape(result.AiReasoning)}</p>");
                html.AppendLine("</div>");
            }

            // Image grid
            html.AppendLine("<div class='visual-grid'>");

            ImageBlock(html, "Baseline", ctx.BaselinePath);
            ImageBlock(html, "Actual", ctx.ActualPath);

            if (!string.IsNullOrWhiteSpace(result.HeatmapPath))
                ImageBlock(html, "Heatmap", result.HeatmapPath);

            if (!string.IsNullOrWhiteSpace(result.DiffImagePath))
                ImageBlock(html, "Side-by-Side Diff", result.DiffImagePath);

            html.AppendLine("</div>");
            html.AppendLine("</div>");
        }

        public static string CreateDocument(string title, Action<StringBuilder> body)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine($"<title>{Escape(title)}</title>");
            sb.AppendLine(@"
<style>
body { font-family: Arial; padding: 20px; }
.visual-test { border: 1px solid #ccc; margin-bottom: 30px; padding: 15px; }
.visual-grid { display: flex; gap: 20px; flex-wrap: wrap; }
.visual-grid img { max-width: 300px; border: 1px solid #aaa; }
.caption { text-align: center; font-size: 0.9em; }
.badge { padding: 2px 8px; border-radius: 4px; color: #fff; font-weight: bold; }
.badge.pass { background: #4caf50; }
.badge.fail { background: #f44336; }
.metrics { border-collapse: collapse; margin: 10px 0; }
.metrics td, .metrics th { border: 1px solid #ddd; padding: 4px 12px; text-align: left; }
.metrics th { background: #f5f5f5; }
.ai-reasoning { background: #f5f5f5; padding: 10px; margin: 10px 0; border-left: 3px solid #2196f3; }
.ai-reasoning h4 { margin: 0 0 5px 0; }
</style>");
            sb.AppendLine("</head><body>");

            body(sb);

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static void ImageBlock(StringBuilder sb, string caption, string path)
        {
            sb.AppendLine("<div>");
            sb.AppendLine($"<div class='caption'>{Escape(caption)}</div>");
            sb.AppendLine($"<img src='{path.Replace("\\", "/")}' />");
            sb.AppendLine("</div>");
        }

        private static string BaselineStatus(VisualBaselineContext ctx)
        {
            if (ctx.BaselineCreated) return "Baseline created";
            if (ctx.BaselineUpdated) return "Baseline updated";
            return "Baseline reused";
        }

        private static string Escape(string s) =>
            System.Net.WebUtility.HtmlEncode(s ?? "");
    }
}
