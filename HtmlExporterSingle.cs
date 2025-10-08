using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Exports a single self-contained HTML report file containing all data embedded as JSON.
    /// Produces a unique filename under the provided folder to avoid overwrites.
    /// </summary>
    public static class HtmlExporterSingle
    {
        /// <summary>
        /// Writes the self-contained HTML report file and returns its full path.
        /// </summary>
        /// <param name="items">Network rows to embed</param>
        /// <param name="outFolderRoot">Folder where the HTML file will be placed (created if missing)</param>
        /// <param name="reportName">Optional friendly name included in filename</param>
        /// <param name="pageSize">Default rows per page in UI</param>
        /// <param name="maxFieldLength">Truncate large fields for table display (0 or negative to disable)</param>
        /// <returns>Absolute path to the written HTML file</returns>
        public static string ExportNetworkInfoToSingleHtml(
            IEnumerable<FullNetworkInfo> items,
            string outFolderRoot,
            string reportName = null,
            int pageSize = 50,
            int maxFieldLength = 2000)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot)) throw new ArgumentNullException(nameof(outFolderRoot));
            Directory.CreateDirectory(outFolderRoot);

            var safeName = string.IsNullOrWhiteSpace(reportName) ? "report" : MakeSafeFileName(reportName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var baseName = $"{safeName}-{timestamp}";
            var filePath = GetUniqueFilePath(outFolderRoot, baseName, ".html");

            // Convert rows to serializable objects
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

            // Serialize JSON (compact). Use UnsafeRelaxedJsonEscaping and then escape closing script tags.
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string dataJson = JsonSerializer.Serialize(rows, jsonOptions);
            // Post-escape to avoid ending the <script> early
            dataJson = dataJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

            // Build html
            var html = BuildHtml(dataJson, Path.GetFileName(filePath), pageSize, maxFieldLength, rows.Count);

            File.WriteAllText(filePath, html, new UTF8Encoding(false));

            return Path.GetFullPath(filePath);
        }

        private static string DictToString(IDictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return "";
            return string.Join("\n", dict.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        private static string MakeSafeFileName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "report";
            foreach (var c in Path.GetInvalidFileNameChars()) input = input.Replace(c, '-');
            var s = string.Join("-", input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            while (s.Contains("--")) s = s.Replace("--", "-");
            return s.Trim('-');
        }

        private static string GetUniqueFilePath(string folder, string baseName, string ext)
        {
            string candidate = Path.Combine(folder, baseName + ext);
            if (!File.Exists(candidate)) return candidate;

            for (int i = 1; i < 1000; i++)
            {
                var alt = Path.Combine(folder, $"{baseName}-{i}{ext}");
                if (!File.Exists(alt)) return alt;
            }

            // fallback
            return Path.Combine(folder, $"{baseName}-{Guid.NewGuid():N}{ext}");
        }

        private static string BuildHtml(string dataJson, string filename, int pageSize, int maxFieldLength, int totalRows)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset='utf-8' />");
            sb.AppendLine("  <meta name='viewport' content='width=device-width,initial-scale=1' />");
            sb.AppendLine($"  <title>Network Report — {System.Net.WebUtility.HtmlEncode(filename)}</title>");
            sb.AppendLine("  <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' crossorigin='anonymous'>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body{background:#f8f9fa}");
            sb.AppendLine("    .container{max-width:1400px;margin:16px auto}");
            sb.AppendLine("    td{vertical-align:top;white-space:pre-wrap;word-wrap:break-word;max-width:420px;overflow:hidden;text-overflow:ellipsis}");
            sb.AppendLine("    thead th{position:sticky;top:0;background:white;z-index:5}");
            sb.AppendLine("    .small-muted{font-size:0.9rem;color:#666}");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class='container'>");
            sb.AppendLine("    <div class='d-flex align-items-center mb-3'>");
            sb.AppendLine("      <h4 class='me-3'>Network Report</h4>");
            sb.AppendLine($"      <div id='summary' class='small-muted'>Rows: {totalRows} — generated {DateTime.UtcNow.ToString("u")}</div>");
            sb.AppendLine("      <div class='ms-auto d-flex gap-2 align-items-center'>");
            sb.AppendLine("        <input id='globalSearch' class='form-control form-control-sm' placeholder='Global search...' style='min-width:240px'/>");
            sb.AppendLine("        <select id='pageSizeSelect' class='form-select form-select-sm' style='width:120px'></select>");
            sb.AppendLine("        <select id='statusFilter' class='form-select form-select-sm' style='width:140px'><option value=''>All statuses</option></select>");
            sb.AppendLine("        <select id='methodFilter' class='form-select form-select-sm' style='width:120px'><option value=''>All methods</option></select>");
            sb.AppendLine("        <select id='resourceFilter' class='form-select form-select-sm' style='width:140px'><option value=''>All resources</option></select>");
            sb.AppendLine("        <button id='resetFilters' class='btn btn-sm btn-outline-secondary'>Reset</button>");
            sb.AppendLine("        <button id='downloadJson' class='btn btn-sm btn-outline-primary'>Download JSON</button>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <div class='table-responsive' style='max-height:60vh;overflow:auto;border:1px solid #e9ecef;border-radius:.25rem;background:white'>");
            sb.AppendLine("      <table class='table table-sm mb-0'>");
            sb.AppendLine("        <thead class='table-light'>");
            sb.AppendLine("          <tr>");
            sb.AppendLine("            <th>RequestUrl</th>");
            sb.AppendLine("            <th>Method</th>");
            sb.AppendLine("            <th>Req Time (UTC)</th>");
            sb.AppendLine("            <th>Resp Time (UTC)</th>");
            sb.AppendLine("            <th>Latency</th>");
            sb.AppendLine("            <th>Status</th>");
            sb.AppendLine("            <th>Resource</th>");
            sb.AppendLine("            <th>RequestHeaders</th>");
            sb.AppendLine("            <th>RequestBody</th>");
            sb.AppendLine("            <th>ResponseHeaders</th>");
            sb.AppendLine("            <th>ResponseBody</th>");
            sb.AppendLine("          </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody id='gridBody'></tbody>");
            sb.AppendLine("      </table>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <nav class='d-flex justify-content-between align-items-center mt-2' aria-label='Pagination'>");
            sb.AppendLine("      <div class='small' id='pageInfo'>Page 0</div>");
            sb.AppendLine("      <ul class='pagination pagination-sm mb-0' id='pager'></ul>");
            sb.AppendLine("    </nav>");
            sb.AppendLine("  </div>");

            // Modal
            sb.AppendLine("  <div class='modal fade' id='fullModal' tabindex='-1'><div class='modal-dialog modal-xl'><div class='modal-content'><div class='modal-header'><h5 class='modal-title'>Full content</h5><button type='button' class='btn-close' data-bs-dismiss='modal' aria-label='Close'></button></div><div class='modal-body'><pre id='modalPre' class='mb-0'></pre></div><div class='modal-footer'><button class='btn btn-secondary' data-bs-dismiss='modal'>Close</button></div></div></div></div>");

            // Embedded data
            sb.AppendLine("  <script>");
            sb.AppendLine($"    window.__ssa_data = {dataJson};");
            sb.AppendLine($"    window.__ssa_pageSizeDefault = {pageSize};");
            sb.AppendLine($"    window.__ssa_maxFieldLength = {maxFieldLength};");
            sb.AppendLine("  </script>");

            // Client JS (fetchless — uses embedded data)
            sb.AppendLine("  <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js' crossorigin='anonymous'></script>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    (function(){");
            sb.AppendLine("      const data = window.__ssa_data || [];");
            sb.AppendLine("      const pageSizeSelect = document.getElementById('pageSizeSelect');");
            sb.AppendLine("      const pageInfo = document.getElementById('pageInfo');");
            sb.AppendLine("      const gridBody = document.getElementById('gridBody');");
            sb.AppendLine("      const globalSearch = document.getElementById('globalSearch');");
            sb.AppendLine("      const statusFilter = document.getElementById('statusFilter');");
            sb.AppendLine("      const methodFilter = document.getElementById('methodFilter');");
            sb.AppendLine("      const resourceFilter = document.getElementById('resourceFilter');");
            sb.AppendLine("      const resetFilters = document.getElementById('resetFilters');");
            sb.AppendLine("      const downloadJson = document.getElementById('downloadJson');");
            sb.AppendLine("      const modalPre = document.getElementById('modalPre');");
            sb.AppendLine("      const fullModalEl = document.getElementById('fullModal');");
            sb.AppendLine("      const bsModal = fullModalEl ? new bootstrap.Modal(fullModalEl) : null;");
            sb.AppendLine("");
            sb.AppendLine("      let pageSize = window.__ssa_pageSizeDefault || 50;");
            sb.AppendLine("      let page = 1;");
            sb.AppendLine("      let filtered = data.slice();");
            sb.AppendLine("");
            sb.AppendLine("      // page size select");
            sb.AppendLine("      [10,25,50,100,250].forEach(n => { var opt = document.createElement('option'); opt.value = n; opt.text = n + ' / page'; pageSizeSelect.appendChild(opt); });");
            sb.AppendLine("      pageSizeSelect.value = pageSize;");
            sb.AppendLine("      pageSizeSelect.addEventListener('change', ()=>{ pageSize = parseInt(pageSizeSelect.value,10); page = 1; render(); });");
            sb.AppendLine("");
            sb.AppendLine("      function truncateForCell(s){");
            sb.AppendLine("        if(s == null) return '';");
            sb.AppendLine("        var max = window.__ssa_maxFieldLength;");
            sb.AppendLine("        if(!max || max <= 0) return s;");
            sb.AppendLine("        return (s.length > max) ? s.slice(0, max) + '... (truncated)' : s;");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      function escapeHtml(s){ if(s==null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;'); }");
            sb.AppendLine("");
            sb.AppendLine("      function renderRow(item, idx){");
            sb.AppendLine("        function makeCell(content, full){");
            sb.AppendLine("          if(!full) return '<td>' + escapeHtml(content) + '</td>'; ");
            sb.AppendLine("          var truncated = truncateForCell(content || '');");
            sb.AppendLine("          var view = (content && content.length > truncated.length) ? '<div><a href=\"#\" class=\"view-full\" data-full=\"' + encodeURIComponent(content) + '\">View</a></div>' : '';");
            sb.AppendLine("          return '<td>' + escapeHtml(truncated) + view + '</td>'; ");
            sb.AppendLine("        }");
            sb.AppendLine("");
            sb.AppendLine("        var cells = [];");
            sb.AppendLine("        cells.push('<td style=\"max-width:420px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis\">' + escapeHtml(item.RequestUrl) + '</td>');");
            sb.AppendLine("        cells.push('<td>' + escapeHtml(item.RequestMethod) + '</td>');");
            sb.AppendLine("        cells.push('<td>' + escapeHtml(item.RequestTimestamp) + '</td>');");
            sb.AppendLine("        cells.push('<td>' + escapeHtml(item.ResponseTimestamp) + '</td>');");
            sb.AppendLine("        cells.push('<td class=\"small-muted\">' + (item.LatencyMs == null ? '' : item.LatencyMs) + '</td>');");
            sb.AppendLine("        cells.push('<td>' + escapeHtml(item.Status) + '</td>');");
            sb.AppendLine("        cells.push('<td>' + escapeHtml(item.ResourceType) + '</td>');");
            sb.AppendLine("        cells.push(makeCell(item.RequestHeaders || '', true));");
            sb.AppendLine("        cells.push(makeCell(item.RequestBody || '', true));");
            sb.AppendLine("        cells.push(makeCell(item.ResponseHeaders || '', true));");
            sb.AppendLine("        cells.push(makeCell(item.ResponseBody || '', true));");
            sb.AppendLine("");
            sb.AppendLine("        return '<tr data-idx=\"'+idx+'\">' + cells.join('') + '</tr>'; ");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      function render(){");
            sb.AppendLine("        const start = (page-1)*pageSize;");
            sb.AppendLine("        const pageItems = filtered.slice(start, start+pageSize);");
            sb.AppendLine("        gridBody.innerHTML = pageItems.map((it, i) => renderRow(it, start + i)).join('');");
            sb.AppendLine("        pageInfo.textContent = 'Page ' + page + ' of ' + Math.max(1, Math.ceil(filtered.length / pageSize)) + ' — ' + filtered.length + ' items';");
            sb.AppendLine("        populateDownload();");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      function renderPager(){");
            sb.AppendLine("        const pager = document.getElementById('pager');");
            sb.AppendLine("        if(!pager) return;");
            sb.AppendLine("        const totalPages = Math.max(1, Math.ceil(filtered.length/pageSize));");
            sb.AppendLine("        pager.innerHTML = '';");
            sb.AppendLine("        function addBtn(text, cb, disabled){ var li = document.createElement('li'); li.className = 'page-item ' + (disabled ? 'disabled' : ''); var a = document.createElement('a'); a.className = 'page-link'; a.href = '#'; a.textContent = text; a.onclick = function(ev){ ev.preventDefault(); if(!disabled) cb(); }; li.appendChild(a); pager.appendChild(li); }");
            sb.AppendLine("        addBtn('⏮', ()=>{ page = 1; render(); }, page === 1);");
            sb.AppendLine("        addBtn('◀ Prev', ()=>{ if(page>1) page--; render(); }, page === 1);");
            sb.AppendLine("        addBtn('Next ▶', ()=>{ if(page < totalPages) page++; render(); }, page === totalPages);");
            sb.AppendLine("        addBtn('⏭', ()=>{ page = totalPages; render(); }, page === totalPages);");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      function applyFilters(){");
            sb.AppendLine("        const q = (globalSearch.value||'').toLowerCase();");
            sb.AppendLine("        const status = statusFilter.value || '';");
            sb.AppendLine("        const method = methodFilter.value || '';");
            sb.AppendLine("        const resource = resourceFilter.value || '';");
            sb.AppendLine("        filtered = data.filter(d => {");
            sb.AppendLine("          if(status && String(d.Status) !== String(status)) return false;");
            sb.AppendLine("          if(method && method.length && String(d.RequestMethod || '').toLowerCase() !== String(method||'').toLowerCase()) return false;");
            sb.AppendLine("          if(resource && resource.length && String(d.ResourceType || '').toLowerCase() !== String(resource||'').toLowerCase()) return false;");
            sb.AppendLine("          if(!q) return true;");
            sb.AppendLine("          const hay = ((d.RequestUrl||'') + ' ' + (d.RequestHeaders||'') + ' ' + (d.ResponseHeaders||'') + ' ' + (d.RequestBody||'') + ' ' + (d.ResponseBody||'')).toLowerCase();");
            sb.AppendLine("          return hay.indexOf(q) !== -1;");
            sb.AppendLine("        });");
            sb.AppendLine("        page = 1; render();");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      globalSearch.addEventListener('input', ()=> applyFilters());");
            sb.AppendLine("      statusFilter.addEventListener('change', ()=> applyFilters());");
            sb.AppendLine("      methodFilter.addEventListener('change', ()=> applyFilters());");
            sb.AppendLine("      resourceFilter.addEventListener('change', ()=> applyFilters());");
            sb.AppendLine("      resetFilters.addEventListener('click', ()=>{ globalSearch.value=''; statusFilter.value=''; methodFilter.value=''; resourceFilter.value=''; applyFilters(); });");
            sb.AppendLine("");
            sb.AppendLine("      // delegated click for full view links");
            sb.AppendLine("      gridBody.addEventListener('click', function(ev){");
            sb.AppendLine("        var a = ev.target.closest('a.view-full'); if(!a) return; ev.preventDefault(); var full = decodeURIComponent(a.getAttribute('data-full') || ''); modalPre.textContent = full; if(bsModal) bsModal.show(); else alert(full);");
            sb.AppendLine("      });");
            sb.AppendLine("");
            sb.AppendLine("      function populateDownload(){");
            sb.AppendLine("        const json = JSON.stringify(filtered, null, 2);");
            sb.AppendLine("        const blob = new Blob([json], { type: 'application/json' });");
            sb.AppendLine("        downloadJson.href = URL.createObjectURL(blob);");
            sb.AppendLine("        downloadJson.download = 'report.json';");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      function populateFilters(){");
            sb.AppendLine("        const statuses = Array.from(new Set(data.map(d=>String(d.Status||'')).filter(x=>x))).sort();");
            sb.AppendLine("        statusFilter.innerHTML = '<option value=\"\">All statuses</option>' + statuses.map(s=>`<option value='${s}'>${s}</option>`).join('');");
            sb.AppendLine("        const methods = Array.from(new Set(data.map(d=>String(d.RequestMethod||'')).filter(x=>x))).sort();");
            sb.AppendLine("        methodFilter.innerHTML = '<option value=\"\">All methods</option>' + methods.map(s=>`<option value='${s}'>${s}</option>`).join('');");
            sb.AppendLine("        const resources = Array.from(new Set(data.map(d=>String(d.ResourceType||'')).filter(x=>x))).sort();");
            sb.AppendLine("        resourceFilter.innerHTML = '<option value=\"\">All resources</option>' + resources.map(s=>`<option value='${s}'>${s}</option>`).join('');");
            sb.AppendLine("      }");
            sb.AppendLine("");
            sb.AppendLine("      // initial");
            sb.AppendLine("      populateFilters(); applyFilters(); populateDownload();");
            sb.AppendLine("");
            sb.AppendLine("    })();");
            sb.AppendLine("  </script>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
