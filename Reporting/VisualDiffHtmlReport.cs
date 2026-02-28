using SimpleSeleniumSupport.Image;
using System;
using System.Text;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Builds an interactive single-page HTML visual regression report.
    /// Features: sticky summary bar, JS-generated TOC, pass/fail card borders,
    /// dark-mode toggle, lazy image loading, and click-to-zoom lightbox.
    /// </summary>
    public static class VisualDiffHtmlReport
    {
        // ── Append a single test result ──────────────────────────────────────

        public static void Append(
            StringBuilder html,
            VisualBaselineContext ctx,
            ImageComparisonResult result)
        {
            var cardClass = result.Passed ? "vt-pass" : "vt-fail";
            var safeId    = MakeSafeId(ctx.TestId);

            html.AppendLine($"<div class='vt {cardClass}' id='{safeId}' data-passed='{result.Passed.ToString().ToLower()}'>");

            // Header row
            html.AppendLine("<div class='vt-header'>");
            html.AppendLine($"  <h3 class='vt-title'>{Escape(ctx.TestId)}</h3>");
            var badge = result.Passed
                ? "<span class='badge bg-success'>PASS</span>"
                : "<span class='badge bg-danger'>FAIL</span>";
            html.AppendLine($"  {badge}");
            html.AppendLine("</div>");

            // Similarity line
            var simColor = result.Passed ? "#198754" : (result.SimilarityPercent >= 80 ? "#fd7e14" : "#dc3545");
            html.AppendLine("<div class='vt-sim-row'>");
            html.AppendLine($"  <div class='sim-bar-outer'><div class='sim-bar-inner' style='width:{result.SimilarityPercent}%;background:{simColor}'></div></div>");
            html.AppendLine($"  <span class='sim-pct'>{result.SimilarityPercent:F1}%</span>");
            html.AppendLine($"  <span class='sim-meta text-muted'>(threshold {result.Threshold}%  ·  {Escape(BaselineStatus(ctx))})</span>");
            html.AppendLine("</div>");

            // Metrics table
            html.AppendLine("<table class='vt-metrics'>");
            html.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
            if (result.SsimPercent.HasValue)
                html.AppendLine($"<tr><td>SSIM</td><td>{result.SsimPercent:F2}%</td></tr>");
            if (result.EdgePercent.HasValue)
                html.AppendLine($"<tr><td>Edge Similarity</td><td>{result.EdgePercent:F2}%</td></tr>");
            if (result.PixelDiffPercent.HasValue)
                html.AppendLine($"<tr><td>Pixel Diff</td><td>{result.PixelDiffPercent:F2}% ({result.PixelDiffCount}/{result.TotalPixels} px)</td></tr>");
            html.AppendLine("</table>");

            // Reasoning
            if (!string.IsNullOrWhiteSpace(result.Reasoning))
                html.AppendLine($"<p class='vt-reasoning'><b>Reasoning:</b> {Escape(result.Reasoning)}</p>");

            // AI reasoning
            if (!string.IsNullOrWhiteSpace(result.AiReasoning))
            {
                html.AppendLine("<div class='vt-ai'>");
                html.AppendLine("  <div class='vt-ai-label'>AI Analysis</div>");
                html.AppendLine($"  <p class='mb-0'>{Escape(result.AiReasoning)}</p>");
                html.AppendLine("</div>");
            }

            // Image grid
            html.AppendLine("<div class='vt-imgs'>");
            ImageBlock(html, "Baseline",       ctx.BaselinePath);
            ImageBlock(html, "Actual",         ctx.ActualPath);
            if (!string.IsNullOrWhiteSpace(result.HeatmapPath))
                ImageBlock(html, "Heatmap",    result.HeatmapPath);
            if (!string.IsNullOrWhiteSpace(result.DiffImagePath))
                ImageBlock(html, "Side-by-Side Diff", result.DiffImagePath);
            html.AppendLine("</div>");

            html.AppendLine("</div>"); // .vt
        }

        // ── Wrap in a full document ──────────────────────────────────────────

        public static string CreateDocument(string title, Action<StringBuilder> body)
        {
            var content = new StringBuilder();
            body(content);

            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine($"  <meta charset='utf-8'/>");
            sb.AppendLine("  <meta name='viewport' content='width=device-width,initial-scale=1'/>");
            sb.AppendLine($"  <title>{Escape(title)}</title>");
            sb.AppendLine(Styles);
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Layout: sidebar TOC + main content
            sb.AppendLine("<div id='layout'>");
            sb.AppendLine("  <nav id='toc'><div id='toc-inner'><h5>Tests</h5><ul id='toc-list'></ul></div></nav>");
            sb.AppendLine("  <div id='content'>");

            // Sticky summary bar
            sb.AppendLine("  <div id='sumbar'>");
            sb.AppendLine($"    <div id='sumbar-title'>{Escape(title)}</div>");
            sb.AppendLine("    <div id='sumbar-stats'></div>");
            sb.AppendLine("    <div style='margin-left:auto;display:flex;gap:8px;align-items:center'>");
            sb.AppendLine("      <input id='toc-search' class='form-control form-control-sm' placeholder='Filter tests…' style='width:180px'/>");
            sb.AppendLine("      <button id='dark-btn' title='Toggle dark mode'>🌙</button>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");

            sb.AppendLine("  <div id='tests'>");
            sb.Append(content);
            sb.AppendLine("  </div>");
            sb.AppendLine("  </div>"); // #content
            sb.AppendLine("</div>"); // #layout

            // Lightbox overlay
            sb.AppendLine("<div id='lb' onclick=\"if(this===event.target)closeLb()\">");
            sb.AppendLine("  <div id='lb-box'>");
            sb.AppendLine("    <div id='lb-bar'><span id='lb-cap'></span><button id='lb-x' onclick='closeLb()'>×</button></div>");
            sb.AppendLine("    <img id='lb-img' src='' alt=''/>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            sb.AppendLine(Script);
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void ImageBlock(StringBuilder sb, string caption, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var src = path.Replace("\\", "/");
            sb.AppendLine("<div class='vt-img-wrap'>");
            sb.AppendLine($"  <div class='vt-img-cap'>{Escape(caption)}</div>");
            sb.AppendLine($"  <img src='{src}' class='vt-img' alt='{Escape(caption)}' loading='lazy' onclick='openLb(this)'/>");
            sb.AppendLine("</div>");
        }

        private static string BaselineStatus(VisualBaselineContext ctx)
        {
            if (ctx.BaselineCreated) return "Baseline created";
            if (ctx.BaselineUpdated) return "Baseline updated";
            return "Baseline reused";
        }

        private static string MakeSafeId(string s)
        {
            if (string.IsNullOrEmpty(s)) return "test-" + Guid.NewGuid().ToString("N")[..8];
            var sb = new StringBuilder();
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            return "vt-" + sb.ToString().Trim('-');
        }

        private static string Escape(string s) =>
            System.Net.WebUtility.HtmlEncode(s ?? "");

        // ── Inline CSS ───────────────────────────────────────────────────────

        private const string Styles = @"
<link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' crossorigin='anonymous'/>
<style>
:root{
  --bg:#f5f6f8;--card:#fff;--border:#e2e8f0;--text:#1e293b;--muted:#64748b;
  --pass-border:#198754;--fail-border:#dc3545;--pass-bg:#f0fdf4;--fail-bg:#fff5f5;
  --toc-bg:#1e293b;--toc-text:#cbd5e1;--sumbar-bg:#fff;
  --sim-track:#e2e8f0;
}
body.dark{
  --bg:#0f172a;--card:#1e293b;--border:#334155;--text:#e2e8f0;--muted:#94a3b8;
  --pass-bg:#052e16;--fail-bg:#450a0a;--toc-bg:#0f172a;--toc-text:#94a3b8;
  --sumbar-bg:#1e293b;--sim-track:#334155;
}
*{box-sizing:border-box}
body{margin:0;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;background:var(--bg);color:var(--text);transition:background .2s,color .2s}
#layout{display:flex;min-height:100vh}
/* Sidebar TOC */
#toc{width:220px;flex-shrink:0;background:var(--toc-bg);position:sticky;top:0;height:100vh;overflow-y:auto;z-index:50}
#toc-inner{padding:16px 12px}
#toc h5{color:#f1f5f9;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.6px;margin:0 0 10px}
#toc-list{list-style:none;padding:0;margin:0}
#toc-list li a{display:block;padding:4px 8px;border-radius:5px;color:var(--toc-text);text-decoration:none;font-size:12px;transition:.1s;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#toc-list li a:hover{background:#334155;color:#f1f5f9}
#toc-list li.pass a::before{content:'✅ ';font-size:10px}
#toc-list li.fail a::before{content:'❌ ';font-size:10px}
/* Main content */
#content{flex:1;min-width:0;display:flex;flex-direction:column}
/* Sticky summary bar */
#sumbar{background:var(--sumbar-bg);border-bottom:1px solid var(--border);padding:8px 20px;display:flex;align-items:center;gap:14px;position:sticky;top:0;z-index:40;flex-wrap:wrap}
#sumbar-title{font-weight:700;font-size:15px}
#sumbar-stats{display:flex;gap:10px;flex-wrap:wrap}
.sum-badge{padding:3px 12px;border-radius:20px;font-size:12px;font-weight:600}
.sum-total{background:#e2e8f0;color:#1e293b}
.sum-pass{background:#d1fae5;color:#065f46}
.sum-fail{background:#fee2e2;color:#991b1b}
#dark-btn{background:none;border:1px solid var(--border);border-radius:6px;padding:3px 8px;cursor:pointer;font-size:14px;color:var(--text)}
/* Tests area */
#tests{padding:20px;display:flex;flex-direction:column;gap:24px}
/* Test card */
.vt{background:var(--card);border:2px solid var(--border);border-radius:10px;padding:16px 18px;transition:border-color .2s,background .2s}
.vt.vt-pass{border-color:var(--pass-border);background:var(--pass-bg)}
.vt.vt-fail{border-color:var(--fail-border);background:var(--fail-bg)}
.vt-header{display:flex;align-items:center;gap:10px;margin-bottom:10px}
.vt-title{font-size:15px;font-weight:700;margin:0;color:var(--text)}
.vt-sim-row{display:flex;align-items:center;gap:10px;margin-bottom:10px}
.sim-bar-outer{flex:0 0 160px;background:var(--sim-track);border-radius:4px;height:14px;overflow:hidden}
.sim-bar-inner{height:100%;border-radius:4px;transition:width .4s}
.sim-pct{font-weight:700;min-width:48px}
.sim-meta{font-size:12px}
.vt-metrics{border-collapse:collapse;margin:8px 0;font-size:13px}
.vt-metrics td,.vt-metrics th{border:1px solid var(--border);padding:4px 12px;text-align:left}
.vt-metrics th{background:var(--sim-track);font-size:11px;text-transform:uppercase;letter-spacing:.3px;color:var(--muted)}
.vt-reasoning{font-size:13px;color:var(--muted);margin:6px 0}
.vt-ai{background:#eff6ff;border-left:3px solid #3b82f6;border-radius:4px;padding:8px 12px;margin:8px 0;font-size:13px}
body.dark .vt-ai{background:#1e3a5f}
.vt-ai-label{font-weight:700;font-size:11px;text-transform:uppercase;letter-spacing:.4px;color:#3b82f6;margin-bottom:4px}
/* Image grid */
.vt-imgs{display:flex;gap:16px;flex-wrap:wrap;margin-top:12px}
.vt-img-wrap{display:flex;flex-direction:column;align-items:center}
.vt-img-cap{font-size:11px;font-weight:600;color:var(--muted);margin-bottom:4px;text-transform:uppercase;letter-spacing:.3px}
.vt-img{max-width:260px;max-height:180px;object-fit:contain;border:1px solid var(--border);border-radius:4px;cursor:zoom-in;transition:box-shadow .15s}
.vt-img:hover{box-shadow:0 2px 12px rgba(0,0,0,.18)}
/* Lightbox */
#lb{display:none;position:fixed;inset:0;background:rgba(0,0,0,.88);z-index:2000;align-items:center;justify-content:center;flex-direction:column}
#lb.open{display:flex}
#lb-box{display:flex;flex-direction:column;max-width:95vw;max-height:95vh;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 8px 40px rgba(0,0,0,.5)}
body.dark #lb-box{background:#1e293b}
#lb-bar{display:flex;align-items:center;padding:8px 14px;background:#f1f5f9;border-bottom:1px solid #e2e8f0;gap:10px}
body.dark #lb-bar{background:#0f172a;border-color:#334155}
#lb-cap{flex:1;font-size:13px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#lb-x{background:none;border:none;font-size:22px;cursor:pointer;color:#64748b;padding:0 4px;line-height:1}
#lb-img{max-width:90vw;max-height:85vh;object-fit:contain;display:block}
/* Search hidden cards */
.vt.hidden-by-search{display:none}
</style>
";

        // ── Inline JS ────────────────────────────────────────────────────────

        private const string Script = @"
<script>
(function(){
  // ── TOC ─────────────────────────────────────────────────────────────────
  const tocList = document.getElementById('toc-list');
  document.querySelectorAll('.vt').forEach(card=>{
    const h3   = card.querySelector('.vt-title');
    const text = h3 ? h3.textContent.trim() : card.id;
    const li   = document.createElement('li');
    li.classList.add(card.classList.contains('vt-pass')?'pass':'fail');
    const a  = document.createElement('a');
    a.href   = '#'+card.id;
    a.title  = text;
    a.textContent = text;
    a.addEventListener('click',e=>{e.preventDefault();card.scrollIntoView({behavior:'smooth',block:'start'});});
    li.appendChild(a);
    tocList.appendChild(li);
  });

  // ── Summary stats ────────────────────────────────────────────────────────
  const cards = [...document.querySelectorAll('.vt[data-passed]')];
  const total  = cards.length;
  const passed = cards.filter(c=>c.dataset.passed==='true').length;
  const failed = total - passed;
  const sb = document.getElementById('sumbar-stats');
  sb.innerHTML = `
    <span class='sum-badge sum-total'>Total: ${total}</span>
    <span class='sum-badge sum-pass'>Pass: ${passed}</span>
    <span class='sum-badge sum-fail'>Fail: ${failed}</span>
  `;

  // ── Dark mode ────────────────────────────────────────────────────────────
  const darkBtn = document.getElementById('dark-btn');
  let dark = false;
  darkBtn.addEventListener('click',()=>{
    dark=!dark;
    document.body.classList.toggle('dark',dark);
    darkBtn.textContent = dark ? '☀️' : '🌙';
  });

  // ── TOC search ───────────────────────────────────────────────────────────
  document.getElementById('toc-search').addEventListener('input',function(){
    const q=this.value.toLowerCase();
    document.querySelectorAll('.vt').forEach(card=>{
      const title=(card.querySelector('.vt-title')||{}).textContent||'';
      card.classList.toggle('hidden-by-search', q!=='' && !title.toLowerCase().includes(q));
    });
    [...tocList.querySelectorAll('li')].forEach(li=>{
      const t=(li.querySelector('a')||{}).textContent||'';
      li.style.display = q===''||t.toLowerCase().includes(q) ? '' : 'none';
    });
  });

  // ── Lightbox ─────────────────────────────────────────────────────────────
  const lb    = document.getElementById('lb');
  const lbImg = document.getElementById('lb-img');
  const lbCap = document.getElementById('lb-cap');

  window.openLb = function(imgEl){
    lbImg.src = imgEl.src;
    lbCap.textContent = imgEl.alt || '';
    lb.classList.add('open');
  };
  window.closeLb = function(){ lb.classList.remove('open'); };

  document.addEventListener('keydown',e=>{
    if(e.key==='Escape') closeLb();
  });
})();
</script>
";
    }
}
