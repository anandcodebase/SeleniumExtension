using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Visual regression HTML report with stats bar, pass/fail filters,
    /// browser/viewport dropdowns, similarity progress bars, row tinting,
    /// a four-image lightbox, and CSV/JSON export.
    /// </summary>
    public sealed class HtmlGridJsReportExporter
    {
        private readonly List<VisualGridRow> _visualRows = new();

        public void AddVisualResult(VisualGridRow row) => _visualRows.Add(row);

        public void Save(string outputPath)
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(_visualRows, opts)
                .Replace("</script", @"<\/script", StringComparison.OrdinalIgnoreCase);
            var html = GetTemplate().Replace("[[DATA]]", json);
            File.WriteAllText(outputPath, html, new UTF8Encoding(false));
        }

        // Raw string literal — JS template literals / braces need no escaping here.
        private static string GetTemplate() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>Visual Regression Report</title>
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" crossorigin="anonymous"/>
<link href="https://unpkg.com/gridjs/dist/theme/mermaid.min.css" rel="stylesheet"/>
<style>
:root{--pass:#198754;--fail:#dc3545;--warn:#fd7e14}
body{background:#f5f6f8;font-size:13px;margin:0}
#topbar{background:#1e293b;color:#f1f5f9;padding:9px 20px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:100}
#topbar h1{font-size:15px;margin:0;font-weight:600}
#statsBar{display:flex;gap:10px;padding:10px 20px;background:#fff;border-bottom:1px solid #e2e8f0;flex-wrap:wrap}
.sc{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 18px;text-align:center;min-width:110px}
.sc-val{font-size:22px;font-weight:700;line-height:1.2}
.sc-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.4px;margin-top:2px}
.sc-pass .sc-val{color:var(--pass)}.sc-fail .sc-val{color:var(--fail)}.sc-avg .sc-val{color:#0d6efd}
#filterBar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:8px 20px;background:#fff;border-bottom:1px solid #e2e8f0}
.pf-pills{display:flex;gap:4px}
.pf-btn{border:1px solid #cbd5e1;background:#fff;border-radius:20px;padding:2px 12px;font-size:11px;font-weight:600;cursor:pointer;transition:.12s}
.pf-btn:hover{background:#f1f5f9}
.pf-btn.active{background:#1e293b;color:#fff;border-color:#1e293b}
.pf-btn.pb-pass.active{background:var(--pass);border-color:var(--pass)}
.pf-btn.pb-fail.active{background:var(--fail);border-color:var(--fail)}
#main{padding:16px 20px}
.pf-pass{color:var(--pass);font-weight:700}
.pf-fail{color:var(--fail);font-weight:700}
tr.row-pass>td{background:#f0fdf4!important}
tr.row-fail>td{background:#fff5f5!important}
.sim-wrap{position:relative;background:#e2e8f0;border-radius:4px;height:18px;min-width:80px}
.sim-fill{height:100%;border-radius:4px;transition:width .3s}
.sim-lbl{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;color:#1e293b}
.thumb{max-width:72px;max-height:50px;object-fit:cover;border-radius:3px;border:1px solid #e2e8f0;cursor:pointer;transition:transform .15s,border-color .15s}
.thumb:hover{transform:scale(1.12);border-color:#0d6efd}
.no-img{color:#94a3b8;font-size:11px}
.gridjs-container{font-size:13px}
/* Hide the hidden index column */
.gridjs-table td:first-child,.gridjs-table th:first-child{width:0!important;padding:0!important;overflow:hidden!important;font-size:0!important;border:none!important}
/* Lightbox */
#lb-overlay{display:none;position:fixed;inset:0;background:rgba(0,0,0,.84);z-index:2000;align-items:center;justify-content:center}
#lb-overlay.open{display:flex}
#lb-box{background:#fff;border-radius:10px;max-width:95vw;max-height:95vh;display:flex;flex-direction:column;overflow:hidden;min-width:380px;box-shadow:0 8px 40px rgba(0,0,0,.4)}
#lb-head{display:flex;align-items:center;gap:8px;padding:9px 14px;border-bottom:1px solid #e2e8f0}
#lb-title{flex:1;font-weight:600;font-size:14px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#lb-tabs{display:flex;gap:4px}
.lb-tab{padding:3px 11px;border-radius:4px;border:1px solid #cbd5e1;background:#fff;cursor:pointer;font-size:12px;font-weight:500}
.lb-tab.active{background:#0d6efd;color:#fff;border-color:#0d6efd}
.lb-tab:disabled{opacity:.38;cursor:default}
#lb-close{background:none;border:none;font-size:22px;line-height:1;cursor:pointer;color:#64748b;padding:0 2px}
#lb-img-area{flex:1;overflow:auto;display:flex;align-items:center;justify-content:center;padding:16px;background:#f8fafc;min-height:180px}
#lb-img{max-width:100%;max-height:74vh;border-radius:4px;box-shadow:0 2px 16px rgba(0,0,0,.18)}
#lb-no-img{color:#94a3b8;font-style:italic;font-size:13px}
#lb-foot{padding:7px 14px;border-top:1px solid #e2e8f0;font-size:12px;color:#64748b;display:flex;gap:18px;flex-wrap:wrap}
</style>
</head>
<body>
<div id="topbar">
  <h1>Visual Regression Report</h1>
  <span style="color:#94a3b8;font-size:11px" id="gen-time"></span>
  <div style="margin-left:auto;display:flex;gap:8px">
    <button id="btn-csv" class="btn btn-sm btn-outline-light">Export CSV</button>
    <button id="btn-json" class="btn btn-sm btn-outline-light">Export JSON</button>
  </div>
</div>

<div id="statsBar">
  <div class="sc sc-total"><div class="sc-val" id="s-total">0</div><div class="sc-lbl">Total</div></div>
  <div class="sc sc-pass"><div class="sc-val" id="s-pass">0</div><div class="sc-lbl">Pass</div></div>
  <div class="sc sc-fail"><div class="sc-val" id="s-fail">0</div><div class="sc-lbl">Fail</div></div>
  <div class="sc sc-avg"><div class="sc-val" id="s-avg">—</div><div class="sc-lbl">Avg Similarity</div></div>
</div>

<div id="filterBar">
  <div class="pf-pills">
    <button class="pf-btn active" id="f-all">All</button>
    <button class="pf-btn pb-pass" id="f-pass">PASS</button>
    <button class="pf-btn pb-fail" id="f-fail">FAIL</button>
  </div>
  <select id="f-browser" class="form-select form-select-sm" style="width:auto;min-width:120px">
    <option value="">All Browsers</option>
  </select>
  <select id="f-viewport" class="form-select form-select-sm" style="width:auto;min-width:130px">
    <option value="">All Viewports</option>
  </select>
  <input id="f-search" class="form-control form-control-sm" placeholder="Search… (press /)" style="width:200px"/>
  <button id="f-clear" class="btn btn-sm btn-outline-secondary">Clear</button>
  <span class="text-muted small ms-auto" id="showing-lbl"></span>
</div>

<div id="main">
  <div id="grid"></div>
</div>

<!-- Lightbox -->
<div id="lb-overlay">
  <div id="lb-box">
    <div id="lb-head">
      <div id="lb-title"></div>
      <div id="lb-tabs">
        <button class="lb-tab active" data-key="BaselineImage">Baseline</button>
        <button class="lb-tab" data-key="ActualImage">Actual</button>
        <button class="lb-tab" data-key="HeatmapImage">Heatmap</button>
        <button class="lb-tab" data-key="DiffImage">Diff</button>
      </div>
      <button id="lb-close" title="Close (Esc)">×</button>
    </div>
    <div id="lb-img-area">
      <img id="lb-img" src="" alt=""/>
      <div id="lb-no-img" style="display:none">No image available for this view</div>
    </div>
    <div id="lb-foot">
      <span id="lb-sim"></span>
      <span id="lb-result"></span>
      <span id="lb-threshold"></span>
      <span id="lb-bstatus"></span>
    </div>
  </div>
</div>

<script src="https://unpkg.com/gridjs/dist/gridjs.umd.js"></script>
<script>
(function(){
  // ── Helpers ─────────────────────────────────────────────────────────────
  function esc(s){ return s==null?'':String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
  function download(name, content, type){
    const a=document.createElement('a');
    a.href=URL.createObjectURL(new Blob([content],{type}));
    a.download=name; a.click(); URL.revokeObjectURL(a.href);
  }

  // ── Data ────────────────────────────────────────────────────────────────
  const allRows = [[DATA]];
  allRows.forEach((r,i)=>{ if(r.__rowIdx==null) r.__rowIdx=i; });
  const rowLookup = {};
  allRows.forEach(r=>rowLookup[r.__rowIdx]=r);

  // ── Stats bar ───────────────────────────────────────────────────────────
  const total = allRows.length;
  const passCount = allRows.filter(r=>r.Passed).length;
  const failCount = total - passCount;
  const avgSim = total ? (allRows.reduce((s,r)=>s+(r.Similarity||0),0)/total).toFixed(1)+'%' : '—';
  document.getElementById('s-total').textContent = total;
  document.getElementById('s-pass').textContent  = passCount;
  document.getElementById('s-fail').textContent  = failCount;
  document.getElementById('s-avg').textContent   = avgSim;
  document.getElementById('gen-time').textContent = 'Generated '+new Date().toLocaleString();

  // ── Dropdowns ───────────────────────────────────────────────────────────
  const fBrowser  = document.getElementById('f-browser');
  const fViewport = document.getElementById('f-viewport');
  const fSearch   = document.getElementById('f-search');
  [...new Set(allRows.map(r=>r.Browser ).filter(Boolean))].sort()
    .forEach(b=>fBrowser.insertAdjacentHTML('beforeend',`<option value="${esc(b)}">${esc(b)}</option>`));
  [...new Set(allRows.map(r=>r.Viewport).filter(Boolean))].sort()
    .forEach(v=>fViewport.insertAdjacentHTML('beforeend',`<option value="${esc(v)}">${esc(v)}</option>`));

  // ── Filter state ─────────────────────────────────────────────────────────
  let pfFilter = 'all';
  let filtered  = allRows.slice();

  function applyFilters(){
    const q        = fSearch.value.toLowerCase();
    const browser  = fBrowser.value;
    const viewport = fViewport.value;
    filtered = allRows.filter(r=>{
      if(pfFilter==='pass' && !r.Passed) return false;
      if(pfFilter==='fail' &&  r.Passed) return false;
      if(browser  && r.Browser  !== browser)  return false;
      if(viewport && r.Viewport !== viewport) return false;
      if(q){
        const hay=[r.TestId,r.Browser,r.Viewport,r.Reasoning,r.AiReasoning,r.BaselineStatus]
          .filter(Boolean).join(' ').toLowerCase();
        if(!hay.includes(q)) return false;
      }
      return true;
    });
    document.getElementById('showing-lbl').textContent =
      filtered.length===allRows.length ? '' : `Showing ${filtered.length} of ${allRows.length}`;
    renderGrid();
  }

  // ── gridjs formatters ────────────────────────────────────────────────────
  function simFmt(o){
    const w=o.sim.toFixed(1);
    const color=o.passed?'#198754':(o.sim>=80?'#fd7e14':'#dc3545');
    return gridjs.html(
      `<div class="sim-wrap" title="${w}% (threshold ${o.threshold}%)"><div class="sim-fill" style="width:${w}%;background:${color}"></div><div class="sim-lbl">${w}%</div></div>`
    );
  }

  function thumbFmt(src, idx, key){
    if(!src) return gridjs.html('<span class="no-img">—</span>');
    return gridjs.html(`<img src="${esc(src)}" class="thumb" onclick="openLb(${idx},'${key}')" loading="lazy" title="Click to zoom"/>`);
  }

  function toRow(r){
    const sim={sim:Math.min(100,Math.max(0,r.Similarity||0)),passed:r.Passed,threshold:r.Threshold};
    return[
      r.__rowIdx,            // 0: hidden – used by thumbFmt via row.cells[0].data
      r.TestId||'',          // 1
      r.Browser||'',         // 2
      r.Viewport||'',        // 3
      sim,                   // 4: similarity object
      r.Passed,              // 5: pass/fail badge
      r.Threshold,           // 6
      r.BaselineStatus||'',  // 7
      r.BaselineImage||'',   // 8
      r.ActualImage||'',     // 9
      r.HeatmapImage||'',    // 10
      r.DiffImage||'',       // 11
      r.Reasoning||''        // 12
    ];
  }

  const columns=[
    {id:'__idx',   name:'', sort:false, width:'0px', formatter:()=>''},
    {id:'TestId',  name:'Test ID'},
    {id:'Browser', name:'Browser'},
    {id:'Viewport',name:'Viewport'},
    {id:'Sim',     name:'Similarity', sort:false, width:'110px', formatter:simFmt},
    {id:'Passed',  name:'Result',
      formatter:c=>gridjs.html(c?`<span class="pf-pass">✅ PASS</span>`:`<span class="pf-fail">❌ FAIL</span>`)},
    {id:'Threshold',     name:'Threshold', formatter:c=>c+'%'},
    {id:'BaselineStatus',name:'Baseline Status'},
    {id:'Baseline', name:'Baseline', sort:false,
      formatter:(c,row)=>thumbFmt(c,row.cells[0].data,'BaselineImage')},
    {id:'Actual',   name:'Actual',   sort:false,
      formatter:(c,row)=>thumbFmt(c,row.cells[0].data,'ActualImage')},
    {id:'Heatmap',  name:'Heatmap',  sort:false,
      formatter:(c,row)=>thumbFmt(c,row.cells[0].data,'HeatmapImage')},
    {id:'Diff',     name:'Diff',     sort:false,
      formatter:(c,row)=>thumbFmt(c,row.cells[0].data,'DiffImage')},
    {id:'Reasoning',name:'Reasoning'}
  ];

  const grid = new gridjs.Grid({
    columns,
    data:[],
    sort:true,
    search:false,
    pagination:{limit:25,summary:true},
    fixedHeader:true,
    height:'65vh'
  }).render(document.getElementById('grid'));

  function renderGrid(){
    grid.updateConfig({data:filtered.map(toRow)}).forceRender();
  }

  // Row tinting — watch for gridjs re-renders via MutationObserver
  new MutationObserver(()=>{
    document.querySelectorAll('.pf-pass,.pf-fail').forEach(el=>{
      const tr=el.closest('tr'); if(!tr) return;
      tr.classList.remove('row-pass','row-fail');
      tr.classList.add(el.classList.contains('pf-pass')?'row-pass':'row-fail');
    });
  }).observe(document.getElementById('grid'),{childList:true,subtree:true});

  // ── Filter wiring ────────────────────────────────────────────────────────
  function syncPfBtns(){
    document.getElementById('f-all' ).classList.toggle('active',pfFilter==='all');
    document.getElementById('f-pass').classList.toggle('active',pfFilter==='pass');
    document.getElementById('f-fail').classList.toggle('active',pfFilter==='fail');
  }
  document.getElementById('f-all' ).addEventListener('click',()=>{pfFilter='all'; syncPfBtns(); applyFilters();});
  document.getElementById('f-pass').addEventListener('click',()=>{pfFilter='pass';syncPfBtns(); applyFilters();});
  document.getElementById('f-fail').addEventListener('click',()=>{pfFilter='fail';syncPfBtns(); applyFilters();});
  fBrowser .addEventListener('change', applyFilters);
  fViewport.addEventListener('change', applyFilters);
  fSearch  .addEventListener('input',  applyFilters);
  document.getElementById('f-clear').addEventListener('click',()=>{
    pfFilter='all'; syncPfBtns(); fBrowser.value=''; fViewport.value=''; fSearch.value='';
    applyFilters();
  });

  // ── Keyboard shortcuts ───────────────────────────────────────────────────
  document.addEventListener('keydown',e=>{
    const tag=document.activeElement.tagName;
    if(e.key==='/'&&tag!=='INPUT'&&tag!=='TEXTAREA'&&tag!=='SELECT'){e.preventDefault();fSearch.focus();}
    if(e.key==='Escape'){closeLb();}
    const lbOpen=document.getElementById('lb-overlay').classList.contains('open');
    if(lbOpen&&e.key==='ArrowLeft')  prevTab();
    if(lbOpen&&e.key==='ArrowRight') nextTab();
  });

  // ── Lightbox ──────────────────────────────────────────────────────────────
  const LB_KEYS=['BaselineImage','ActualImage','HeatmapImage','DiffImage'];
  let lbRow=null, lbTabIdx=0;

  window.openLb=function(idx,key){
    lbRow=rowLookup[idx];
    lbTabIdx=Math.max(0,LB_KEYS.indexOf(key));
    renderLb();
    document.getElementById('lb-overlay').classList.add('open');
  };

  function renderLb(){
    if(!lbRow) return;
    const key=LB_KEYS[lbTabIdx];
    const src=lbRow[key]||'';
    const img=document.getElementById('lb-img');
    const noImg=document.getElementById('lb-no-img');
    if(src){img.src=src;img.style.display='';noImg.style.display='none';}
    else   {img.src=''; img.style.display='none';noImg.style.display='';}
    document.getElementById('lb-title')    .textContent=lbRow.TestId||'';
    document.getElementById('lb-sim')      .textContent=`Similarity: ${(lbRow.Similarity||0).toFixed(1)}%`;
    document.getElementById('lb-result')   .textContent=`Result: ${lbRow.Passed?'✅ PASS':'❌ FAIL'}`;
    document.getElementById('lb-threshold').textContent=`Threshold: ${lbRow.Threshold}%`;
    document.getElementById('lb-bstatus') .textContent=`Baseline: ${lbRow.BaselineStatus||''}`;
    document.querySelectorAll('.lb-tab').forEach((tab,i)=>{
      const k=tab.getAttribute('data-key');
      tab.classList.toggle('active',i===lbTabIdx);
      tab.disabled=!(lbRow[k]);
    });
  }

  function closeLb(){document.getElementById('lb-overlay').classList.remove('open');}

  function prevTab(){for(let i=lbTabIdx-1;i>=0;i--){if(lbRow&&lbRow[LB_KEYS[i]]){lbTabIdx=i;renderLb();break;}}}
  function nextTab(){for(let i=lbTabIdx+1;i<LB_KEYS.length;i++){if(lbRow&&lbRow[LB_KEYS[i]]){lbTabIdx=i;renderLb();break;}}}

  document.querySelectorAll('.lb-tab').forEach((tab,i)=>{
    tab.addEventListener('click',()=>{lbTabIdx=i;renderLb();});
  });
  document.getElementById('lb-close').addEventListener('click',closeLb);
  document.getElementById('lb-overlay').addEventListener('click',e=>{
    if(e.target===document.getElementById('lb-overlay'))closeLb();
  });

  // ── Exports ──────────────────────────────────────────────────────────────
  document.getElementById('btn-csv').addEventListener('click',()=>{
    const cols=['TestId','Browser','Viewport','Similarity','Passed','Threshold','BaselineStatus','Reasoning','AiReasoning'];
    const lines=[cols.join(','),...filtered.map(r=>cols.map(c=>JSON.stringify(r[c]??'')).join(','))];
    download('visual-regression.csv',lines.join('\n'),'text/csv');
  });
  document.getElementById('btn-json').addEventListener('click',()=>{
    download('visual-regression.json',JSON.stringify(filtered,null,2),'application/json');
  });

  // ── Init ──────────────────────────────────────────────────────────────────
  applyFilters();
})();
</script>
</body>
</html>
""";
    }
}
