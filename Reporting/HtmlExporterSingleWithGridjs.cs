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
    /// Single-file HTML network report with coloured badges, stats bar,
    /// status-class filter pills, tabbed detail modal, and keyboard shortcuts.
    /// </summary>
    public static class HtmlExporterSingleWithGridjs
    {
        // ── Public API (signature unchanged) ─────────────────────────────────
        public static string ExportNetworkInfoToSingleHtml(
            IEnumerable<FullNetworkInfo> items,
            string outFolderRoot,
            string? reportName = null,
            int pageSize = 50,
            int maxFieldLength = 200)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot)) throw new ArgumentNullException(nameof(outFolderRoot));
            Directory.CreateDirectory(outFolderRoot);

            var safeName = string.IsNullOrWhiteSpace(reportName) ? "report" : MakeSafeFileName(reportName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filePath = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}", ".html");

            var rows = (items ?? Enumerable.Empty<FullNetworkInfo>()).Select((e, idx) => new
            {
                __index = idx,
                RequestId = e.RequestId ?? "",
                RequestUrl = e.RequestUrl ?? "",
                RequestMethod = e.RequestMethod ?? "",
                RequestTimestamp = e.RequestTimestamp?.ToString("o") ?? "",
                ResponseTimestamp = e.ResponseTimestamp?.ToString("o") ?? "",
                LatencyMs = e.LatencyMs ??
                    (e.RequestTimestamp.HasValue && e.ResponseTimestamp.HasValue
                        ? (long?)(e.ResponseTimestamp.Value - e.RequestTimestamp.Value).TotalMilliseconds
                        : null),
                Status = e.ResponseStatusCode,
                RequestHeaders = DictToString(e.RequestHeaders),
                ResponseHeaders = DictToString(e.ResponseHeaders),
                RequestBody = e.RequestPostData ?? "",
                ResponseBody = e.ResponseBody ?? "",
                ResourceType = e.ResponseResourceType ?? ""
            }).ToList();

            var jsonOpts = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string dataJson = JsonSerializer.Serialize(rows, jsonOpts)
                .Replace("</script", @"<\/script", StringComparison.OrdinalIgnoreCase);

            var html = BuildHtml(dataJson, Path.GetFileName(filePath), pageSize, maxFieldLength, rows.Count);
            File.WriteAllText(filePath, html, new UTF8Encoding(false));
            return Path.GetFullPath(filePath);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string DictToString(IDictionary<string, string>? d)
            => d == null || d.Count == 0 ? "" : string.Join("\n", d.Select(kv => $"{kv.Key}: {kv.Value}"));

        private static string MakeSafeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            s = string.Join("-", s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            while (s.Contains("--")) s = s.Replace("--", "-");
            return s.Trim('-');
        }

        private static string GetUniqueFilePath(string folder, string baseName, string ext)
        {
            var candidate = Path.Combine(folder, baseName + ext);
            if (!File.Exists(candidate)) return candidate;
            for (int i = 1; i < 1000; i++)
            {
                var alt = Path.Combine(folder, $"{baseName}-{i}{ext}");
                if (!File.Exists(alt)) return alt;
            }
            return Path.Combine(folder, $"{baseName}-{Guid.NewGuid():N}{ext}");
        }

        // ── HTML builder ──────────────────────────────────────────────────────
        private static string BuildHtml(
            string dataJson, string filename, int pageSize, int maxFieldLength, int totalRows)
        {
            return GetTemplate()
                .Replace("[[DATA]]", dataJson)
                .Replace("[[PGSZ]]", pageSize.ToString())
                .Replace("[[MAXF]]", maxFieldLength.ToString())
                .Replace("[[TITLE]]", System.Net.WebUtility.HtmlEncode(filename))
                .Replace("[[GEN]]", DateTime.UtcNow.ToString("u"))
                .Replace("[[TOTAL]]", totalRows.ToString());
        }

        // Raw string literal — JS braces/template literals need no escaping here
        private static string GetTemplate() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>Network Report — [[TITLE]]</title>
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" crossorigin="anonymous"/>
<link href="https://unpkg.com/gridjs/dist/theme/mermaid.min.css" rel="stylesheet"/>
<style>
:root{--c-2xx:#198754;--c-3xx:#0d6efd;--c-4xx:#fd7e14;--c-5xx:#dc3545}
body{background:#f5f6f8;font-size:13px;margin:0}
#topbar{background:#1e293b;color:#f1f5f9;padding:9px 20px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:100}
#topbar h1{font-size:15px;margin:0;font-weight:600}
#topbar .meta{font-size:11px;color:#94a3b8}
#statsBar{display:flex;gap:10px;padding:10px 20px;background:#fff;border-bottom:1px solid #e2e8f0;flex-wrap:wrap}
.stat-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 16px;min-width:120px;text-align:center}
.stat-val{font-size:20px;font-weight:700;line-height:1.2}
.stat-lbl{font-size:10px;color:#64748b;margin-top:2px;text-transform:uppercase;letter-spacing:.4px}
.stat-card.has-errors{border-color:#fca5a5;background:#fff5f5}
.stat-card.has-errors .stat-val{color:#dc3545}
#filterBar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:8px 20px;background:#fff;border-bottom:1px solid #e2e8f0}
.sc-pills{display:flex;gap:3px}
.sc-btn{border:1px solid #cbd5e1;background:#fff;border-radius:20px;padding:2px 11px;font-size:11px;font-weight:500;cursor:pointer;transition:.12s}
.sc-btn:hover{background:#f1f5f9}
.sc-btn.active{background:#1e293b;color:#fff;border-color:#1e293b}
.sc-btn.s2xx.active{background:var(--c-2xx);border-color:var(--c-2xx)}
.sc-btn.s3xx.active{background:var(--c-3xx);border-color:var(--c-3xx)}
.sc-btn.s4xx.active{background:var(--c-4xx);border-color:var(--c-4xx)}
.sc-btn.s5xx.active{background:var(--c-5xx);border-color:var(--c-5xx)}
#gridWrap{padding:0 20px 4px}
.gridjs-container{font-size:12px}
.gridjs-td{padding:4px 8px!important;max-width:280px;overflow:hidden;text-overflow:ellipsis;white-space:pre-wrap;word-break:break-word}
/* badges */
.sbadge,.mbadge,.lbadge{display:inline-block;padding:1px 7px;border-radius:12px;font-size:11px;font-weight:600;white-space:nowrap;line-height:1.6}
.s2xx{background:#d1fae5;color:#065f46}.s3xx{background:#dbeafe;color:#1e40af}
.s4xx{background:#ffedd5;color:#9a3412}.s5xx{background:#fee2e2;color:#991b1b}.sunk{background:#f1f5f9;color:#475569}
.mget{background:#dbeafe;color:#1e40af}.mpost{background:#d1fae5;color:#065f46}
.mput{background:#ffedd5;color:#9a3412}.mdel{background:#fee2e2;color:#991b1b}
.mpatch{background:#ede9fe;color:#5b21b6}.mopt,.moth{background:#f1f5f9;color:#475569}
.lfast{background:#d1fae5;color:#065f46}.lok{background:#fef9c3;color:#713f12}
.lslow{background:#ffedd5;color:#9a3412}.lvery{background:#fee2e2;color:#991b1b}.lunk{color:#94a3b8}
/* URL cell */
.url-wrap{display:flex;align-items:center;gap:4px}
.url-text{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1;cursor:pointer;color:#1d4ed8;font-size:12px}
.url-text:hover{text-decoration:underline}
.copy-btn{flex-shrink:0;background:none;border:none;cursor:pointer;color:#94a3b8;padding:0 3px;opacity:0;transition:.12s;font-size:13px}
.url-wrap:hover .copy-btn{opacity:1}
/* expand cell */
.cell-ex{cursor:pointer;color:#374151}
.cell-ex:hover{color:#1d4ed8;text-decoration:underline}
/* pager */
#pagerBar{display:flex;align-items:center;justify-content:space-between;padding:6px 20px;background:#fff;border-top:1px solid #e2e8f0;position:sticky;bottom:0;z-index:50}
#pagerBar .pagination{margin:0}
#pageInfo{font-size:11px;color:#64748b}
/* modal */
.modal-tabs{display:flex;gap:2px;padding:6px 14px;background:#f8fafc;border-bottom:1px solid #e2e8f0}
.tab-btn{border:none;background:none;border-radius:6px;padding:3px 13px;font-size:12px;cursor:pointer;color:#475569;font-weight:500}
.tab-btn.active{background:#1e293b;color:#fff}
#modal-area{padding:14px;min-height:180px;max-height:58vh;overflow:auto}
.detail-tbl{width:100%;border-collapse:collapse;font-size:12px}
.detail-tbl th{width:110px;padding:5px 9px;background:#f8fafc;border:1px solid #e2e8f0;font-weight:600;color:#475569;text-align:left;white-space:nowrap}
.detail-tbl td{padding:5px 9px;border:1px solid #e2e8f0;word-break:break-all}
.hdr-tbl{width:100%;border-collapse:collapse;font-size:12px}
.hdr-tbl th{background:#f8fafc;border:1px solid #e2e8f0;padding:4px 8px;text-align:left;color:#475569}
.hdr-tbl td{border:1px solid #e2e8f0;padding:4px 8px;word-break:break-all}
.hkey{font-family:monospace;color:#1e40af;white-space:nowrap;font-weight:600}
.body-pre{background:#1e293b;color:#e2e8f0;padding:12px;border-radius:6px;font-size:11px;overflow:auto;max-height:380px;white-space:pre-wrap;word-break:break-all;margin:0}
kbd{background:#e2e8f0;border:1px solid #cbd5e1;border-radius:3px;padding:1px 5px;font-size:10px}
</style>
</head>
<body>

<div id="topbar">
  <h1>&#127760; Network Report</h1>
  <span class="meta">[[TITLE]] &nbsp;&middot;&nbsp; Generated [[GEN]]</span>
  <div style="margin-left:auto;display:flex;gap:6px">
    <button id="btnDlJson" class="btn btn-sm btn-outline-light py-0">&#11015; JSON</button>
    <button id="btnDlSel" class="btn btn-sm btn-outline-light py-0">&#11015; Selected</button>
  </div>
</div>

<div id="statsBar">
  <div class="stat-card"><div class="stat-val" id="st-total">[[TOTAL]]</div><div class="stat-lbl">Total</div></div>
  <div class="stat-card" id="st-errors-card"><div class="stat-val" id="st-errors">—</div><div class="stat-lbl">Errors 4xx/5xx</div></div>
  <div class="stat-card"><div class="stat-val" id="st-lat">—</div><div class="stat-lbl">Avg Latency</div></div>
  <div class="stat-card"><div class="stat-val" id="st-showing">—</div><div class="stat-lbl">Showing</div></div>
  <div style="margin-left:auto;font-size:11px;color:#94a3b8;align-self:center">
    <kbd>/</kbd> search &nbsp;<kbd>Esc</kbd> clear
  </div>
</div>

<div id="filterBar">
  <input id="globalSearch" class="form-control form-control-sm" placeholder="Search all fields…" style="width:230px"/>
  <div class="sc-pills">
    <button class="sc-btn active" data-sc="">All</button>
    <button class="sc-btn s2xx" data-sc="2">2xx</button>
    <button class="sc-btn s3xx" data-sc="3">3xx</button>
    <button class="sc-btn s4xx" data-sc="4">4xx</button>
    <button class="sc-btn s5xx" data-sc="5">5xx</button>
  </div>
  <select id="methodFilter" class="form-select form-select-sm" style="width:130px"><option value="">All methods</option></select>
  <select id="resourceFilter" class="form-select form-select-sm" style="width:145px"><option value="">All resources</option></select>
  <button id="btnClear" class="btn btn-sm btn-outline-secondary py-0">&#10005; Clear</button>
  <div style="margin-left:auto;display:flex;align-items:center;gap:10px">
    <label class="d-flex align-items-center gap-1" style="font-size:12px;cursor:pointer">
      <input type="checkbox" id="hdrCb" class="form-check-input m-0"/> Select all
    </label>
    <select id="pgSzSel" class="form-select form-select-sm" style="width:110px"></select>
  </div>
</div>

<div id="gridWrap"><div id="grid"></div></div>

<div id="pagerBar">
  <span id="pageInfo"></span>
  <ul class="pagination pagination-sm" id="pager"></ul>
</div>

<!-- Detail modal -->
<div class="modal fade" id="detailModal" tabindex="-1">
  <div class="modal-dialog modal-xl modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header py-2">
        <h6 class="modal-title text-truncate" id="modal-url" style="max-width:680px;font-size:13px"></h6>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-tabs">
        <button class="tab-btn active" data-tab="overview">Overview</button>
        <button class="tab-btn" data-tab="reqHeaders">Req Headers</button>
        <button class="tab-btn" data-tab="reqBody">Req Body</button>
        <button class="tab-btn" data-tab="respHeaders">Resp Headers</button>
        <button class="tab-btn" data-tab="respBody">Resp Body</button>
      </div>
      <div id="modal-area"></div>
    </div>
  </div>
</div>

<script>window.__d=[[DATA]];window.__ps=[[PGSZ]];window.__mf=[[MAXF]];</script>
<script src="https://unpkg.com/gridjs/dist/gridjs.umd.js"></script>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js" crossorigin="anonymous"></script>
<script>
(function () {
  'use strict';
  const allData = window.__d || [];
  const maxField = window.__mf || 200;
  let pageSize = window.__ps || 50;
  let filtered = allData.slice();
  let page = 1;
  const sel = new Set();
  let modalTabs = {};

  const $ = id => document.getElementById(id);

  // ── Formatters ─────────────────────────────────────────────────────────────
  function esc(s) { return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }
  function escA(s) { return esc(s).replace(/"/g,'&quot;'); }

  function trunc(s) {
    const t = String(s??'');
    return t.length <= maxField ? esc(t) : esc(t.slice(0,maxField))+'<span style="color:#94a3b8">\u2026</span>';
  }

  function statusBadge(code) {
    const n = Number(code)||0;
    const c = n>=500?'s5xx':n>=400?'s4xx':n>=300?'s3xx':n>=200?'s2xx':'sunk';
    return `<span class="sbadge ${c}">${esc(code)||'—'}</span>`;
  }

  function methodBadge(m) {
    const u = String(m||'').toUpperCase();
    const map = {GET:'mget',POST:'mpost',PUT:'mput',DELETE:'mdel',PATCH:'mpatch',OPTIONS:'mopt',HEAD:'moth'};
    return `<span class="mbadge ${map[u]||'moth'}">${esc(m)||'—'}</span>`;
  }

  function latencyBadge(ms) {
    if (ms==null||ms==='') return '<span class="lbadge lunk">—</span>';
    const n = Number(ms);
    const c = n<100?'lfast':n<500?'lok':n<2000?'lslow':'lvery';
    return `<span class="lbadge ${c}">${n} ms</span>`;
  }

  function urlCell(url, idx) {
    const short = url.length>80 ? url.slice(0,77)+'\u2026' : url;
    return `<div class="url-wrap"><span class="url-text" data-idx="${idx}" title="${escA(url)}">${esc(short)}</span>`
         + `<button class="copy-btn" data-url="${escA(url)}" title="Copy URL">\u2398</button></div>`;
  }

  function expandCell(text, idx, field) {
    const s = String(text??'');
    if (!s) return '<span style="color:#cbd5e1">—</span>';
    return `<span class="cell-ex" data-idx="${idx}" data-field="${field}">${trunc(s)}</span>`;
  }

  // ── Stats ──────────────────────────────────────────────────────────────────
  function updateStats() {
    const errors = allData.filter(d=>Number(d.Status)>=400).length;
    const lats = allData.map(d=>d.LatencyMs).filter(x=>x!=null).map(Number);
    const avg = lats.length ? Math.round(lats.reduce((a,b)=>a+b,0)/lats.length) : null;
    $('st-total').textContent = allData.length;
    $('st-errors').textContent = errors;
    $('st-errors-card').classList.toggle('has-errors', errors>0);
    $('st-lat').textContent = avg!=null ? avg+' ms' : '—';
  }

  function updateShowing() {
    $('st-showing').textContent = filtered.length===allData.length
      ? allData.length : `${filtered.length} / ${allData.length}`;
  }

  // ── Filters ────────────────────────────────────────────────────────────────
  function populateFilters() {
    const methods = [...new Set(allData.map(d=>d.RequestMethod).filter(Boolean))].sort();
    $('methodFilter').innerHTML = '<option value="">All methods</option>'
      + methods.map(m=>`<option>${esc(m)}</option>`).join('');
    const resources = [...new Set(allData.map(d=>d.ResourceType).filter(Boolean))].sort();
    $('resourceFilter').innerHTML = '<option value="">All resources</option>'
      + resources.map(r=>`<option>${esc(r)}</option>`).join('');
  }

  function applyFilters() {
    const q = ($('globalSearch').value||'').toLowerCase();
    const sc = document.querySelector('.sc-btn.active')?.dataset.sc||'';
    const method = $('methodFilter').value.toLowerCase();
    const resource = $('resourceFilter').value.toLowerCase();
    filtered = allData.filter(d => {
      if (sc && !String(d.Status||'').startsWith(sc)) return false;
      if (method && String(d.RequestMethod||'').toLowerCase()!==method) return false;
      if (resource && String(d.ResourceType||'').toLowerCase()!==resource) return false;
      if (!q) return true;
      return (d.RequestUrl+' '+d.RequestMethod+' '+d.Status+' '
             +d.RequestHeaders+' '+d.RequestBody+' '
             +d.ResponseHeaders+' '+d.ResponseBody).toLowerCase().includes(q);
    });
    page=1; renderPage(); renderPager(); updateShowing();
  }

  // ── Grid ───────────────────────────────────────────────────────────────────
  // Data layout per row: [__index, Status, Method, Url, ReqTime, Latency, Resource,
  //                       ReqHeaders, ReqBody, RespHeaders, RespBody]
  // cells[0].data = __index, used by other formatters via row parameter
  const columns = [
    { id:'idx', name:gridjs.html('<input type="checkbox" id="hdrCb" title="Select all on page"/>'),
      sort:false, width:36,
      formatter: c => gridjs.html(`<input type="checkbox" class="row-cb" data-idx="${c}"/>`) },
    { id:'Status', name:'Status', width:74, formatter: c=>gridjs.html(statusBadge(c)) },
    { id:'Method', name:'Method', width:80, formatter: c=>gridjs.html(methodBadge(c)) },
    { id:'Url',    name:'URL',    formatter:(c,row)=>gridjs.html(urlCell(c,row.cells[0].data)) },
    { id:'ReqTime',  name:'Req Time',  width:155 },
    { id:'Latency',  name:'Latency',   width:88, formatter:c=>gridjs.html(latencyBadge(c)) },
    { id:'Resource', name:'Resource',  width:100 },
    { id:'ReqHdr',  name:'Req Headers', formatter:(c,row)=>gridjs.html(expandCell(c,row.cells[0].data,'RequestHeaders')) },
    { id:'ReqBody', name:'Req Body',    formatter:(c,row)=>gridjs.html(expandCell(c,row.cells[0].data,'RequestBody')) },
    { id:'RespHdr', name:'Resp Headers',formatter:(c,row)=>gridjs.html(expandCell(c,row.cells[0].data,'ResponseHeaders')) },
    { id:'RespBody',name:'Resp Body',   formatter:(c,row)=>gridjs.html(expandCell(c,row.cells[0].data,'ResponseBody')) },
  ];

  const grid = new gridjs.Grid({ columns, data:[], sort:true, search:false, fixedHeader:true })
    .render($('grid'));

  function renderPage() {
    const start=(page-1)*pageSize;
    const slice=filtered.slice(start,start+pageSize);
    const data=slice.map(o=>[
      o.__index, o.Status, o.RequestMethod, o.RequestUrl,
      o.RequestTimestamp, o.LatencyMs, o.ResourceType,
      o.RequestHeaders, o.RequestBody, o.ResponseHeaders, o.ResponseBody
    ]);
    const sched = window.requestIdleCallback||(cb=>setTimeout(cb,50));
    sched(()=>{
      grid.updateConfig({data}).forceRender();
      // Re-check selection state after re-render
      setTimeout(()=>{
        document.querySelectorAll('.row-cb').forEach(cb=>{
          cb.checked = sel.has(Number(cb.dataset.idx));
        });
      }, 80);
    });
  }

  // ── Pager ──────────────────────────────────────────────────────────────────
  function renderPager() {
    const total=Math.max(1,Math.ceil(filtered.length/pageSize));
    const pager=$('pager');
    pager.innerHTML='';
    function btn(html,cb,disabled,active) {
      const li=document.createElement('li');
      li.className='page-item'+(disabled?' disabled':'')+(active?' active':'');
      const a=document.createElement('a');
      a.className='page-link'; a.href='#'; a.innerHTML=html;
      a.onclick=e=>{e.preventDefault();if(!disabled)cb();};
      li.appendChild(a); pager.appendChild(li);
    }
    btn('&laquo;',()=>{page=1;renderPage();renderPager();},page===1);
    btn('&lsaquo;',()=>{page--;renderPage();renderPager();},page===1);
    const s=Math.max(1,page-2),e=Math.min(total,page+2);
    for(let p=s;p<=e;p++) btn(p,()=>{page=p;renderPage();renderPager();},false,p===page);
    btn('&rsaquo;',()=>{page++;renderPage();renderPager();},page===total);
    btn('&raquo;',()=>{page=total;renderPage();renderPager();},page===total);
    $('pageInfo').textContent=`Page ${page} of ${total} — ${filtered.length} rows`;
  }

  // ── Detail modal ───────────────────────────────────────────────────────────
  function headersTable(str) {
    if (!str) return '<p class="text-muted small p-2">No headers</p>';
    const rows=str.split('\n').filter(Boolean).map(l=>{
      const i=l.indexOf(':');
      if(i<0) return `<tr><td colspan="2">${esc(l)}</td></tr>`;
      return `<tr><td class="hkey">${esc(l.slice(0,i).trim())}</td><td>${esc(l.slice(i+1).trim())}</td></tr>`;
    });
    return `<table class="hdr-tbl"><thead><tr><th>Header</th><th>Value</th></tr></thead><tbody>${rows.join('')}</tbody></table>`;
  }

  function bodyPanel(str) {
    if (!str) return '<p class="text-muted small p-2">No body</p>';
    let content=str, tag='Text', tagCls='bg-secondary';
    try { content=JSON.stringify(JSON.parse(str),null,2); tag='JSON'; tagCls='bg-info text-dark'; } catch {}
    const enc=encodeURIComponent(content);
    return `<div style="padding:10px"><div style="display:flex;align-items:center;gap:8px;margin-bottom:8px">`
         + `<span class="badge ${tagCls}">${tag}</span>`
         + `<button class="btn btn-sm btn-outline-secondary py-0" onclick="navigator.clipboard&&navigator.clipboard.writeText(decodeURIComponent('${enc}'))">Copy</button></div>`
         + `<pre class="body-pre">${esc(content)}</pre></div>`;
  }

  function openDetail(idx) {
    const obj=allData.find(d=>d.__index===idx);
    if(!obj) return;
    $('modal-url').textContent=obj.RequestUrl||'—';
    modalTabs={
      overview:`<div style="padding:14px"><table class="detail-tbl">
        <tr><th>URL</th><td>${esc(obj.RequestUrl)}</td></tr>
        <tr><th>Method</th><td>${methodBadge(obj.RequestMethod)}</td></tr>
        <tr><th>Status</th><td>${statusBadge(obj.Status)}</td></tr>
        <tr><th>Latency</th><td>${latencyBadge(obj.LatencyMs)}</td></tr>
        <tr><th>Req Time</th><td>${esc(obj.RequestTimestamp)}</td></tr>
        <tr><th>Resp Time</th><td>${esc(obj.ResponseTimestamp)}</td></tr>
        <tr><th>Resource</th><td>${esc(obj.ResourceType)}</td></tr>
        <tr><th>Request ID</th><td><code style="font-size:11px">${esc(obj.RequestId)}</code></td></tr>
      </table></div>`,
      reqHeaders: headersTable(obj.RequestHeaders),
      reqBody: bodyPanel(obj.RequestBody),
      respHeaders: headersTable(obj.ResponseHeaders),
      respBody: bodyPanel(obj.ResponseBody),
    };
    activateTab('overview');
    new bootstrap.Modal($('detailModal')).show();
  }

  function activateTab(name) {
    document.querySelectorAll('.tab-btn').forEach(b=>b.classList.toggle('active',b.dataset.tab===name));
    $('modal-area').innerHTML=modalTabs[name]||'';
  }

  // ── Events ─────────────────────────────────────────────────────────────────
  $('globalSearch').addEventListener('input', debounce(applyFilters,250));
  $('methodFilter').addEventListener('change', applyFilters);
  $('resourceFilter').addEventListener('change', applyFilters);

  document.querySelectorAll('.sc-btn').forEach(btn=>btn.addEventListener('click',()=>{
    document.querySelectorAll('.sc-btn').forEach(b=>b.classList.remove('active'));
    btn.classList.add('active');
    applyFilters();
  }));

  $('btnClear').addEventListener('click',()=>{
    $('globalSearch').value=''; $('methodFilter').value=''; $('resourceFilter').value='';
    document.querySelectorAll('.sc-btn').forEach(b=>b.classList.toggle('active',!b.dataset.sc));
    applyFilters();
  });

  [10,25,50,100,250].forEach(n=>{
    const o=document.createElement('option');
    o.value=n; o.textContent=n+' / page'; o.selected=n===pageSize;
    $('pgSzSel').appendChild(o);
  });
  $('pgSzSel').addEventListener('change',e=>{pageSize=Number(e.target.value);page=1;renderPage();renderPager();});

  document.addEventListener('change',e=>{
    if(e.target.id==='hdrCb'){
      document.querySelectorAll('.row-cb').forEach(cb=>{
        cb.checked=e.target.checked;
        const idx=Number(cb.dataset.idx);
        e.target.checked?sel.add(idx):sel.delete(idx);
      });
    } else if(e.target.classList.contains('row-cb')){
      const idx=Number(e.target.dataset.idx);
      e.target.checked?sel.add(idx):sel.delete(idx);
    }
  });

  document.addEventListener('click',e=>{
    const ex=e.target.closest('.cell-ex');
    if(ex){ openDetail(Number(ex.dataset.idx)); return; }
    const ut=e.target.closest('.url-text');
    if(ut){ openDetail(Number(ut.dataset.idx)); return; }
    const cb=e.target.closest('.copy-btn');
    if(cb){
      e.stopPropagation();
      const url=cb.dataset.url;
      navigator.clipboard?.writeText(url).catch(()=>legacyCopy(url));
      cb.textContent='\u2713'; setTimeout(()=>cb.textContent='\u2398',1200);
      return;
    }
    const tb=e.target.closest('.tab-btn');
    if(tb){ activateTab(tb.dataset.tab); return; }
  });

  document.addEventListener('keydown',e=>{
    if(e.key==='/'&&document.activeElement!==$('globalSearch')&&!e.ctrlKey&&!e.metaKey){
      e.preventDefault(); $('globalSearch').focus();
    } else if(e.key==='Escape'&&document.activeElement===$('globalSearch')){
      $('globalSearch').value=''; applyFilters();
    }
  });

  $('btnDlJson').addEventListener('click',()=>dlBlob(JSON.stringify(filtered,null,2),'filtered-report.json'));
  $('btnDlSel').addEventListener('click',()=>{
    if(!sel.size){alert('No rows selected.');return;}
    dlBlob(JSON.stringify(allData.filter(d=>sel.has(d.__index)),null,2),'selected-report.json');
  });

  // ── Helpers ────────────────────────────────────────────────────────────────
  function dlBlob(content,name){
    const url=URL.createObjectURL(new Blob([content],{type:'application/json'}));
    const a=Object.assign(document.createElement('a'),{href:url,download:name});
    document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
  }
  function legacyCopy(text){
    const ta=Object.assign(document.createElement('textarea'),{value:text});
    ta.style.cssText='position:fixed;opacity:0';
    document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove();
  }
  function debounce(fn,ms){let t;return function(){clearTimeout(t);t=setTimeout(()=>fn.apply(this,arguments),ms);};}

  // ── Init ───────────────────────────────────────────────────────────────────
  populateFilters();
  updateStats();
  updateShowing();
  renderPage();
  renderPager();
})();
</script>
</body>
</html>
""";
    }
}
