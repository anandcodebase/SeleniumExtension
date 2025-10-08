using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Export network info as chunked JSON files and an index.html client that fetches pages via XHR (fetch).
    /// Ensures each export is written into a unique subfolder to avoid overwriting previous reports.
    /// </summary>
    public static class HtmlExporterChunked
    {
        /// <summary>
        /// Exports the rows into a unique report folder inside outFolderRoot.
        /// Returns the path to the created report folder.
        /// </summary>
        /// <param name="items">rows to export</param>
        /// <param name="outFolderRoot">root folder under which a unique report folder will be created</param>
        /// <param name="pageSize">rows per chunk/page</param>
        /// <param name="maxFieldLength">truncate displayed fields (0 or negative to disable)</param>
        /// <param name="emitNdjson">emit NDJSON file 'all.ndjson' for programmatic reads</param>
        /// <param name="reportName">optional friendly report name to include in folder name</param>
        /// <returns>absolute path to the created report folder</returns>
        public static string ExportNetworkInfoToChunkedHtml(
            IEnumerable<FullNetworkInfo> items,
            string outFolderRoot,
            int pageSize = 250,
            int maxFieldLength = 2000,
            bool emitNdjson = false,
            string reportName = null)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot)) throw new ArgumentNullException(nameof(outFolderRoot));

            // Ensure root exists
            Directory.CreateDirectory(outFolderRoot);

            // Build a safe folder name with timestamp and optional reportName
            var safeName = string.IsNullOrWhiteSpace(reportName) ? "report" : MakeSafeFileName(reportName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var baseFolderName = $"{safeName}-{timestamp}";
            var reportFolder = GetUniqueFolderPath(outFolderRoot, baseFolderName);

            Directory.CreateDirectory(reportFolder);

            // Convert items to simplified serializable rows
            var rows = (items ?? Enumerable.Empty<FullNetworkInfo>()).Select(e => new
            {
                RequestId = e.RequestId ?? "",
                RequestUrl = e.RequestUrl ?? "",
                RequestMethod = e.RequestMethod ?? "",
                RequestTimestamp = e.RequestTimestamp?.ToString("o") ?? "",
                ResponseTimestamp = e.ResponseTimestamp?.ToString("o") ?? "",
                LatencyMs = e.LatencyMs ?? (e.RequestTimestamp.HasValue && e.ResponseTimestamp.HasValue ? (long?)(e.ResponseTimestamp.Value - e.RequestTimestamp.Value).TotalMilliseconds : null),
                Status = e.ResponseStatusCode,
                RequestHeaders = DictToString(e.RequestHeaders),
                ResponseHeaders = DictToString(e.ResponseHeaders),
                RequestBody = e.RequestPostData ?? "",
                ResponseBody = e.ResponseBody ?? "",
                ResourceType = e.ResponseResourceType ?? ""
            }).ToList();

            int total = rows.Count;
            int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

            // create pages folder inside report folder
            var pagesFolder = Path.Combine(reportFolder, "pages");
            Directory.CreateDirectory(pagesFolder);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            // write each page as page-{n}.json
            for (int p = 0; p < pageCount; p++)
            {
                var chunk = rows.Skip(p * pageSize).Take(pageSize).ToList();
                string pagePath = Path.Combine(pagesFolder, $"page-{p + 1}.json");
                var json = JsonSerializer.Serialize(chunk, jsonOptions);
                File.WriteAllText(pagePath, json, Encoding.UTF8);
            }

            // write manifest (includes report metadata)
            var manifest = new
            {
                reportName = reportName ?? safeName,
                reportFolder = Path.GetFileName(reportFolder),
                totalRows = total,
                pageSize = pageSize,
                pageCount = pageCount,
                generatedUtc = DateTime.UtcNow.ToString("o")
            };

            File.WriteAllText(Path.Combine(reportFolder, "manifest.json"), JsonSerializer.Serialize(manifest, jsonOptions), Encoding.UTF8);

            // optional NDJSON (all rows newline-delimited)
            if (emitNdjson)
            {
                var ndPath = Path.Combine(reportFolder, "all.ndjson");
                using (var sw = new StreamWriter(ndPath, false, Encoding.UTF8))
                {
                    foreach (var row in rows)
                    {
                        sw.WriteLine(JsonSerializer.Serialize(row, jsonOptions));
                    }
                }
            }

            // write index.html client (will fetch pages from 'pages' subfolder)
            string html = BuildClientHtml(pageCount, pageSize, maxFieldLength);
            File.WriteAllText(Path.Combine(reportFolder, "index.html"), html, Encoding.UTF8);

            return Path.GetFullPath(reportFolder);
        }

        private static string DictToString(IDictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return "";
            return string.Join("\n", dict.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        /// <summary>
        /// Create a unique folder path under root using baseName. If folder exists, append -1, -2, ...
        /// </summary>
        private static string GetUniqueFolderPath(string root, string baseName)
        {
            string candidate = Path.Combine(root, baseName);
            if (!Directory.Exists(candidate)) return candidate;

            // If it exists, try with numeric suffixes
            for (int i = 1; i < 1000; i++)
            {
                var alt = Path.Combine(root, $"{baseName}-{i}");
                if (!Directory.Exists(alt)) return alt;
            }

            // fallback to GUID
            return Path.Combine(root, $"{baseName}-{Guid.NewGuid():N}");
        }

        private static string MakeSafeFileName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "report";
            foreach (var c in Path.GetInvalidFileNameChars()) input = input.Replace(c, '-');
            // replace spaces with dashes, collapse multiple dashes
            var s = string.Join("-", input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            while (s.Contains("--")) s = s.Replace("--", "-");
            return s.Trim('-');
        }

        // Builds the client HTML (same as prior implementation but embedded to fetch pages from local pages/)
        private static string BuildClientHtml(int pageCount, int pageSize, int maxFieldLength)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset='utf-8'/>");
            sb.AppendLine("  <meta name='viewport' content='width=device-width,initial-scale=1'/>");
            sb.AppendLine("  <title>Network Export (chunked)</title>");
            sb.AppendLine("  <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' crossorigin='anonymous'>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body{background:#f8f9fa}");
            sb.AppendLine("    .table-responsive{max-height:60vh;}");
            sb.AppendLine("    td{vertical-align:top;white-space:pre-wrap;word-wrap:break-word;max-width:400px;overflow:hidden;text-overflow:ellipsis}");
            sb.AppendLine("    thead th{position:sticky;top:0;background:white}");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class='container py-3'>");
            sb.AppendLine("    <div class='d-flex mb-2 align-items-center'>");
            sb.AppendLine("      <h4 class='me-3'>Network Export</h4>");
            sb.AppendLine("      <div id='summary' class='text-muted small'>Loading manifest…</div>");
            sb.AppendLine("      <div class='ms-auto d-flex gap-2 align-items-center'>");
            sb.AppendLine("        <input id='globalSearch' class='form-control form-control-sm' placeholder='Global search...' style='min-width:200px'/>");
            sb.AppendLine("        <select id='statusFilter' class='form-select form-select-sm' style='width:140px'><option value=''>All statuses</option></select>");
            sb.AppendLine("        <button id='downloadPage' class='btn btn-sm btn-outline-primary'>Download page JSON</button>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("");
            sb.AppendLine("    <div class='table-responsive border rounded bg-white'>");
            sb.AppendLine("      <table class='table table-sm mb-0'>");
            sb.AppendLine("        <thead class='table-light'><tr>");
            sb.AppendLine("          <th>RequestUrl</th><th>Method</th><th>ReqTime</th><th>RespTime</th><th>Latency</th><th>Status</th><th>Resource</th><th>RequestHeaders</th><th>RequestBody</th><th>ResponseHeaders</th><th>ResponseBody</th>");
            sb.AppendLine("        </tr></thead>");
            sb.AppendLine("        <tbody id='gridBody'></tbody>");
            sb.AppendLine("      </table>");
            sb.AppendLine("    </div>");
            sb.AppendLine("");
            sb.AppendLine("    <nav class='d-flex justify-content-between align-items-center mt-2' aria-label='Pagination'>");
            sb.AppendLine("      <div><small id='pageInfo'>Page 0</small></div>");
            sb.AppendLine("      <ul class='pagination pagination-sm mb-0' id='pager'></ul>");
            sb.AppendLine("    </nav>");
            sb.AppendLine("  </div>");
            sb.AppendLine("");
            sb.AppendLine("  <div class='modal fade' id='fullModal' tabindex='-1'><div class='modal-dialog modal-xl'><div class='modal-content'><div class='modal-header'><h5 class='modal-title'>Full content</h5><button type='button' class='btn-close' data-bs-dismiss='modal' aria-label='Close'></button></div><div class='modal-body'><pre id='modalPre'></pre></div><div class='modal-footer'><button class='btn btn-secondary' data-bs-dismiss='modal'>Close</button></div></div></div></div>");
            sb.AppendLine("");
            sb.AppendLine("  <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js' crossorigin='anonymous'></script>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    const manifestUrl = 'manifest.json';");
            sb.AppendLine($"    const pageCount = {pageCount};");
            sb.AppendLine($"    const pageSize = {pageSize};");
            sb.AppendLine($"    const maxFieldLength = {maxFieldLength};");
            sb.AppendLine("");
            sb.AppendLine("    let currentPage = 1;");
            sb.AppendLine("    let currentRows = []; // rows of current page");
            sb.AppendLine("");
            sb.AppendLine("    function escapeHtml(s){ if(s==null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;'); }");
            sb.AppendLine("    function truncate(s){ if(!s) return ''; if(!maxFieldLength || maxFieldLength<=0) return s; return s.length>maxFieldLength ? s.slice(0,maxFieldLength)+'... (truncated)' : s; }");
            sb.AppendLine("");
            sb.AppendLine("    function renderRow(r, idx){");
            sb.AppendLine("      function cell(full){ return `<td>${escapeHtml(truncate(full))}${full && full.length>maxFieldLength?`<div><a href='#' class='view-full' data-full='${encodeURIComponent(full)}'>View</a></div>`:''}</td>`; }");
            sb.AppendLine("      return `<tr>` +");
            sb.AppendLine("        `<td style='max-width:300px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis'>${escapeHtml(r.RequestUrl||'')}</td>` +");
            sb.AppendLine("        `<td>${escapeHtml(r.RequestMethod||'')}</td>` +");
            sb.AppendLine("        `<td>${escapeHtml(r.RequestTimestamp||'')}</td>` +");
            sb.AppendLine("        `<td>${escapeHtml(r.ResponseTimestamp||'')}</td>` +");
            sb.AppendLine("        `<td>${escapeHtml(r.LatencyMs==null?'':r.LatencyMs)}</td>` +");
            sb.AppendLine("        `<td>${escapeHtml(r.Status)}</td>` +");
            sb.AppendLine("        `<td>${escapeHtml(r.ResourceType||'')}</td>` +");
            sb.AppendLine("        cell(r.RequestHeaders) + cell(r.RequestBody) + cell(r.ResponseHeaders) + cell(r.ResponseBody) +");
            sb.AppendLine("      `</tr>`;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    async function fetchPage(n){");
            sb.AppendLine("      const url = `pages/page-${n}.json`;");
            sb.AppendLine("      const res = await fetch(url);");
            sb.AppendLine("      if(!res.ok) throw new Error('Failed to fetch page ' + n + ': ' + res.status);");
            sb.AppendLine("      const json = await res.json();");
            sb.AppendLine("      return json;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    async function gotoPage(n){");
            sb.AppendLine("      if(n < 1) n = 1; if(n > pageCount) n = pageCount;");
            sb.AppendLine("      try{");
            sb.AppendLine("        const rows = await fetchPage(n);");
            sb.AppendLine("        currentPage = n; currentRows = rows;");
            sb.AppendLine("        render(); populatePageInfo();");
            sb.AppendLine("      }catch(e){ alert('Failed to load page: ' + e.message); }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function render(){");
            sb.AppendLine("      const tbody = document.getElementById('gridBody');");
            sb.AppendLine("      const q = (document.getElementById('globalSearch').value || '').toLowerCase();");
            sb.AppendLine("      const statusFilter = document.getElementById('statusFilter').value || '';");
            sb.AppendLine("      const rows = currentRows.filter(r => {");
            sb.AppendLine("        if(statusFilter && String(r.Status) !== String(statusFilter)) return false;");
            sb.AppendLine("        if(!q) return true;");
            sb.AppendLine("        const hay = ((r.RequestUrl||'')+' '+(r.RequestHeaders||'')+' '+(r.ResponseHeaders||'')+' '+(r.RequestBody||'')+' '+(r.ResponseBody||'')).toLowerCase();");
            sb.AppendLine("        return hay.indexOf(q) !== -1;");
            sb.AppendLine("      });");
            sb.AppendLine("      tbody.innerHTML = rows.map((r,i) => renderRow(r,i)).join('');");
            sb.AppendLine("      // wire modal view links");
            sb.AppendLine("      [...tbody.querySelectorAll('a.view-full')].forEach(a => a.addEventListener('click', ev => { ev.preventDefault(); const full = decodeURIComponent(a.getAttribute('data-full')||''); document.getElementById('modalPre').textContent = full; var bs = new bootstrap.Modal(document.getElementById('fullModal')); bs.show(); }));");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function populatePageInfo(){ document.getElementById('pageInfo').textContent = `Page ${currentPage} of ${pageCount}`; renderPager(); }");
            sb.AppendLine("    function renderPager(){ const pager = document.getElementById('pager'); pager.innerHTML=''; function pageItem(label, target, disabled){ const li=document.createElement('li'); li.className='page-item'+(disabled?' disabled':''); const a=document.createElement('a'); a.className='page-link'; a.href='#'; a.textContent=label; a.addEventListener('click', ev => { ev.preventDefault(); if(!disabled) gotoPage(target);}); li.appendChild(a); pager.appendChild(li);} pageItem('«',1,currentPage===1); pageItem('<', currentPage-1, currentPage===1); for(let p=Math.max(1,currentPage-2); p<=Math.min(pageCount, currentPage+2); p++){ pageItem(String(p), p, false);} pageItem('>', currentPage+1, currentPage===pageCount); pageItem('»', pageCount, currentPage===pageCount); }");
            sb.AppendLine("");
            sb.AppendLine("    document.getElementById('globalSearch').addEventListener('input', () => render());");
            sb.AppendLine("    document.getElementById('statusFilter').addEventListener('change', () => render());");
            sb.AppendLine("    document.getElementById('downloadPage').addEventListener('click', ()=> { const blob = new Blob([JSON.stringify(currentRows, null, 2)], {type:'application/json'}); const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = `page-${currentPage}.json`; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url); });");
            sb.AppendLine("");
            sb.AppendLine("    async function init(){");
            sb.AppendLine("      try{");
            sb.AppendLine("        const man = await fetch(manifestUrl).then(r=>r.json());");
            sb.AppendLine("        document.getElementById('summary').textContent = `${man.totalRows} rows — pageSize ${man.pageSize} — generated ${man.generated || ''}`;");
            sb.AppendLine("        await gotoPage(1);");
            sb.AppendLine("        populateStatusFilter();");
            sb.AppendLine("      }catch(e){ console.error(e); document.getElementById('summary').textContent = 'Failed to load manifest'; }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function populateStatusFilter(){ const el = document.getElementById('statusFilter'); const vals = Array.from(new Set(currentRows.map(r=>String(r.Status||'')).filter(x=>x && x.length>0))).sort(); el.innerHTML = '<option value=\"\">All statuses</option>' + vals.map(v=>`<option value='${v}'>${v}</option>`).join(''); }");
            sb.AppendLine("");
            sb.AppendLine("    // start");
            sb.AppendLine("    init();");
            sb.AppendLine("  </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
