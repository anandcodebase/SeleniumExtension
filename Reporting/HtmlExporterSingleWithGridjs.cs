using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Single-file HTML exporter using grid.js (fast paged rendering).
    /// Truncates text beyond 200 characters in cell; clicking cell opens modal with full text.
    /// </summary>
    public static class HtmlExporterSingleWithGridjs
    {
        public static string ExportNetworkInfoToSingleHtml(
            IEnumerable<FullNetworkInfo> items,
            string outFolderRoot,
            string reportName = null,
            int pageSize = 50,
            int maxFieldLength = 200) // default changed to 200
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot)) throw new ArgumentNullException(nameof(outFolderRoot));
            Directory.CreateDirectory(outFolderRoot);

            var safeName = string.IsNullOrWhiteSpace(reportName) ? "report" : MakeSafeFileName(reportName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var baseName = $"{safeName}-{timestamp}";
            var filePath = GetUniqueFilePath(outFolderRoot, baseName, ".html");

            var rows = (items ?? Enumerable.Empty<FullNetworkInfo>()).Select((e, idx) => new
            {
                __index = idx,
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
            sb.AppendLine("  <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' crossorigin='anonymous'>");
            sb.AppendLine("  <link href='https://unpkg.com/gridjs/dist/theme/mermaid.min.css' rel='stylesheet' />");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body{background:#f8f9fa}");
            sb.AppendLine("    .container{max-width:1400px;margin:16px auto}");
            sb.AppendLine("    td{white-space:pre-wrap;word-break:break-word;max-width:420px;overflow:hidden;text-overflow:ellipsis}");
            sb.AppendLine("    thead th{position:sticky;top:0;background:white;z-index:5}");
            sb.AppendLine("    .thumb{max-width:120px;max-height:80px;object-fit:cover;border-radius:4px;border:1px solid #ddd}");
            sb.AppendLine("    .select-col{width:36px;text-align:center}");
            sb.AppendLine("    .cell-content{cursor:pointer}"); // indicate clickable
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

            // Modal
            sb.AppendLine("  <div class='modal fade' id='fullModal' tabindex='-1'><div class='modal-dialog modal-xl modal-dialog-centered'><div class='modal-content'><div class='modal-header'><h5 class='modal-title'>Full content</h5><button type='button' class='btn-close' data-bs-dismiss='modal' aria-label='Close'></button></div><div class='modal-body'><div id='modalBody'></div></div><div class='modal-footer'><button class='btn btn-secondary' data-bs-dismiss='modal'>Close</button></div></div></div></div>");

            // Embedded data
            sb.AppendLine("  <script>");
            sb.AppendLine($"    window.__ssa_data = {dataJson};");
            sb.AppendLine($"    window.__ssa_pageSizeDefault = {pageSize};");
            sb.AppendLine($"    window.__ssa_maxFieldLength = {maxFieldLength};");
            sb.AppendLine("  </script>");

            // Grid.js + polyfill + bootstrap
            sb.AppendLine("  <script src='https://unpkg.com/gridjs/dist/gridjs.umd.js'></script>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    if (!gridjs.htmlEscape) {");
            sb.AppendLine("      gridjs.htmlEscape = function (str) { if (str == null) return ''; return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;').replace(/'/g,'&#39;'); };");
            sb.AppendLine("    }");
            sb.AppendLine("  </script>");
            sb.AppendLine("  <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js' crossorigin='anonymous'></script>");

            // Client JS (paged rendering, click cell opens modal)
            sb.AppendLine("  <script>");
            sb.AppendLine("  (function(){");
            sb.AppendLine("    const allData = window.__ssa_data || [];");
            sb.AppendLine("    const maxField = window.__ssa_maxFieldLength || 200;");
            sb.AppendLine("    const defaultPageSize = window.__ssa_pageSizeDefault || 50;");
            sb.AppendLine("    let pageSize = defaultPageSize; let currentPage = 1; let filteredObjects = allData.slice(); const selected = new Set();");
            sb.AppendLine("    const gridContainer = document.getElementById('grid'); const globalSearch = document.getElementById('globalSearch');");
            sb.AppendLine("    const statusFilter = document.getElementById('statusFilter'); const methodFilter = document.getElementById('methodFilter'); const resourceFilter = document.getElementById('resourceFilter');");
            sb.AppendLine("    const downloadJson = document.getElementById('downloadJson'); const exportSelectedBtn = document.getElementById('exportSelected'); const summaryEl = document.getElementById('summary');");
            sb.AppendLine("    function esc(s){ if(s==null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }");
            sb.AppendLine("    function isImageUrl(u){ if(!u) return false; try{ const s=String(u).trim(); if(s.toLowerCase().startsWith('data:image/')) return true; try{ const parsed=new URL(s, window.location.href); return /\\.(png|jpe?g|gif|webp|bmp)$/i.test(parsed.pathname);}catch{return /^[.]{1,2}\\/.*\\.(png|jpe?g|gif|webp|bmp)$/i.test(s);} }catch{return false;} }");
            sb.AppendLine("    function makeClickableHtmlForText(s){ if(!s) return ''; const text=String(s); if(maxField<=0) return '<div class=\"cell-content\" data-full=\"'+encodeURIComponent(text)+'\">'+gridjs.htmlEscape(text)+'</div>'; if(text.length<=maxField) return '<div class=\"cell-content\" data-full=\"'+encodeURIComponent(text)+'\">'+gridjs.htmlEscape(text)+'</div>'; const short = gridjs.htmlEscape(text.slice(0,maxField)) + '...'; return '<div class=\"cell-content\" data-full=\"'+encodeURIComponent(text)+'\">'+short+'</div>'; }");
            sb.AppendLine("    const columns = [");
            sb.AppendLine("      { id:'select', name:'', formatter: (_,row)=>gridjs.html('<input type=\"checkbox\" class=\"row-select\"/>'), sort:false, width:36 },");
            sb.AppendLine("      { id:'RequestUrl', name:'RequestUrl', formatter: c => gridjs.html('<div class=\"cell-content\" data-full=\"'+encodeURIComponent(c||'')+'\">'+gridjs.htmlEscape(c||'')+'</div>') },");
            sb.AppendLine("      { id:'RequestMethod', name:'Method' }, { id:'RequestTimestamp', name:'Req Time (UTC)' }, { id:'ResponseTimestamp', name:'Resp Time (UTC)' },");
            sb.AppendLine("      { id:'LatencyMs', name:'Latency' }, { id:'Status', name:'Status' }, { id:'ResourceType', name:'Resource' },");
            sb.AppendLine("      { id:'RequestHeaders', name:'RequestHeaders', formatter: c => gridjs.html(makeClickableHtmlForText(c)) },");
            sb.AppendLine("      { id:'RequestBody', name:'RequestBody', formatter: c => { if(isImageUrl(c)) return gridjs.html('<img src=\"'+gridjs.htmlEscape(c)+'\" class=\"thumb view-media\" data-full=\"'+encodeURIComponent(c)+'\" loading=\"lazy\"/>'); return gridjs.html(makeClickableHtmlForText(c)); } },");
            sb.AppendLine("      { id:'ResponseHeaders', name:'ResponseHeaders', formatter: c => gridjs.html(makeClickableHtmlForText(c)) },");
            sb.AppendLine("      { id:'ResponseBody', name:'ResponseBody', formatter: c => { if(isImageUrl(c)) return gridjs.html('<img src=\"'+gridjs.htmlEscape(c)+'\" class=\"thumb view-media\" data-full=\"'+encodeURIComponent(c)+'\" loading=\"lazy\"/>'); return gridjs.html(makeClickableHtmlForText(c)); } }");
            sb.AppendLine("    ];");
            sb.AppendLine("    const grid = new gridjs.Grid({ columns: columns, data: [], pagination:{enabled:true,limit:pageSize,summary:true},sort:true,search:false,fixedHeader:true }).render(gridContainer);");
            sb.AppendLine("    function getPage(objs, page, limit){ const start=(page-1)*limit; return objs.slice(start, start+limit); }");
            sb.AppendLine("    function renderPage(){ const pageSlice = getPage(filteredObjects, currentPage, pageSize); const data = pageSlice.map(o=>['', o.RequestUrl, o.RequestMethod, o.RequestTimestamp, o.ResponseTimestamp, o.LatencyMs, o.Status, o.ResourceType, o.RequestHeaders, o.RequestBody, o.ResponseHeaders, o.ResponseBody]); const schedule = window.requestIdleCallback || function(cb){return setTimeout(cb,50);}; schedule(()=>{ grid.updateConfig({data:data, pagination:{enabled:true,limit:pageSize}}).forceRender(); attachMeta(pageSlice); }); }");
            sb.AppendLine("    function attachMeta(slice){ try{ const tbody=document.querySelector('.gridjs-table tbody'); if(!tbody) return; const rows=Array.from(tbody.querySelectorAll('tr')); rows.forEach((tr,i)=>{ const obj=slice[i]; if(!obj) return; tr.dataset.objIndex = obj.__index; const first=tr.querySelector('td'); if(first) first.innerHTML = `<input type=\"checkbox\" class=\"row-select\" data-idx=\"${obj.__index}\" ${selected.has(obj.__index)?'checked':''} />`; Array.from(tr.querySelectorAll('td')).forEach(td=>{ if(!td.dataset._title){ td.title = td.textContent || ''; td.dataset._title='1'; } }); }); }catch(e){console.error(e);} }");
            sb.AppendLine("    function applyFilters(){ const q=(globalSearch.value||'').toLowerCase(); const status=(statusFilter.value||''); const method=(methodFilter.value||''); const resource=(resourceFilter.value||''); filteredObjects = allData.filter(d=>{ if(status && String(d.Status)!==String(status)) return false; if(method && method.length && String(d.RequestMethod||'').toLowerCase() !== String(method||'').toLowerCase()) return false; if(resource && resource.length && String(d.ResourceType||'').toLowerCase() !== String(resource||'').toLowerCase()) return false; if(!q) return true; const hay = ((d.RequestUrl||'')+' '+(d.RequestHeaders||'')+' '+(d.ResponseHeaders||'')+' '+(d.RequestBody||'')+' '+(d.ResponseBody||'')).toLowerCase(); return hay.indexOf(q)!==-1; }); currentPage=1; renderPage(); updatePager(); }");
            sb.AppendLine("    function populateFilters(){ const statuses = Array.from(new Set(allData.map(d=>String(d.Status||'')).filter(x=>x))).sort(); document.getElementById('statusFilter').innerHTML = '<option value=\"\">All statuses</option>' + statuses.map(s=>`<option value='${s}'>${s}</option>`).join(''); }");
            sb.AppendLine("    function updatePager(){ const totalPages = Math.max(1, Math.ceil(filteredObjects.length/pageSize)); const pager = document.getElementById('pager'); if(!pager) return; pager.innerHTML=''; function addBtn(t,cb,dis){ const li=document.createElement('li'); li.className='page-item '+(dis?'disabled':''); const a=document.createElement('a'); a.className='page-link'; a.href='#'; a.textContent=t; a.onclick=function(e){e.preventDefault(); if(!dis) cb();}; li.appendChild(a); pager.appendChild(li);} addBtn('⏮', ()=>{ currentPage=1; renderPage(); }, currentPage===1); addBtn('Prev', ()=>{ if(currentPage>1){currentPage--; renderPage();}}, currentPage===1); const start=Math.max(1,currentPage-2), end=Math.min(totalPages,currentPage+2); for(let p=start;p<=end;p++){ addBtn(String(p), ()=>{ currentPage=p; renderPage(); }, false); } addBtn('Next', ()=>{ if(currentPage<totalPages){ currentPage++; renderPage(); } }, currentPage===totalPages); addBtn('⏭', ()=>{ currentPage=totalPages; renderPage(); }, currentPage===totalPages); if(summaryEl) summaryEl.textContent = `Rows: ${filteredObjects.length} — page ${currentPage}/${totalPages}`; }");
            sb.AppendLine("    document.getElementById('globalSearch').addEventListener('input', debounce(applyFilters, 200)); document.getElementById('statusFilter').addEventListener('change', applyFilters);");
            sb.AppendLine("    document.body.addEventListener('change', function(ev){ const cb = ev.target.closest('.row-select'); if(!cb) return; const idx = Number(cb.getAttribute('data-idx')); if(cb.checked) selected.add(idx); else selected.delete(idx); });");
            sb.AppendLine("    document.body.addEventListener('click', function(ev){ // cell click for full text");
            sb.AppendLine("      const cell = ev.target.closest('.cell-content'); if(cell){ ev.preventDefault(); const full = decodeURIComponent(cell.getAttribute('data-full')||''); const modal = new bootstrap.Modal(document.getElementById('fullModal')); document.getElementById('modalBody').innerHTML = '<pre>'+esc(full)+'</pre>'; modal.show(); return; }");
            sb.AppendLine("      const img = ev.target.closest('img.view-media'); if(img){ ev.preventDefault(); const src = decodeURIComponent(img.getAttribute('data-full')||img.src||''); const modal = new bootstrap.Modal(document.getElementById('fullModal')); modal.show(); document.getElementById('modalBody').innerHTML = `<img src=\"${src.Replace(\"\\\"\",\"&quot;\")}\" style=\"max-width:100%;height:auto;display:block;margin:0 auto\" />`; return; }");
            sb.AppendLine("    });");
            sb.AppendLine("    document.getElementById('downloadJson').addEventListener('click', function(){ const json = JSON.stringify(filteredObjects, null, 2); const blob = new Blob([json], { type: 'application/json' }); const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = 'filtered-report.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url); });");
            sb.AppendLine("    document.getElementById('exportSelected').addEventListener('click', function(){ if(selected.size===0){ alert('No rows selected'); return; } const objs = allData.filter(o=> selected.has(o.__index)); const blob = new Blob([JSON.stringify(objs, null, 2)], { type: 'application/json' }); const url = URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download='selected-report.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url); });");
            sb.AppendLine("    function debounce(fn, ms){ let t; return function(){ clearTimeout(t); t = setTimeout(()=>fn.apply(this, arguments), ms); }; }");
            sb.AppendLine("    populateFilters(); filteredObjects = allData.slice(); renderPage(); updatePager(); window.__ssa_setPageSize = function(sz){ pageSize = Math.max(1, Number(sz)||defaultPageSize); currentPage = 1; renderPage(); updatePager(); };");
            sb.AppendLine("  })();");
            sb.AppendLine("  </script>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
