using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Chunked exporter that writes pages and a central data.json file, plus an index.html.
    /// Client uses same fast paged grid and truncates text beyond 200 characters; clicking a cell opens modal with full text.
    /// </summary>
    public static class HtmlExporterChunkedWithGridjs
    {
        public static string ExportNetworkInfoToChunkedHtml(
            IEnumerable<FullNetworkInfo> items,
            string outFolderRoot,
            int pageSize = 250,
            int maxFieldLength = 200, // default 200
            bool emitNdjson = false,
            string reportName = null)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot)) throw new ArgumentNullException(nameof(outFolderRoot));
            Directory.CreateDirectory(outFolderRoot);

            var safeName = string.IsNullOrWhiteSpace(reportName) ? "report" : MakeSafeFileName(reportName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var baseFolderName = $"{safeName}-{timestamp}";
            var reportFolder = GetUniqueFolderPath(outFolderRoot, baseFolderName);
            Directory.CreateDirectory(reportFolder);

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

            int total = rows.Count;
            int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var pagesFolder = Path.Combine(reportFolder, "pages");
            Directory.CreateDirectory(pagesFolder);
            for (int p = 0; p < pageCount; p++)
            {
                var chunk = rows.Skip(p * pageSize).Take(pageSize).ToList();
                var pagePath = Path.Combine(pagesFolder, $"page-{p + 1}.json");
                File.WriteAllText(pagePath, JsonSerializer.Serialize(chunk, jsonOptions), Encoding.UTF8);
            }

            // central data.json
            var dataJsonPath = Path.Combine(reportFolder, "data.json");
            File.WriteAllText(dataJsonPath, JsonSerializer.Serialize(rows, jsonOptions), Encoding.UTF8);

            if (emitNdjson)
            {
                var ndPath = Path.Combine(reportFolder, "all.ndjson");
                using (var sw = new StreamWriter(ndPath, false, Encoding.UTF8))
                {
                    foreach (var r in rows) sw.WriteLine(JsonSerializer.Serialize(r, jsonOptions));
                }
            }

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

            var html = BuildClientHtml(pageCount, pageSize, maxFieldLength);
            File.WriteAllText(Path.Combine(reportFolder, "index.html"), html, Encoding.UTF8);

            return Path.GetFullPath(reportFolder);
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

        private static string GetUniqueFolderPath(string root, string baseName)
        {
            string candidate = Path.Combine(root, baseName);
            if (!Directory.Exists(candidate)) return candidate;
            for (int i = 1; i < 1000; i++)
            {
                var alt = Path.Combine(root, $"{baseName}-{i}");
                if (!Directory.Exists(alt)) return alt;
            }
            return Path.Combine(root, $"{baseName}-{Guid.NewGuid():N}");
        }

        private static string BuildClientHtml(int pageCount, int pageSize, int maxFieldLength)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset='utf-8'/>");
            sb.AppendLine("  <meta name='viewport' content='width=device-width,initial-scale=1'/>");
            sb.AppendLine("  <title>Network Export (grid.js)</title>");
            sb.AppendLine("  <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' crossorigin='anonymous'>");
            sb.AppendLine("  <link href='https://unpkg.com/gridjs/dist/theme/mermaid.min.css' rel='stylesheet' />");
            sb.AppendLine("  <style>td{white-space:pre-wrap;word-break:break-word;max-width:420px;overflow:hidden;text-overflow:ellipsis}.gridjs-container{font-size:13px}.thumb{max-width:120px;max-height:80px;object-fit:cover;border-radius:4px;border:1px solid #ddd}.select-col{width:36px;text-align:center}.cell-content{cursor:pointer}</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class='container py-3'>");
            sb.AppendLine("    <div class='d-flex mb-2 align-items-center'>");
            sb.AppendLine("      <h4 class='me-3'>Network Export</h4>");
            sb.AppendLine("      <div id='summary' class='text-muted small'>Loading…</div>");
            sb.AppendLine("      <div class='ms-auto d-flex gap-2 align-items-center'>");
            sb.AppendLine("        <input id='globalSearch' class='form-control form-control-sm' placeholder='Global search...' style='min-width:200px'/>");
            sb.AppendLine("        <select id='pageSizeSelect' class='form-select form-select-sm' style='width:120px'></select>");
            sb.AppendLine("        <select id='statusFilter' class='form-select form-select-sm' style='width:140px'><option value=''>All statuses</option></select>");
            sb.AppendLine("        <button id='downloadFiltered' class='btn btn-sm btn-outline-primary'>Download Filtered JSON</button>");
            sb.AppendLine("        <button id='exportSelected' class='btn btn-sm btn-outline-success'>Export Selected</button>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div id='grid'></div>");
            sb.AppendLine("    <nav class='d-flex justify-content-between align-items-center mt-2' aria-label='Pagination'>");
            sb.AppendLine("      <div class='small' id='pageInfo'>Page 0</div>");
            sb.AppendLine("      <ul class='pagination pagination-sm mb-0' id='pager'></ul>");
            sb.AppendLine("    </nav>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class='modal fade' id='fullModal' tabindex='-1'><div class='modal-dialog modal-xl'><div class='modal-content'><div class='modal-header'><h5 class='modal-title'>Full content</h5><button type='button' class='btn-close' data-bs-dismiss='modal' aria-label='Close'></button></div><div class='modal-body'><div id='modalBody'></div></div><div class='modal-footer'><button class='btn btn-secondary' data-bs-dismiss='modal'>Close</button></div></div></div></div>");
            sb.AppendLine("  <script src='https://unpkg.com/gridjs/dist/gridjs.umd.js'></script>");
            sb.AppendLine("  <script> if (!gridjs.htmlEscape) { gridjs.htmlEscape = function (s){ if(s==null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;').replace(/'/g,'&#39;'); }; } </script>");
            sb.AppendLine("  <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js' crossorigin='anonymous'></script>");

            // Client JS (fetch data.json then same behaviors; truncation 200 chars and click cell opens modal)
            sb.AppendLine("  <script>");
            sb.AppendLine("  (function(){");
            sb.AppendLine("    const DATA_URL = 'data.json';");
            sb.AppendLine("    const maxFieldDefault = " + maxFieldLength + ";");
            sb.AppendLine("    const pageSizeDefault = " + pageSize + ";");
            sb.AppendLine("    function showSpinner(t){ if(!document.getElementById('ssa-spinner')){ const s=document.createElement('div'); s.id='ssa-spinner'; s.style='position:fixed;right:16px;bottom:16px;padding:10px;background:rgba(0,0,0,0.7);color:#fff;border-radius:6px;z-index:9999'; s.textContent=t||'Loading…'; document.body.appendChild(s);} else document.getElementById('ssa-spinner').textContent=t||'Loading…'; }");
            sb.AppendLine("    function hideSpinner(){ const e=document.getElementById('ssa-spinner'); if(e) e.remove(); }");
            sb.AppendLine("    async function fetchData(){ showSpinner('Fetching data.json…'); try{ const r = await fetch(DATA_URL); if(!r.ok) throw new Error('Failed to fetch '+DATA_URL+': '+r.status); const j = await r.json(); hideSpinner(); return j; } catch(e){ hideSpinner(); throw e; } }");
            sb.AppendLine("    fetchData().then(init).catch(err=>{ console.error(err); alert('Failed to load data.json: '+err.message + '\\nIf opening via file:// your browser might block fetch. Serve the folder via local HTTP.'); });");
            sb.AppendLine("    function init(allData){ const maxField = maxFieldDefault; let pageSize = pageSizeDefault; let currentPage = 1; let filteredObjects = allData.slice(); const selected = new Set(); const gridContainer = document.getElementById('grid'); const globalSearch = document.getElementById('globalSearch'); const pageSizeSelect = document.getElementById('pageSizeSelect'); const statusFilter = document.getElementById('statusFilter'); const downloadFiltered = document.getElementById('downloadFiltered'); const exportSelected = document.getElementById('exportSelected'); const summary = document.getElementById('summary'); const pager = document.getElementById('pager');");
            sb.AppendLine("      function esc(s){ if(s==null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }");
            sb.AppendLine("      function isImageUrl(u){ if(!u) return false; try{ const s=String(u).trim(); if(s.toLowerCase().startsWith('data:image/')) return true; try{ const p=new URL(s, window.location.href); return /\\.(png|jpe?g|gif|webp|bmp)$/i.test(p.pathname);}catch{return /^[.]{1,2}\\/.*\\.(png|jpe?g|gif|webp|bmp)$/i.test(s);} }catch{return false;} }");
            sb.AppendLine("      function makeClickableHtml(s){ if(!s) return ''; const text=String(s); if(maxField<=0) return '<div class=\"cell-content\" data-full=\"'+encodeURIComponent(text)+'\">'+gridjs.htmlEscape(text)+'</div>'; if(text.length<=maxField) return '<div class=\"cell-content\" data-full=\"'+encodeURIComponent(text)+'\">'+gridjs.htmlEscape(text)+'</div>'; const short=gridjs.htmlEscape(text.slice(0,maxField)) + '...'; return '<div class=\"cell-content\" data-full=\"'+encodeURIComponent(text)+'\">'+short+'</div>'; }");
            sb.AppendLine("      [10,25,50,100,250].forEach(n=>{ const o=document.createElement('option'); o.value=n; o.text=n+' / page'; pageSizeSelect.appendChild(o); }); pageSizeSelect.value = pageSize; pageSizeSelect.addEventListener('change', ()=>{ pageSize = Number(pageSizeSelect.value); currentPage=1; renderCurrentPage(); renderPager(); });");
            sb.AppendLine("      const columns = [ { id:'select', name:'', formatter: (_,r)=>gridjs.html('<input type=\"checkbox\" class=\"row-select\"/>'), sort:false, width:36 }, { id:'RequestUrl', name:'RequestUrl', formatter: c => gridjs.html('<div class=\"cell-content\" data-full=\"'+encodeURIComponent(c||'')+'\">'+gridjs.htmlEscape(c||'')+'</div>') }, { id:'RequestMethod', name:'Method' }, { id:'RequestTimestamp', name:'Req Time (UTC)'} , { id:'ResponseTimestamp', name:'Resp Time (UTC)'}, { id:'LatencyMs', name:'Latency'}, { id:'Status', name:'Status'}, { id:'ResourceType', name:'Resource'}, { id:'RequestHeaders', name:'RequestHeaders', formatter: c => gridjs.html(makeClickableHtml(c)) }, { id:'RequestBody', name:'RequestBody', formatter: c => { if(isImageUrl(c)) return gridjs.html('<img src=\"'+gridjs.htmlEscape(c)+'\" class=\"thumb view-media\" data-full=\"'+encodeURIComponent(c)+'\" loading=\"lazy\" />'); return gridjs.html(makeClickableHtml(c)); } }, { id:'ResponseHeaders', name:'ResponseHeaders', formatter: c => gridjs.html(makeClickableHtml(c)) }, { id:'ResponseBody', name:'ResponseBody', formatter: c => { if(isImageUrl(c)) return gridjs.html('<img src=\"'+gridjs.htmlEscape(c)+'\" class=\"thumb view-media\" data-full=\"'+encodeURIComponent(c)+'\" loading=\"lazy\" />'); return gridjs.html(makeClickableHtml(c)); } } ];");
            sb.AppendLine("      const grid = new gridjs.Grid({ columns: columns, data: [], pagination:{enabled:true,limit:pageSize,summary:true}, sort:true, search:false, fixedHeader:true }).render(gridContainer);");
            sb.AppendLine("      function getPageSlice(objs, page, limit){ const start=(page-1)*limit; return objs.slice(start,start+limit); }");
            sb.AppendLine("      function renderCurrentPage(){ showSpinner('Rendering…'); const slice = getPageSlice(filteredObjects, currentPage, pageSize); const dataForGrid = slice.map(o=>['', o.RequestUrl, o.RequestMethod, o.RequestTimestamp, o.ResponseTimestamp, o.LatencyMs, o.Status, o.ResourceType, o.RequestHeaders, o.RequestBody, o.ResponseHeaders, o.ResponseBody]); const schedule = window.requestIdleCallback || function(cb){return setTimeout(cb,50);}; schedule(()=>{ grid.updateConfig({ data: dataForGrid, pagination:{ enabled:true, limit:pageSize } }).forceRender(); attachRowMeta(slice); hideSpinner(); }); }");
            sb.AppendLine("      function attachRowMeta(slice){ try{ const tbody=document.querySelector('.gridjs-table tbody'); if(!tbody) return; const rows=Array.from(tbody.querySelectorAll('tr')); rows.forEach((tr,i)=>{ const obj=slice[i]; if(!obj) return; tr.dataset.objIndex = obj.__index; const first=tr.querySelector('td'); if(first) first.innerHTML = `<input type=\"checkbox\" class=\"row-select\" data-idx=\"${obj.__index}\" ${selected.has(obj.__index)?'checked':''} />`; Array.from(tr.querySelectorAll('td')).forEach(td=>{ if(!td.dataset._title){ td.title = td.textContent || ''; td.dataset._title='1'; } }); }); }catch(e){ console.error(e);} }");
            sb.AppendLine("      function applyFiltersDebounced(){ showSpinner('Filtering…'); setTimeout(()=>{ const q=(globalSearch.value||'').toLowerCase(); const status=(statusFilter.value||''); filteredObjects = allData.filter(d=>{ if(status && String(d.Status)!==String(status)) return false; if(!q) return true; const hay = ((d.RequestUrl||'')+' '+(d.RequestHeaders||'')+' '+(d.ResponseHeaders||'')+' '+(d.RequestBody||'')+' '+(d.ResponseBody||'')).toLowerCase(); return hay.indexOf(q)!==-1; }); currentPage=1; renderPager(); renderCurrentPage(); hideSpinner(); }, 150); }");
            sb.AppendLine("      function renderPager(){ const totalPages = Math.max(1, Math.ceil(filteredObjects.length/pageSize)); const pagerEl = document.getElementById('pager'); if(!pagerEl) return; pagerEl.innerHTML=''; function addBtn(text, cb, disabled){ const li=document.createElement('li'); li.className='page-item '+(disabled?'disabled':''); const a=document.createElement('a'); a.className='page-link'; a.href='#'; a.textContent=text; a.onclick=function(e){ e.preventDefault(); if(!disabled) cb(); }; li.appendChild(a); pagerEl.appendChild(li);} addBtn('⏮', ()=>{ currentPage=1; renderCurrentPage(); }, currentPage===1); addBtn('Prev', ()=>{ if(currentPage>1){ currentPage--; renderCurrentPage(); } }, currentPage===1); const start=Math.max(1,currentPage-2), end=Math.min(totalPages,currentPage+2); for(let p=start;p<=end;p++){ addBtn(String(p), ()=>{ currentPage=p; renderCurrentPage(); }, false); } addBtn('Next', ()=>{ if(currentPage<totalPages){ currentPage++; renderCurrentPage(); } }, currentPage===totalPages); addBtn('⏭', ()=>{ currentPage=totalPages; renderCurrentPage(); }, currentPage===totalPages); const info = document.getElementById('pageInfo'); if(info) info.textContent = `Page ${currentPage} of ${totalPages} — ${filteredObjects.length} items`; }");
            sb.AppendLine("      function showSpinner(t){ if(!document.getElementById('ssa-spinner')){ const s=document.createElement('div'); s.id='ssa-spinner'; s.style='position:fixed;right:16px;bottom:16px;padding:10px;background:rgba(0,0,0,0.7);color:#fff;border-radius:6px;z-index:9999'; s.textContent = t||'Loading…'; document.body.appendChild(s);} else document.getElementById('ssa-spinner').textContent = t||'Loading…'; }");
            sb.AppendLine("      function hideSpinner(){ const e=document.getElementById('ssa-spinner'); if(e) e.remove(); }");
            sb.AppendLine("      downloadFiltered.addEventListener('click', ()=>{ const json = JSON.stringify(filteredObjects, null, 2); const blob = new Blob([json], {type:'application/json'}); const url = URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download='filtered-report.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url); });");
            sb.AppendLine("      exportSelected.addEventListener('click', ()=>{ if(selected.size===0){ alert('No rows selected'); return; } const objs = allData.filter(o=> selected.has(o.__index)); const blob = new Blob([JSON.stringify(objs, null, 2)], {type:'application/json'}); const url = URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download='selected-report.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url); });");
            sb.AppendLine("      document.body.addEventListener('change', function(ev){ const cb = ev.target.closest('.row-select'); if(!cb) return; const idx = Number(cb.getAttribute('data-idx')); if(cb.checked) selected.add(idx); else selected.delete(idx); });");
            sb.AppendLine("      document.body.addEventListener('click', function(ev){ const cell = ev.target.closest('.cell-content'); if(cell){ ev.preventDefault(); const full = decodeURIComponent(cell.getAttribute('data-full')||''); const modal = new bootstrap.Modal(document.getElementById('fullModal')); document.getElementById('modalBody').innerHTML = '<pre>'+esc(full)+'</pre>'; modal.show(); return; } const img = ev.target.closest('img.view-media'); if(img){ ev.preventDefault(); const src = decodeURIComponent(img.getAttribute('data-full')||img.src||''); const modal = new bootstrap.Modal(document.getElementById('fullModal')); modal.show(); document.getElementById('modalBody').innerHTML = `<img src=\"${src.Replace(/\", '&quot;')}\" style=\"max-width:100%;height:auto;display:block;margin:0 auto\" />`; return; } });");
            sb.AppendLine("      globalSearch.addEventListener('input', debounce(applyFiltersDebounced, 200)); statusFilter.addEventListener('change', ()=>{ applyFiltersDebounced(); });");
            sb.AppendLine("      function debounce(fn, ms){ let t; return function(){ clearTimeout(t); t = setTimeout(()=>fn.apply(this, arguments), ms); }; }");
            sb.AppendLine("      function populateAndStart(){ const statuses = Array.from(new Set(allData.map(d=>String(d.Status||'')).filter(x=>x))).sort(); statusFilter.innerHTML = '<option value=\"\">All statuses</option>' + statuses.map(s=>`<option value='${s}'>${s}</option>`).join(''); filteredObjects = allData.slice(); renderPager(); renderCurrentPage(); }");
            sb.AppendLine("      populateAndStart();");
            sb.AppendLine("    }"); // end init
            sb.AppendLine("  })();");
            sb.AppendLine("  </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
}
}
