using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SimpleSeleniumSupport.Network;

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
            => GetClientTemplate()
                .Replace("[[PGSZ]]", pageSize.ToString())
                .Replace("[[MAXF]]", maxFieldLength.ToString());

        // Raw string literal — JS template literals / braces need no escaping here.
        private static string GetClientTemplate() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>Network Export</title>
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" crossorigin="anonymous"/>
<link href="https://unpkg.com/gridjs/dist/theme/mermaid.min.css" rel="stylesheet"/>
<style>
:root{--c-2xx:#198754;--c-3xx:#0d6efd;--c-4xx:#fd7e14;--c-5xx:#dc3545}
body{background:#f5f6f8;font-size:13px;margin:0}
#topbar{background:#1e293b;color:#f1f5f9;padding:9px 20px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:100}
#topbar h1{font-size:15px;margin:0;font-weight:600}
#statsBar{display:flex;gap:10px;padding:10px 20px;background:#fff;border-bottom:1px solid #e2e8f0;flex-wrap:wrap;align-items:center}
.stat-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:6px 16px;text-align:center;min-width:100px}
.stat-val{font-size:18px;font-weight:700;line-height:1.2}
.stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.4px}
.stat-card.has-err .stat-val{color:#dc3545}
#filterBar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:8px 20px;background:#fff;border-bottom:1px solid #e2e8f0}
.sc-pills{display:flex;gap:3px}
.sc-btn{border:1px solid #cbd5e1;background:#fff;border-radius:20px;padding:2px 11px;font-size:11px;font-weight:600;cursor:pointer;transition:.12s}
.sc-btn:hover{background:#f1f5f9}.sc-btn.active{background:#1e293b;color:#fff;border-color:#1e293b}
.sc-btn.s2xx.active{background:var(--c-2xx);border-color:var(--c-2xx)}
.sc-btn.s3xx.active{background:var(--c-3xx);border-color:var(--c-3xx)}
.sc-btn.s4xx.active{background:var(--c-4xx);border-color:var(--c-4xx)}
.sc-btn.s5xx.active{background:var(--c-5xx);border-color:var(--c-5xx)}
#main{padding:12px 20px}
/* Status badges */
.sb{display:inline-block;padding:1px 7px;border-radius:4px;font-size:11px;font-weight:700;color:#fff}
.sb-2xx{background:var(--c-2xx)}.sb-3xx{background:var(--c-3xx)}.sb-4xx{background:var(--c-4xx)}.sb-5xx{background:var(--c-5xx)}.sb-unk{background:#94a3b8}
/* Method badges */
.mb{display:inline-block;padding:1px 6px;border-radius:3px;font-size:11px;font-weight:700;font-family:monospace}
.mb-get{background:#dbeafe;color:#1d4ed8}.mb-post{background:#dcfce7;color:#166534}
.mb-put{background:#fff7ed;color:#9a3412}.mb-del{background:#fee2e2;color:#991b1b}
.mb-pat{background:#f5f3ff;color:#6d28d9}.mb-hd{background:#f0fdf4;color:#166534}.mb-oth{background:#f1f5f9;color:#475569}
/* Latency badges */
.lb{display:inline-block;padding:1px 6px;border-radius:3px;font-size:11px;font-weight:600}
.lb-fast{background:#d1fae5;color:#065f46}.lb-ok{background:#fef9c3;color:#854d0e}
.lb-slow{background:#ffedd5;color:#9a3412}.lb-vslow{background:#fee2e2;color:#991b1b}
/* URL cell */
.url-cell{display:flex;align-items:center;gap:4px;max-width:360px}
.url-txt{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1;font-size:12px}
.copy-btn{display:none;background:none;border:1px solid #cbd5e1;border-radius:3px;padding:0 4px;font-size:10px;cursor:pointer;color:#64748b}
.url-cell:hover .copy-btn{display:inline}
/* Cell expand */
.cell-content{cursor:pointer;white-space:pre-wrap;word-break:break-word;max-width:340px;overflow:hidden;text-overflow:ellipsis;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical}
.gridjs-container{font-size:13px}
.thumb{max-width:100px;max-height:70px;object-fit:cover;border-radius:4px;border:1px solid #e2e8f0;cursor:zoom-in}
/* Pager */
#pageInfo{font-size:12px;color:#64748b}
</style>
</head>
<body>
<div id="topbar">
  <h1>Network Export</h1>
  <span id="gen-time" style="color:#94a3b8;font-size:11px"></span>
  <div style="margin-left:auto;display:flex;gap:8px">
    <button id="downloadFiltered" class="btn btn-sm btn-outline-light">Download Filtered JSON</button>
    <button id="exportSelected"   class="btn btn-sm btn-outline-light">Export Selected JSON</button>
  </div>
</div>

<div id="statsBar">
  <div class="stat-card"><div class="stat-val" id="s-total">—</div><div class="stat-lbl">Total</div></div>
  <div class="stat-card has-err"><div class="stat-val" id="s-err">—</div><div class="stat-lbl">Errors 4xx/5xx</div></div>
  <div class="stat-card"><div class="stat-val" id="s-lat">—</div><div class="stat-lbl">Avg Latency</div></div>
  <div class="stat-card"><div class="stat-val" id="s-show">—</div><div class="stat-lbl">Showing</div></div>
  <div id="load-msg" style="margin-left:12px;color:#64748b;font-size:12px">Loading data.json…</div>
</div>

<div id="filterBar">
  <div class="sc-pills">
    <button class="sc-btn active" data-sc="all">All</button>
    <button class="sc-btn s2xx" data-sc="2">2xx</button>
    <button class="sc-btn s3xx" data-sc="3">3xx</button>
    <button class="sc-btn s4xx" data-sc="4">4xx</button>
    <button class="sc-btn s5xx" data-sc="5">5xx</button>
  </div>
  <select id="f-method"   class="form-select form-select-sm" style="width:auto;min-width:110px"><option value="">All Methods</option></select>
  <select id="f-resource" class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Resources</option></select>
  <input  id="f-search"   class="form-control form-control-sm" placeholder="Search… (press /)" style="width:200px"/>
  <select id="f-pagesize" class="form-select form-select-sm" style="width:110px"></select>
  <button id="f-clear"    class="btn btn-sm btn-outline-secondary">Clear</button>
  <label style="font-size:12px;color:#64748b;margin-left:4px">
    <input type="checkbox" id="sel-all"/> Select all
  </label>
</div>

<div id="main">
  <div id="grid"></div>
  <nav class="d-flex justify-content-between align-items-center mt-2">
    <div id="pageInfo"></div>
    <ul class="pagination pagination-sm mb-0" id="pager"></ul>
  </nav>
</div>

<!-- Detail modal -->
<div class="modal fade" id="detailModal" tabindex="-1">
  <div class="modal-dialog modal-xl modal-dialog-scrollable">
    <div class="modal-content">
      <div class="modal-header py-2">
        <h6 class="modal-title" id="modal-title">Request Detail</h6>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body p-0">
        <ul class="nav nav-tabs px-3 pt-2" id="modal-tabs" role="tablist"></ul>
        <div class="tab-content p-3" id="modal-tab-content"></div>
      </div>
    </div>
  </div>
</div>

<script src="https://unpkg.com/gridjs/dist/gridjs.umd.js"></script>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js" crossorigin="anonymous"></script>
<script>
(function(){
  const PGSZ_DEFAULT = [[PGSZ]];
  const MAXF         = [[MAXF]];

  // ── Helpers ───────────────────────────────────────────────────────────────
  function esc(s){ return s==null?'':String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
  function debounce(fn,ms){ let t; return function(){ clearTimeout(t); t=setTimeout(()=>fn.apply(this,arguments),ms); }; }
  function showSpinner(t){ const el=document.getElementById('ssa-sp'); if(el){el.textContent=t||'…';}else{const s=document.createElement('div');s.id='ssa-sp';s.style='position:fixed;right:16px;bottom:16px;padding:8px 14px;background:rgba(0,0,0,.75);color:#fff;border-radius:6px;z-index:9999;font-size:13px';s.textContent=t||'…';document.body.appendChild(s);} }
  function hideSpinner(){ const e=document.getElementById('ssa-sp');if(e)e.remove(); }
  function download(name,content,type){ const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([content],{type}));a.download=name;a.click();URL.revokeObjectURL(a.href); }

  function statusBadge(s){
    const code=Number(s)||0;
    const cls=code>=500?'sb-5xx':code>=400?'sb-4xx':code>=300?'sb-3xx':code>=200?'sb-2xx':'sb-unk';
    return `<span class="sb ${cls}">${esc(String(s||'—'))}</span>`;
  }
  function methodBadge(m){
    const v=(m||'').toUpperCase();
    const cls=v==='GET'?'mb-get':v==='POST'?'mb-post':v==='PUT'?'mb-put':v==='DELETE'?'mb-del':v==='PATCH'?'mb-pat':v==='HEAD'?'mb-hd':'mb-oth';
    return `<span class="mb ${cls}">${esc(v||'—')}</span>`;
  }
  function latencyBadge(ms){
    if(ms==null||ms==='') return '<span class="lb lb-ok">—</span>';
    const n=Number(ms);
    const cls=n<100?'lb-fast':n<500?'lb-ok':n<2000?'lb-slow':'lb-vslow';
    return `<span class="lb ${cls}">${n.toLocaleString()} ms</span>`;
  }
  function urlCell(u){
    const safe=esc(u||'');
    return `<div class="url-cell"><span class="url-txt" title="${safe}">${safe}</span><button class="copy-btn" onclick="navigator.clipboard.writeText(decodeURIComponent('${encodeURIComponent(u||'')}'))">copy</button></div>`;
  }
  function clickable(s){
    const text=String(s||'');
    const short=MAXF>0&&text.length>MAXF?esc(text.slice(0,MAXF))+'…':esc(text);
    return `<div class="cell-content" data-full="${encodeURIComponent(text)}">${short}</div>`;
  }
  function isImg(u){ if(!u)return false; const s=String(u); return s.startsWith('data:image/')||/\.(png|jpe?g|gif|webp|bmp)$/i.test(s); }
  function bodyCell(s){
    if(isImg(s)) return `<img src="${esc(s)}" class="thumb view-media" data-full="${encodeURIComponent(s)}" loading="lazy"/>`;
    return clickable(s);
  }

  // ── Fetch data ────────────────────────────────────────────────────────────
  showSpinner('Loading data.json…');
  fetch('data.json')
    .then(r=>{ if(!r.ok) throw new Error('HTTP '+r.status); return r.json(); })
    .then(init)
    .catch(err=>{
      hideSpinner();
      document.getElementById('load-msg').textContent='Failed to load data.json: '+err.message+'. Serve the folder via HTTP.';
    });

  function init(allData){
    hideSpinner();
    document.getElementById('load-msg').style.display='none';
    document.getElementById('gen-time').textContent='Loaded '+allData.length.toLocaleString()+' rows · '+new Date().toLocaleTimeString();

    // Stats
    const errCount = allData.filter(d=>{ const c=Number(d.Status); return c>=400; }).length;
    const lats = allData.map(d=>Number(d.LatencyMs)).filter(n=>!isNaN(n)&&n>=0);
    const avgLat = lats.length ? Math.round(lats.reduce((a,b)=>a+b,0)/lats.length) : null;
    document.getElementById('s-total').textContent = allData.length.toLocaleString();
    document.getElementById('s-err'  ).textContent = errCount.toLocaleString();
    document.getElementById('s-lat'  ).textContent = avgLat!=null ? avgLat.toLocaleString()+' ms' : '—';

    // Populate method/resource dropdowns
    const fMethod   = document.getElementById('f-method');
    const fResource = document.getElementById('f-resource');
    [...new Set(allData.map(d=>d.RequestMethod||'').filter(Boolean))].sort()
      .forEach(m=>fMethod.insertAdjacentHTML('beforeend',`<option value="${esc(m)}">${esc(m)}</option>`));
    [...new Set(allData.map(d=>d.ResourceType||'').filter(Boolean))].sort()
      .forEach(r=>fResource.insertAdjacentHTML('beforeend',`<option value="${esc(r)}">${esc(r)}</option>`));

    // Page size selector
    const fPagesize = document.getElementById('f-pagesize');
    [10,25,50,100,250].forEach(n=>{
      const o=document.createElement('option'); o.value=n; o.text=n+' / page';
      if(n===PGSZ_DEFAULT) o.selected=true;
      fPagesize.appendChild(o);
    });

    let pageSize = PGSZ_DEFAULT;
    let currentPage = 1;
    let filtered = allData.slice();
    let activeSc = 'all';
    const selected = new Set();

    // ── gridjs ───────────────────────────────────────────────────────────────
    const columns = [
      { id:'sel',    name:'',            sort:false, width:'36px',
        formatter:()=>gridjs.html('<input type="checkbox" class="row-select"/>') },
      { id:'url',    name:'URL',         sort:false,
        formatter:c=>gridjs.html(urlCell(c)) },
      { id:'method', name:'Method',      formatter:c=>gridjs.html(methodBadge(c)) },
      { id:'status', name:'Status',      formatter:c=>gridjs.html(statusBadge(c)) },
      { id:'lat',    name:'Latency',     formatter:c=>gridjs.html(latencyBadge(c)) },
      { id:'res',    name:'Resource' },
      { id:'reqTs',  name:'Req Time' },
      { id:'respTs', name:'Resp Time' },
      { id:'reqH',   name:'Req Headers', sort:false, formatter:c=>gridjs.html(clickable(c)) },
      { id:'reqB',   name:'Req Body',    sort:false, formatter:c=>gridjs.html(bodyCell(c)) },
      { id:'respH',  name:'Resp Headers',sort:false, formatter:c=>gridjs.html(clickable(c)) },
      { id:'respB',  name:'Resp Body',   sort:false, formatter:c=>gridjs.html(bodyCell(c)) }
    ];

    const grid = new gridjs.Grid({
      columns, data:[], sort:true, search:false,
      pagination:{enabled:true,limit:pageSize,summary:true},
      fixedHeader:true, height:'62vh'
    }).render(document.getElementById('grid'));

    function toRow(o){
      return ['', o.RequestUrl, o.RequestMethod, o.Status, o.LatencyMs,
              o.ResourceType, o.RequestTimestamp, o.ResponseTimestamp,
              o.RequestHeaders, o.RequestBody, o.ResponseHeaders, o.ResponseBody];
    }

    function renderPage(){
      showSpinner('Rendering…');
      const start=(currentPage-1)*pageSize;
      const slice=filtered.slice(start,start+pageSize);
      const schedule=window.requestIdleCallback||function(cb){return setTimeout(cb,40);};
      schedule(()=>{
        grid.updateConfig({data:slice.map(toRow),pagination:{enabled:true,limit:pageSize}}).forceRender();
        // Attach checkbox data-idx and selected state after render
        setTimeout(()=>{
          const tbody=document.querySelector('.gridjs-table tbody'); if(!tbody){hideSpinner();return;}
          Array.from(tbody.querySelectorAll('tr')).forEach((tr,i)=>{
            const obj=slice[i]; if(!obj) return;
            tr.dataset.idx=obj.__index;
            const cb=tr.querySelector('.row-select');
            if(cb){ cb.setAttribute('data-idx',obj.__index); cb.checked=selected.has(obj.__index); }
          });
          hideSpinner();
        },80);
      });
    }

    function applyFilters(){
      showSpinner('Filtering…');
      setTimeout(()=>{
        const q    = (document.getElementById('f-search').value||'').toLowerCase();
        const meth = fMethod.value;
        const res  = fResource.value;
        filtered = allData.filter(d=>{
          if(activeSc!=='all'){
            const c=Number(d.Status)||0;
            if(activeSc==='2'&&(c<200||c>=300)) return false;
            if(activeSc==='3'&&(c<300||c>=400)) return false;
            if(activeSc==='4'&&(c<400||c>=500)) return false;
            if(activeSc==='5'&&c<500)           return false;
          }
          if(meth && d.RequestMethod!==meth)   return false;
          if(res  && d.ResourceType!==res)     return false;
          if(q){
            const hay=((d.RequestUrl||'')+' '+(d.RequestHeaders||'')+' '+(d.ResponseHeaders||'')+' '+(d.RequestBody||'')+' '+(d.ResponseBody||'')).toLowerCase();
            if(!hay.includes(q)) return false;
          }
          return true;
        });
        document.getElementById('s-show').textContent = filtered.length.toLocaleString();
        currentPage=1;
        renderPager();
        renderPage();
        hideSpinner();
      },120);
    }

    function renderPager(){
      const total=Math.max(1,Math.ceil(filtered.length/pageSize));
      const pager=document.getElementById('pager');
      pager.innerHTML='';
      function btn(txt,cb,dis){
        const li=document.createElement('li'); li.className='page-item'+(dis?' disabled':'');
        const a=document.createElement('a');   a.className='page-link'; a.href='#';
        a.textContent=txt; a.onclick=e=>{e.preventDefault();if(!dis)cb();};
        li.appendChild(a); pager.appendChild(li);
      }
      btn('⏮',()=>{currentPage=1;renderPage();renderPager();},currentPage===1);
      btn('‹', ()=>{if(currentPage>1){currentPage--;renderPage();renderPager();}},currentPage===1);
      const lo=Math.max(1,currentPage-2), hi=Math.min(total,currentPage+2);
      for(let p=lo;p<=hi;p++) btn(String(p),()=>{currentPage=p;renderPage();renderPager();},p===currentPage);
      btn('›',()=>{if(currentPage<total){currentPage++;renderPage();renderPager();}},currentPage===total);
      btn('⏭',()=>{currentPage=total;renderPage();renderPager();},currentPage===total);
      document.getElementById('pageInfo').textContent=
        `Page ${currentPage} of ${total} — ${filtered.length.toLocaleString()} rows`;
    }

    // ── Status class pills ────────────────────────────────────────────────────
    document.querySelectorAll('.sc-btn').forEach(btn=>{
      btn.addEventListener('click',()=>{
        activeSc=btn.getAttribute('data-sc');
        document.querySelectorAll('.sc-btn').forEach(b=>b.classList.remove('active'));
        btn.classList.add('active');
        applyFilters();
      });
    });

    // ── Filter wiring ─────────────────────────────────────────────────────────
    fMethod  .addEventListener('change', applyFilters);
    fResource.addEventListener('change', applyFilters);
    fPagesize.addEventListener('change', ()=>{ pageSize=Number(fPagesize.value); currentPage=1; renderPage(); renderPager(); });
    document.getElementById('f-search').addEventListener('input', debounce(applyFilters,200));
    document.getElementById('f-clear').addEventListener('click',()=>{
      activeSc='all';
      document.querySelectorAll('.sc-btn').forEach(b=>b.classList.remove('active'));
      document.querySelector('.sc-btn[data-sc="all"]').classList.add('active');
      fMethod.value=''; fResource.value='';
      document.getElementById('f-search').value='';
      applyFilters();
    });
    document.getElementById('sel-all').addEventListener('change', function(){
      const checks=document.querySelectorAll('.row-select');
      checks.forEach(cb=>{
        cb.checked=this.checked;
        const idx=Number(cb.getAttribute('data-idx'));
        if(this.checked) selected.add(idx); else selected.delete(idx);
      });
    });
    document.body.addEventListener('change',ev=>{
      const cb=ev.target.closest('.row-select'); if(!cb) return;
      const idx=Number(cb.getAttribute('data-idx'));
      if(cb.checked) selected.add(idx); else selected.delete(idx);
    });

    // ── Detail modal on cell click ────────────────────────────────────────────
    document.body.addEventListener('click',ev=>{
      const cell=ev.target.closest('.cell-content');
      if(!cell) return;
      ev.preventDefault();
      const full=decodeURIComponent(cell.getAttribute('data-full')||'');
      const tabsEl=document.getElementById('modal-tabs');
      const bodyEl=document.getElementById('modal-tab-content');
      tabsEl.innerHTML=''; bodyEl.innerHTML='';
      // Try JSON pretty-print
      let rendered;
      try{ rendered='<pre style="margin:0;white-space:pre-wrap;word-break:break-word">'+esc(JSON.stringify(JSON.parse(full),null,2))+'</pre>'; }
      catch{ rendered='<pre style="margin:0;white-space:pre-wrap;word-break:break-word">'+esc(full)+'</pre>'; }
      tabsEl.insertAdjacentHTML('beforeend','<li class="nav-item"><a class="nav-link active" href="#">Content</a></li>');
      bodyEl.insertAdjacentHTML('beforeend','<div class="tab-pane show active">'+rendered+'</div>');
      document.getElementById('modal-title').textContent='Full content';
      new bootstrap.Modal(document.getElementById('detailModal')).show();
    });

    // ── Exports ───────────────────────────────────────────────────────────────
    document.getElementById('downloadFiltered').addEventListener('click',()=>{
      download('filtered-report.json',JSON.stringify(filtered,null,2),'application/json');
    });
    document.getElementById('exportSelected').addEventListener('click',()=>{
      if(!selected.size){ alert('No rows selected'); return; }
      const objs=allData.filter(o=>selected.has(o.__index));
      download('selected-report.json',JSON.stringify(objs,null,2),'application/json');
    });

    // ── Keyboard shortcuts ────────────────────────────────────────────────────
    document.addEventListener('keydown',e=>{
      if(e.key==='/'&&!['INPUT','SELECT','TEXTAREA'].includes(document.activeElement.tagName)){
        e.preventDefault(); document.getElementById('f-search').focus();
      }
      if(e.key==='Escape'){ document.getElementById('f-search').value=''; applyFilters(); }
    });

    // ── Initial render ────────────────────────────────────────────────────────
    document.getElementById('s-show').textContent = allData.length.toLocaleString();
    renderPager();
    renderPage();
  }
})();
</script>
</body>
</html>
""";
}
}
