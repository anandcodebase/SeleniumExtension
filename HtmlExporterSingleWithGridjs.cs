using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Single-file HTML exporter using grid.js with:
    /// - fast paged rendering (only current page rendered)
    /// - truncated columns with title tooltip and "View" modal
    /// - media thumbnails (images) clickable to expand
    /// - selection column + "Export Selected"
    /// - Download Filtered JSON exports filtered objects with original field names
    /// </summary>
    public static class HtmlExporterSingleWithGridjs
    {
        public static string ExportNetworkInfoToSingleHtml(
            IEnumerable<FullNetworkInfo> items,
            string outFolderRoot,
            string reportName = null,
            int pageSize = 50,
            int maxFieldLength = 1000)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot)) throw new ArgumentNullException(nameof(outFolderRoot));
            Directory.CreateDirectory(outFolderRoot);

            var safeName = string.IsNullOrWhiteSpace(reportName) ? "report" : MakeSafeFileName(reportName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var baseName = $"{safeName}-{timestamp}";
            var filePath = GetUniqueFilePath(outFolderRoot, baseName, ".html");

            // Build serializable objects (preserve original field names)
            var rows = (items ?? Enumerable.Empty<FullNetworkInfo>()).Select((e, idx) => new
            {
                __index = idx, // preserve global index for selection mapping
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

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string dataJson = JsonSerializer.Serialize(rows, jsonOptions);
            dataJson = dataJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

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
            // Bootstrap + grid.js CSS
            sb.AppendLine("  <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' crossorigin='anonymous'>");
            sb.AppendLine("  <link href='https://unpkg.com/gridjs/dist/theme/mermaid.min.css' rel='stylesheet' />");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body{background:#f8f9fa}");
            sb.AppendLine("    .container{max-width:1400px;margin:16px auto}");
            sb.AppendLine("    td{white-space:pre-wrap;word-break:break-word;max-width:420px;overflow:hidden;text-overflow:ellipsis}");
            sb.AppendLine("    thead th{position:sticky;top:0;background:white;z-index:5}");
            sb.AppendLine("    .thumb{max-width:120px;max-height:80px;object-fit:cover;border-radius:4px;border:1px solid #ddd}");
            sb.AppendLine("    .select-col{width:36px;text-align:center}");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class='container py-3'>");
            sb.AppendLine("    <div class='d-flex mb-3 align-items-center'>");
            sb.AppendLine("      <h4 class='me-3'>Network Report</h4>");
            sb.AppendLine($"      <div id='summary' class='text-muted small'>Rows: {totalRows} — generated {DateTime.UtcNow.ToString("u")}</div>");
            sb.AppendLine("      <div class='ms-auto d-flex gap-2 align-items-center'>");
            sb.AppendLine("        <input id='globalSearch' class='form-control form-control-sm' placeholder='Global search...' style='min-width:220px'/>");
            sb.AppendLine("        <select id='statusFilter' class='form-select form-select-sm' style='width:140px'><option value=''>All statuses</option></select>");
            sb.AppendLine("        <select id='methodFilter' class='form-select form-select-sm' style='width:120px'><option value=''>All methods</option></select>");
            sb.AppendLine("        <select id='resourceFilter' class='form-select form-select-sm' style='width:140px'><option value=''>All resources</option></select>");
            sb.AppendLine("        <button id='downloadJson' class='btn btn-sm btn-outline-primary'>Download Filtered JSON</button>");
            sb.AppendLine("        <button id='exportSelected' class='btn btn-sm btn-outline-success'>Export Selected</button>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div id='grid'></div>");
            sb.AppendLine("  </div>");

            // Modal for full content or media
            sb.AppendLine("  <div class='modal fade' id='fullModal' tabindex='-1'><div class='modal-dialog modal-xl modal-dialog-centered'><div class='modal-content'><div class='modal-header'><h5 class='modal-title'>Full content</h5><button type='button' class='btn-close' data-bs-dismiss='modal' aria-label='Close'></button></div><div class='modal-body'><div id='modalBody'></div></div><div class='modal-footer'><button class='btn btn-secondary' data-bs-dismiss='modal'>Close</button></div></div></div></div>");

            // Embedded data (preserve original fields including __index for selection)
            sb.AppendLine("  <script>");
            sb.AppendLine($"    window.__ssa_data = {dataJson};");
            sb.AppendLine($"    window.__ssa_pageSizeDefault = {pageSize};");
            sb.AppendLine($"    window.__ssa_maxFieldLength = {maxFieldLength};");
            sb.AppendLine("  </script>");

            // Include Grid.js + Bootstrap JS
            sb.AppendLine("  <script src='https://unpkg.com/gridjs/dist/gridjs.umd.js'></script>");
            // Polyfill gridjs.htmlEscape if missing (grid.js v6+ removed it)
            sb.AppendLine("  <script>");
            sb.AppendLine("    if (!gridjs.htmlEscape) {");
            sb.AppendLine("      gridjs.htmlEscape = function (str) {");
            sb.AppendLine("        if (str == null) return '';");
            sb.AppendLine("        return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;').replace(/'/g, '&#39;');");
            sb.AppendLine("      };");
            sb.AppendLine("    }");
            sb.AppendLine("  </script>");

            sb.AppendLine("  <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js' crossorigin='anonymous'></script>");

            // Optimized client JS: paged rendering, lazy images, selection, export, modal
            sb.AppendLine("  <script>");
            sb.AppendLine("  (function(){");
            sb.AppendLine("    // Configuration (populated by exporter)");
            sb.AppendLine("    const allData = window.__ssa_data || [];");
            sb.AppendLine("    const maxField = window.__ssa_maxFieldLength || 1000;");
            sb.AppendLine("    const defaultPageSize = window.__ssa_pageSizeDefault || 50;");
            sb.AppendLine("");
            sb.AppendLine("    // State");
            sb.AppendLine("    let pageSize = defaultPageSize;");
            sb.AppendLine("    let currentPage = 1;");
            sb.AppendLine("    let filteredObjects = allData.slice();");
            sb.AppendLine("    const selected = new Set();");
            sb.AppendLine("");
            sb.AppendLine("    // DOM references");
            sb.AppendLine("    const gridContainer = document.getElementById('grid');");
            sb.AppendLine("    const globalSearch = document.getElementById('globalSearch');");
            sb.AppendLine("    const statusFilter = document.getElementById('statusFilter');");
            sb.AppendLine("    const methodFilter = document.getElementById('methodFilter');");
            sb.AppendLine("    const resourceFilter = document.getElementById('resourceFilter');");
            sb.AppendLine("    const downloadJson = document.getElementById('downloadJson');");
            sb.AppendLine("    const exportSelectedBtn = document.getElementById('exportSelected');");
            sb.AppendLine("    const summaryEl = document.getElementById('summary');");
            sb.AppendLine("");
            sb.AppendLine("    // spinner");
            sb.AppendLine("    function showSpinner() { if (!document.getElementById('ssa-spinner')) { const s = document.createElement('div'); s.id = 'ssa-spinner'; s.style = 'position:fixed;right:16px;bottom:16px;padding:10px;background:rgba(0,0,0,0.7);color:#fff;border-radius:6px;z-index:9999'; s.textContent = 'Rendering…'; document.body.appendChild(s); } }");
            sb.AppendLine("    function hideSpinner(){ const s = document.getElementById('ssa-spinner'); if(s) s.remove(); }");
            sb.AppendLine("");
            sb.AppendLine("    // basic escape");
            sb.AppendLine("    function esc(s){ if(s==null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }");
            sb.AppendLine("");
            sb.AppendLine("    // detect image");
            sb.AppendLine("    function isImageUrl(u){");
            sb.AppendLine("      if(!u) return false;");
            sb.AppendLine("      try {");
            sb.AppendLine("        const s = String(u).trim();");
            sb.AppendLine("        if (s.toLowerCase().startsWith('data:image/')) return true;");
            sb.AppendLine("        try {");
            sb.AppendLine("          const parsed = new URL(s, window.location.href);");
            sb.AppendLine("          return /\\.(png|jpe?g|gif|webp|bmp)$/i.test(parsed.pathname);");
            sb.AppendLine("        } catch {");
            sb.AppendLine("          return /^[.]{1,2}\\/.*\\.(png|jpe?g|gif|webp|bmp)$/i.test(s);");
            sb.AppendLine("        }");
            sb.AppendLine("      } catch { return false; }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // truncate helper (keeps original object intact)");
            sb.AppendLine("    function truncatedHtml(text){");
            sb.AppendLine("      if(!text) return '';");
            sb.AppendLine("      const s = String(text);");
            sb.AppendLine("      if(maxField <= 0) return esc(s);");
            sb.AppendLine("      if(s.length <= maxField) return esc(s);");
            sb.AppendLine("      const short = esc(s.slice(0, maxField)) + '...';");
            sb.AppendLine("      return `${short} <a href=\"#\" class=\"view-full\" data-full=\"${encodeURIComponent(s)}\">View</a>`;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Build grid columns (first column is selection checkbox)");
            sb.AppendLine("    const columns = [");
            sb.AppendLine("      { id: 'select', name: '', formatter: (_, row) => gridjs.html('<input type=\"checkbox\" class=\"row-select\" />'), sort: false, width: 36 },");
            sb.AppendLine("      { id: 'RequestUrl', name: 'RequestUrl', formatter: cell => gridjs.html('<div title=\"'+esc(cell||'')+'\" style=\"max-width:420px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis\">'+gridjs.htmlEscape(cell||'')+'</div>') },");
            sb.AppendLine("      { id: 'RequestMethod', name: 'Method' },");
            sb.AppendLine("      { id: 'RequestTimestamp', name: 'Req Time (UTC)' },");
            sb.AppendLine("      { id: 'ResponseTimestamp', name: 'Resp Time (UTC)' },");
            sb.AppendLine("      { id: 'LatencyMs', name: 'Latency' },");
            sb.AppendLine("      { id: 'Status', name: 'Status' },");
            sb.AppendLine("      { id: 'ResourceType', name: 'Resource' },");
            sb.AppendLine("      { id: 'RequestHeaders', name: 'RequestHeaders', formatter: (cell) => gridjs.html(truncatedHtml(cell)) },");
            sb.AppendLine("      { id: 'RequestBody', name: 'RequestBody', formatter: (cell) => { if(isImageUrl(cell)) return gridjs.html('<img src=\"'+gridjs.htmlEscape(cell)+'\" class=\"thumb view-media\" data-full=\"'+encodeURIComponent(cell)+'\" loading=\"lazy\" />'); return gridjs.html(truncatedHtml(cell)); } },");
            sb.AppendLine("      { id: 'ResponseHeaders', name: 'ResponseHeaders', formatter: (cell) => gridjs.html(truncatedHtml(cell)) },");
            sb.AppendLine("      { id: 'ResponseBody', name: 'ResponseBody', formatter: (cell) => { if(isImageUrl(cell)) return gridjs.html('<img src=\"'+gridjs.htmlEscape(cell)+'\" class=\"thumb view-media\" data-full=\"'+encodeURIComponent(cell)+'\" loading=\"lazy\" />'); return gridjs.html(truncatedHtml(cell)); } }");
            sb.AppendLine("    ];");
            sb.AppendLine("");
            sb.AppendLine("    // Create Grid.js instance with empty data; we'll feed page slices");
            sb.AppendLine("    const grid = new gridjs.Grid({");
            sb.AppendLine("      columns: columns,");
            sb.AppendLine("      data: [],");
            sb.AppendLine("      pagination: { enabled: true, limit: pageSize, summary: true },");
            sb.AppendLine("      sort: true,");
            sb.AppendLine("      search: false,");
            sb.AppendLine("      fixedHeader: true");
            sb.AppendLine("    }).render(gridContainer);");
            sb.AppendLine("");
            sb.AppendLine("    // Get page slice");
            sb.AppendLine("    function getPageSlice(objs, page, limit){ const start = (page - 1) * limit; return objs.slice(start, start + limit); }");
            sb.AppendLine("");
            sb.AppendLine("    // Render current page into grid");
            sb.AppendLine("    function renderCurrentPage(){");
            sb.AppendLine("      showSpinner();");
            sb.AppendLine("      const pageSlice = getPageSlice(filteredObjects, currentPage, pageSize);");
            sb.AppendLine("      const dataForGrid = pageSlice.map(o => [");
            sb.AppendLine("        '',");
            sb.AppendLine("        o.RequestUrl, o.RequestMethod, o.RequestTimestamp, o.ResponseTimestamp, o.LatencyMs, o.Status, o.ResourceType,");
            sb.AppendLine("        o.RequestHeaders, o.RequestBody, o.ResponseHeaders, o.ResponseBody");
            sb.AppendLine("      ]);");
            sb.AppendLine("      const schedule = window.requestIdleCallback || function(cb){ return setTimeout(cb, 50); };");
            sb.AppendLine("      schedule(function(){");
            sb.AppendLine("        grid.updateConfig({ data: dataForGrid, pagination: { enabled: true, limit: pageSize } }).forceRender();");
            sb.AppendLine("        attachRowMeta(pageSlice);");
            sb.AppendLine("        hideSpinner();");
            sb.AppendLine("      });");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Attach __index meta and set up checkbox states & titles");
            sb.AppendLine("    function attachRowMeta(pageSlice){");
            sb.AppendLine("      try {");
            sb.AppendLine("        const tbody = document.querySelector('.gridjs-table tbody');");
            sb.AppendLine("        if(!tbody) return;");
            sb.AppendLine("        const rows = Array.from(tbody.querySelectorAll('tr'));");
            sb.AppendLine("        rows.forEach((tr, i) => {");
            sb.AppendLine("          const obj = pageSlice[i];");
            sb.AppendLine("          if(!obj) return;");
            sb.AppendLine("          tr.dataset.objIndex = obj.__index;");
            sb.AppendLine("          const firstTd = tr.querySelector('td');");
            sb.AppendLine("          if(firstTd){");
            sb.AppendLine("            firstTd.innerHTML = `<input type=\"checkbox\" class=\"row-select\" data-idx=\"${obj.__index}\" ${selected.has(obj.__index) ? 'checked' : ''} />`;");
            sb.AppendLine("          }");
            sb.AppendLine("          Array.from(tr.querySelectorAll('td')).forEach(td => { if(!td.dataset._title){ td.title = td.textContent || ''; td.dataset._title = '1'; } });");
            sb.AppendLine("        });");
            sb.AppendLine("      } catch(e){ console.error('attachRowMeta error', e); }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Apply filters to allData, then show page 1");
            sb.AppendLine("    function applyFiltersDebounced(){");
            sb.AppendLine("      showSpinner();");
            sb.AppendLine("      setTimeout(() => {");
            sb.AppendLine("        const q = (globalSearch.value || '').toLowerCase();");
            sb.AppendLine("        const status = (statusFilter.value || '');");
            sb.AppendLine("        const method = (methodFilter.value || '');");
            sb.AppendLine("        const resource = (resourceFilter.value || '');");
            sb.AppendLine("        filteredObjects = allData.filter(d => {");
            sb.AppendLine("          if(status && String(d.Status) !== String(status)) return false;");
            sb.AppendLine("          if(method && method.length && String(d.RequestMethod || '').toLowerCase() !== String(method || '').toLowerCase()) return false;");
            sb.AppendLine("          if(resource && resource.length && String(d.ResourceType || '').toLowerCase() !== String(resource || '').toLowerCase()) return false;");
            sb.AppendLine("          if(!q) return true;");
            sb.AppendLine("          const hay = ((d.RequestUrl||'') + ' ' + (d.RequestHeaders||'') + ' ' + (d.ResponseHeaders||'') + ' ' + (d.RequestBody||'') + ' ' + (d.ResponseBody||'')).toLowerCase();");
            sb.AppendLine("          return hay.indexOf(q) !== -1;");
            sb.AppendLine("        });");
            sb.AppendLine("        currentPage = 1;");
            sb.AppendLine("        updatePager();");
            sb.AppendLine("        renderCurrentPage();");
            sb.AppendLine("        hideSpinner();");
            sb.AppendLine("      }, 200);");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Populate filter selects");
            sb.AppendLine("    function populateFilters(){");
            sb.AppendLine("      const statuses = Array.from(new Set(allData.map(d => String(d.Status||'')).filter(x=>x))).sort();");
            sb.AppendLine("      statusFilter.innerHTML = '<option value=\"\">All statuses</option>' + statuses.map(s => `<option value=\"${s}\">${s}</option>`).join('');");
            sb.AppendLine("      const methods = Array.from(new Set(allData.map(d => String(d.RequestMethod||'')).filter(x=>x))).sort();");
            sb.AppendLine("      methodFilter.innerHTML = '<option value=\"\">All methods</option>' + methods.map(s => `<option value=\"${s}\">${s}</option>`).join('');");
            sb.AppendLine("      const resources = Array.from(new Set(allData.map(d => String(d.ResourceType||'')).filter(x=>x))).sort();");
            sb.AppendLine("      resourceFilter.innerHTML = '<option value=\"\">All resources</option>' + resources.map(s => `<option value=\"${s}\">${s}</option>`).join('');");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Pager UI update (simple)");
            sb.AppendLine("    function updatePager(){");
            sb.AppendLine("      const totalPages = Math.max(1, Math.ceil(filteredObjects.length / pageSize));");
            sb.AppendLine("      const pager = document.getElementById('pager');");
            sb.AppendLine("      if(!pager) return;");
            sb.AppendLine("      pager.innerHTML = '';");
            sb.AppendLine("      function addBtn(text, cb, disabled){");
            sb.AppendLine("        const li = document.createElement('li'); li.className = 'page-item ' + (disabled ? 'disabled' : '');");
            sb.AppendLine("        const a = document.createElement('a'); a.className='page-link'; a.href='#'; a.textContent = text; a.onclick = function(e){ e.preventDefault(); if(!disabled) cb(); };");
            sb.AppendLine("        li.appendChild(a); pager.appendChild(li);");
            sb.AppendLine("      }");
            sb.AppendLine("      addBtn('⏮', ()=>{ currentPage = 1; renderCurrentPage(); }, currentPage === 1);");
            sb.AppendLine("      addBtn('Prev', ()=>{ if(currentPage>1){ currentPage--; renderCurrentPage(); } }, currentPage === 1);");
            sb.AppendLine("      const start = Math.max(1, currentPage - 2), end = Math.min(totalPages, currentPage + 2);");
            sb.AppendLine("      for(let p=start; p<=end; p++){ addBtn(String(p), ()=>{ currentPage = p; renderCurrentPage(); }, false); }");
            sb.AppendLine("      addBtn('Next', ()=>{ if(currentPage<totalPages){ currentPage++; renderCurrentPage(); } }, currentPage === totalPages);");
            sb.AppendLine("      addBtn('⏭', ()=>{ currentPage = totalPages; renderCurrentPage(); }, currentPage === totalPages);");
            sb.AppendLine("      if(summaryEl) summaryEl.textContent = `Rows: ${filteredObjects.length} — page ${currentPage}/${totalPages}`;");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Download filtered objects (original field names)");
            sb.AppendLine("    if(downloadJson){");
            sb.AppendLine("      downloadJson.addEventListener('click', function(){");
            sb.AppendLine("        const json = JSON.stringify(filteredObjects, null, 2);");
            sb.AppendLine("        const blob = new Blob([json], { type: 'application/json' });");
            sb.AppendLine("        const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = 'filtered-report.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);");
            sb.AppendLine("      });");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Export selected");
            sb.AppendLine("    if(exportSelectedBtn){");
            sb.AppendLine("      exportSelectedBtn.addEventListener('click', () => {");
            sb.AppendLine("        if(selected.size === 0){ alert('No rows selected'); return; }");
            sb.AppendLine("        const objs = allData.filter(o => selected.has(o.__index));");
            sb.AppendLine("        const blob = new Blob([JSON.stringify(objs, null, 2)], { type: 'application/json' });");
            sb.AppendLine("        const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = 'selected-report.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);");
            sb.AppendLine("      });");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Delegated events: checkbox, view, media click");
            sb.AppendLine("    document.body.addEventListener('change', function(ev){");
            sb.AppendLine("      const cb = ev.target.closest('.row-select'); if(!cb) return; const idx = Number(cb.getAttribute('data-idx')); if(cb.checked) selected.add(idx); else selected.delete(idx);");
            sb.AppendLine("    });");
            sb.AppendLine("");
            sb.AppendLine("    document.body.addEventListener('click', function(ev){");
            sb.AppendLine("      const a = ev.target.closest('a.view-full');");
            sb.AppendLine("      if(a){ ev.preventDefault(); const text = decodeURIComponent(a.getAttribute('data-full')||''); const modal = new bootstrap.Modal(document.getElementById('fullModal')); document.getElementById('modalBody').innerHTML = '<pre>'+esc(text)+'</pre>'; modal.show(); return; }");
            sb.AppendLine("      const img = ev.target.closest('img.view-media');");
            sb.AppendLine("      if(img){ ev.preventDefault(); const src = decodeURIComponent(img.getAttribute('data-full')||img.src||''); const modal = new bootstrap.Modal(document.getElementById('fullModal')); modal.show(); document.getElementById('modalBody').innerHTML = `<img src=\"${src.replace(/\"/g, '&quot;')}\" style=\"max-width:100%;height:auto;display:block;margin:0 auto\" />`; return; }");
            sb.AppendLine("    });");
            sb.AppendLine("");
            sb.AppendLine("    // Wire filters");
            sb.AppendLine("    globalSearch.addEventListener('input', debounce(applyFiltersDebounced, 200));");
            sb.AppendLine("    statusFilter.addEventListener('change', () => { applyFiltersDebounced(); });");
            sb.AppendLine("    methodFilter.addEventListener('change', () => { applyFiltersDebounced(); });");
            sb.AppendLine("    resourceFilter.addEventListener('change', () => { applyFiltersDebounced(); });");
            sb.AppendLine("");
            sb.AppendLine("    // small debounce helper");
            sb.AppendLine("    function debounce(fn, ms){ let t; return function(){ clearTimeout(t); t = setTimeout(()=>fn.apply(this, arguments), ms); }; }");
            sb.AppendLine("");
            sb.AppendLine("    // Initial setup");
            sb.AppendLine("    populateFilters();");
            sb.AppendLine("    applyFiltersDebounced();");
            sb.AppendLine("    updatePager();");
            sb.AppendLine("");
            sb.AppendLine("    // Expose a method to change page size if you have a selector");
            sb.AppendLine("    window.__ssa_setPageSize = function(size){ pageSize = Math.max(1, Number(size) || defaultPageSize); currentPage = 1; updatePager(); renderCurrentPage(); };");
            sb.AppendLine("");
            sb.AppendLine("  })();");
            sb.AppendLine("  </script>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
