using SimpleSeleniumSupport.Image;
using SimpleSeleniumSupport.VisualBaseline;
using System;
using System.IO;
using System.Text;
using SimpleSeleniumSupport.VisualBaseline;

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

            html.AppendLine($"<h3>{ctx.TestId}</h3>");
            html.AppendLine($"<p><b>Similarity:</b> {result.SimilarityPercent}%</p>");
            html.AppendLine($"<p><b>Status:</b> {BaselineStatus(ctx)}</p>");
            html.AppendLine($"<p><b>Reasoning:</b> {Escape(result.Reasoning)}</p>");

            html.AppendLine("<div class='visual-grid'>");

            ImageBlock(html, "Baseline", ctx.BaselinePath);
            ImageBlock(html, "Actual", ctx.ActualPath);

            if (!string.IsNullOrWhiteSpace(result.HeatmapPath))
                ImageBlock(html, "Heatmap", result.HeatmapPath);

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
.visual-grid { display: flex; gap: 20px; }
.visual-grid img { max-width: 300px; border: 1px solid #aaa; }
.caption { text-align: center; font-size: 0.9em; }
</style>");
            sb.AppendLine("</head><body>");

            body(sb);

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static void ImageBlock(StringBuilder sb, string caption, string path)
        {
            sb.AppendLine("<div>");
            sb.AppendLine($"<div class='caption'>{caption}</div>");
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
