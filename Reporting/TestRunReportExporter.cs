using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Exports a collection of <see cref="TestResult"/> objects to a production-ready
    /// interactive HTML report (GridJS + Bootstrap 5).
    /// <para>
    /// Output: a folder containing <c>index.html</c>, <c>data.json</c>, and <c>manifest.json</c>.
    /// All data is loaded client-side from data.json, so the report scales to thousands of tests
    /// without server-side paging.
    /// </para>
    /// <para>
    /// Features: summary stats bar, status/suite/category/browser filter bar, paginated sortable grid,
    /// per-row detail modal with tabs (Overview / Failure / AI Analysis / Screenshot / Screencast /
    /// Network / Custom Properties), video player for local/URL screencasts, stack-trace copy button,
    /// CSV + JSON export, keyboard shortcuts.
    /// </para>
    /// </summary>
    public static class TestRunReportExporter
    {
        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Exports <paramref name="results"/> to a self-contained HTML report folder.
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Parent directory under which the report folder is created.</param>
        /// <param name="reportName">Display name used in the report title and folder name.</param>
        /// <returns>Absolute path of the generated report folder.</returns>
        public static string Export(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot))
                throw new ArgumentNullException(nameof(outFolderRoot));

            Directory.CreateDirectory(outFolderRoot);

            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var reportFolder = GetUniqueFolderPath(outFolderRoot, $"{safeName}-{timestamp}");
            Directory.CreateDirectory(reportFolder);

            var list = (results ?? Enumerable.Empty<TestResult>()).ToList();

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented     = false,
                Encoder           = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // ── data.json (all rows — client does filtering/paging) ────────────
            var rows = list.Select((r, idx) => BuildRow(r, idx)).ToList();
            File.WriteAllText(
                Path.Combine(reportFolder, "data.json"),
                JsonSerializer.Serialize(rows, jsonOptions),
                Encoding.UTF8);

            // ── Per-run breakdown (populated when any result has RunName) ─────
            var runGroups = list
                .Where(r => !string.IsNullOrEmpty(r.RunName))
                .GroupBy(r => r.RunName!)
                .Select(g => new {
                    runName = g.Key,
                    total   = g.Count(),
                    pass    = g.Count(r => r.Status == TestStatus.Pass),
                    fail    = g.Count(r => r.Status == TestStatus.Fail),
                    skip    = g.Count(r => r.Status == TestStatus.Skip),
                    error   = g.Count(r => r.Status == TestStatus.Error)
                })
                .ToList();
            var runsJson = JsonSerializer.Serialize(runGroups, jsonOptions);

            // ── manifest.json ──────────────────────────────────────────────────
            var manifest = new
            {
                reportName   = reportName ?? safeName,
                totalTests   = list.Count,
                passed       = list.Count(x => x.Status == TestStatus.Pass),
                failed       = list.Count(x => x.Status == TestStatus.Fail),
                skipped      = list.Count(x => x.Status == TestStatus.Skip),
                errors       = list.Count(x => x.Status == TestStatus.Error),
                generatedUtc = DateTime.UtcNow.ToString("o"),
                runs         = runGroups
            };
            File.WriteAllText(
                Path.Combine(reportFolder, "manifest.json"),
                JsonSerializer.Serialize(manifest, jsonOptions),
                Encoding.UTF8);

            // ── index.html ────────────────────────────────────────────────────
            var html = GetTemplate()
                .Replace("[[REPORT_NAME]]", HtmlEnc(reportName ?? safeName))
                .Replace("[[GEN_TIME]]",    DateTime.UtcNow.ToString("u"))
                .Replace("[[RUNS_JSON]]",   runsJson);
            File.WriteAllText(Path.Combine(reportFolder, "index.html"), html, Encoding.UTF8);

            return Path.GetFullPath(reportFolder);
        }

        // ── Row serialisation ──────────────────────────────────────────────────

        private static object BuildRow(TestResult r, int idx) => new
        {
            __index        = idx,
            runName        = r.RunName        ?? "",
            testName       = r.TestName,
            testSuite      = r.TestSuite      ?? "",
            fullName       = r.FullName       ?? "",
            category       = r.Category       ?? "",
            tags           = r.Tags           ?? "",
            status         = r.Status.ToString(),
            durationMs     = (long)r.DurationMs,
            startTime      = r.StartTime.ToString("o"),
            browser        = r.Browser        ?? "",
            environment    = r.Environment    ?? "",
            machineName    = r.MachineName    ?? "",
            exceptionType  = r.ExceptionType  ?? "",
            exceptionMsg   = r.ExceptionMessage ?? "",
            stackTrace     = r.StackTrace     ?? "",
            assertMsg      = r.AssertMessage  ?? "",
            skipReason     = r.SkipReason     ?? "",
            screenshot     = r.ScreenshotPath ?? "",
            screencast     = r.ScreencastPath ?? "",
            diagnostics    = r.DiagnosticsFolder ?? "",
            networkHar     = r.NetworkHarPath ?? "",
            networkExcel   = r.NetworkExcelPath ?? "",
            aiAnalysis     = r.AiAnalysis     ?? "",
            customProps    = r.CustomProperties
        };

        // ── Utilities ──────────────────────────────────────────────────────────

        private static string HtmlEnc(string s) => System.Net.WebUtility.HtmlEncode(s);

        private static string MakeSafeFileName(string input)
        {
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

        // ── HTML template (raw string literal) ────────────────────────────────

        private static string GetTemplate() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>[[REPORT_NAME]] — Test Report</title>
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" crossorigin="anonymous"/>
<link href="https://unpkg.com/gridjs/dist/theme/mermaid.min.css" rel="stylesheet"/>
<style>
:root{
  --c-pass:#198754;--c-fail:#dc3545;--c-skip:#6c757d;--c-error:#fd7e14;
  --bg:#f5f6f8;--card:#fff;--border:#e2e8f0;
}
body{background:var(--bg);font-size:13px;margin:0}
/* ── Top bar ── */
#topbar{background:#1e293b;color:#f1f5f9;padding:9px 20px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:200}
#topbar h1{font-size:15px;margin:0;font-weight:600}
/* ── Stats bar ── */
#statsBar{display:flex;gap:10px;padding:10px 20px;background:var(--card);border-bottom:1px solid var(--border);flex-wrap:wrap;align-items:center}
.stat-card{background:#f8fafc;border:1px solid var(--border);border-radius:8px;padding:6px 16px;text-align:center;min-width:90px}
.stat-val{font-size:20px;font-weight:700;line-height:1.2}
.stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.4px}
#st-pass .stat-val{color:var(--c-pass)}
#st-fail .stat-val{color:var(--c-fail)}
#st-skip .stat-val{color:var(--c-skip)}
#st-err  .stat-val{color:var(--c-error)}
/* ── Filter bar ── */
#filterBar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:8px 20px;background:var(--card);border-bottom:1px solid var(--border)}
.st-pills{display:flex;gap:3px}
.st-btn{border:1px solid #cbd5e1;background:#fff;border-radius:20px;padding:2px 11px;font-size:11px;font-weight:600;cursor:pointer;transition:.12s}
.st-btn:hover{background:#f1f5f9}
.st-btn.active{background:#1e293b;color:#fff;border-color:#1e293b}
.st-btn.s-pass.active{background:var(--c-pass);border-color:var(--c-pass)}
.st-btn.s-fail.active{background:var(--c-fail);border-color:var(--c-fail)}
.st-btn.s-skip.active{background:var(--c-skip);border-color:var(--c-skip)}
.st-btn.s-error.active{background:var(--c-error);border-color:var(--c-error)}
/* ── Status badges ── */
.badge-pass{background:var(--c-pass);color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700}
.badge-fail{background:var(--c-fail);color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700}
.badge-skip{background:var(--c-skip);color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700}
.badge-error{background:var(--c-error);color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700}
/* ── Grid tweaks ── */
#main{padding:12px 20px}
.gridjs-container{font-size:13px}
.gridjs-table tr[data-st=Fail]{background:#fff5f5}
.gridjs-table tr[data-st=Error]{background:#fff8f0}
.gridjs-table tr[data-st=Skip]{background:#fafafa}
/* ── Detail modal ── */
.modal-xl .modal-body{padding:0}
#detailTabs .nav-link{font-size:12px;padding:6px 12px}
.tab-pane{padding:16px}
/* Stack trace */
.stack-wrap{position:relative}
pre.stack-pre{background:#0f172a;color:#e2e8f0;border-radius:6px;padding:14px;font-size:12px;max-height:280px;overflow:auto;white-space:pre-wrap;word-break:break-word}
.copy-stack{position:absolute;top:6px;right:6px;background:#334155;border:none;color:#94a3b8;border-radius:4px;padding:2px 8px;font-size:11px;cursor:pointer}
.copy-stack:hover{background:#475569;color:#f1f5f9}
.stack-toggle{font-size:11px;color:#64748b;cursor:pointer;text-decoration:underline;display:block;margin-top:4px}
/* AI analysis panel */
.ai-panel{background:#f0f9ff;border-left:4px solid #0ea5e9;border-radius:0 6px 6px 0;padding:12px 16px;font-size:13px;white-space:pre-wrap;word-break:break-word;line-height:1.6}
.ai-panel .xpath-candidate{background:#fef3c7;border:1px solid #fbbf24;border-radius:3px;padding:0 4px;font-family:monospace;font-size:12px}
/* Screenshot */
.ss-wrap img{max-width:100%;border-radius:6px;border:1px solid var(--border);cursor:zoom-in;transition:.15s}
.ss-wrap img:hover{box-shadow:0 4px 20px rgba(0,0,0,.15)}
/* Screencast */
.sc-video{width:100%;border-radius:6px;max-height:360px;background:#000}
.sc-link{display:inline-flex;align-items:center;gap:6px;color:#0d6efd;text-decoration:none;font-size:13px}
.sc-link:hover{text-decoration:underline}
/* Custom props table */
.cp-table{width:100%;border-collapse:collapse;font-size:12px}
.cp-table th{background:#f1f5f9;padding:5px 8px;text-align:left;font-weight:600;border:1px solid var(--border)}
.cp-table td{padding:5px 8px;border:1px solid var(--border);word-break:break-word}
/* Lightbox */
#lightbox{display:none;position:fixed;inset:0;background:rgba(0,0,0,.85);z-index:9999;align-items:center;justify-content:center}
#lightbox.open{display:flex}
#lightbox img{max-width:92vw;max-height:90vh;border-radius:6px;box-shadow:0 8px 40px rgba(0,0,0,.6)}
#lightbox-close{position:absolute;top:14px;right:20px;font-size:28px;color:#fff;cursor:pointer;line-height:1}
/* Hidden index col */
.gridjs-th:first-child,.gridjs-td:first-child{width:0!important;max-width:0;overflow:hidden;padding:0;border:none}
/* ── Runs bar ── */
#runsBar{display:none;padding:8px 20px;background:var(--card);border-bottom:1px solid var(--border);overflow-x:auto}
.runs-scroll{display:flex;gap:8px;min-width:max-content}
.run-card{background:#f8fafc;border:1px solid var(--border);border-radius:8px;padding:8px 14px;min-width:150px;cursor:pointer;transition:.12s;user-select:none}
.run-card:hover{border-color:#94a3b8;background:#f1f5f9}
.run-card.active{border-color:#1e293b;background:#e2e8f0}
.run-name{font-weight:700;font-size:12px;color:#1e293b;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:190px}
.run-stats{font-size:11px;margin-top:3px;display:flex;gap:6px;flex-wrap:wrap}
.run-rate{font-size:10px;color:#64748b;margin-top:3px;text-align:right}
.run-progbar{height:3px;border-radius:2px;background:#e2e8f0;margin-top:3px}
.run-progfill{height:3px;border-radius:2px}
/* Run badge in grid */
.run-badge{background:#e0e7ff;color:#3730a3;padding:1px 7px;border-radius:4px;font-size:11px;font-weight:600;white-space:nowrap}
</style>
</head>
<body>

<!-- ── Top bar ────────────────────────────────────────────────────────── -->
<div id="topbar">
  <h1>[[REPORT_NAME]]</h1>
  <span id="gen-time" style="color:#94a3b8;font-size:11px">Generated [[GEN_TIME]]</span>
  <div style="margin-left:auto;display:flex;gap:8px">
    <button id="btnCsv"  class="btn btn-sm btn-outline-light">Export CSV</button>
    <button id="btnJson" class="btn btn-sm btn-outline-light">Export JSON</button>
  </div>
</div>

<!-- ── Stats bar ──────────────────────────────────────────────────────── -->
<div id="statsBar">
  <div class="stat-card"   ><div class="stat-val" id="sv-total">—</div><div class="stat-lbl">Total</div></div>
  <div class="stat-card" id="st-pass"><div class="stat-val" id="sv-pass">—</div><div class="stat-lbl">Pass</div></div>
  <div class="stat-card" id="st-fail"><div class="stat-val" id="sv-fail">—</div><div class="stat-lbl">Fail</div></div>
  <div class="stat-card" id="st-skip"><div class="stat-val" id="sv-skip">—</div><div class="stat-lbl">Skip</div></div>
  <div class="stat-card" id="st-err" ><div class="stat-val" id="sv-err" >—</div><div class="stat-lbl">Error</div></div>
  <div class="stat-card"   ><div class="stat-val" id="sv-rate">—</div><div class="stat-lbl">Pass Rate</div></div>
  <div class="stat-card"   ><div class="stat-val" id="sv-dur" >—</div><div class="stat-lbl">Total Duration</div></div>
  <div class="stat-card"   ><div class="stat-val" id="sv-avg" >—</div><div class="stat-lbl">Avg Duration</div></div>
  <div id="load-msg" style="margin-left:auto;color:#64748b;font-size:12px">Loading…</div>
</div>

<!-- ── Runs bar (shown only when RunName is set on results) ───────────── -->
<div id="runsBar"><div class="runs-scroll" id="runsScroll"></div></div>

<!-- ── Filter bar ─────────────────────────────────────────────────────── -->
<div id="filterBar">
  <div class="st-pills">
    <button class="st-btn active" data-st="all"  >All</button>
    <button class="st-btn s-pass" data-st="Pass" >Pass</button>
    <button class="st-btn s-fail" data-st="Fail" >Fail</button>
    <button class="st-btn s-skip" data-st="Skip" >Skip</button>
    <button class="st-btn s-error"data-st="Error">Error</button>
  </div>
  <select id="f-suite"   class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Suites</option></select>
  <select id="f-cat"     class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Categories</option></select>
  <select id="f-browser" class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Browsers</option></select>
  <select id="f-env"     class="form-select form-select-sm" style="width:auto;min-width:110px"><option value="">All Envs</option></select>
  <select id="f-run"     class="form-select form-select-sm" style="display:none;width:auto;min-width:120px"><option value="">All Runs</option></select>
  <input  id="f-search"  class="form-control form-control-sm" placeholder="Search… (press /)" style="width:200px"/>
  <select id="f-pagesize"class="form-select form-select-sm" style="width:100px"></select>
  <button id="f-clear"   class="btn btn-sm btn-outline-secondary">Clear</button>
</div>

<!-- ── Grid ───────────────────────────────────────────────────────────── -->
<div id="main"><div id="grid"></div></div>

<!-- ── Detail modal ───────────────────────────────────────────────────── -->
<div class="modal fade modal-xl" id="detailModal" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-xl modal-dialog-scrollable">
    <div class="modal-content">
      <div class="modal-header py-2">
        <h5 class="modal-title fs-6 fw-semibold" id="detailTitle">Test Detail</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body p-0">
        <ul class="nav nav-tabs px-3 pt-2" id="detailTabs">
          <li class="nav-item"><button class="nav-link active" data-tab="overview"  >Overview</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="failure"   >Failure</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="ai"        >AI Analysis</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="screenshot">Screenshot</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="screencast">Screencast</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="network"   >Network</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="custom"    >Custom</button></li>
        </ul>

        <!-- Overview -->
        <div id="tab-overview" class="tab-pane">
          <table class="table table-sm table-bordered small mb-0">
            <tbody id="ov-body"></tbody>
          </table>
        </div>

        <!-- Failure -->
        <div id="tab-failure" class="tab-pane" style="display:none">
          <div id="fail-empty" class="text-muted small fst-italic">No failure details for this test.</div>
          <div id="fail-content">
            <div class="mb-3">
              <div class="fw-semibold small mb-1">Exception</div>
              <div id="fail-exc" class="bg-danger bg-opacity-10 border border-danger-subtle rounded px-3 py-2 small font-monospace"></div>
            </div>
            <div class="mb-3" id="fail-assert-wrap">
              <div class="fw-semibold small mb-1">Assertion Detail</div>
              <div id="fail-assert" class="bg-warning bg-opacity-10 border border-warning-subtle rounded px-3 py-2 small"></div>
            </div>
            <div class="mb-1 fw-semibold small">Stack Trace</div>
            <div class="stack-wrap">
              <pre class="stack-pre" id="fail-stack"></pre>
              <button class="copy-stack" id="btn-copy-stack">Copy</button>
            </div>
            <span class="stack-toggle" id="stack-toggle">Show full stack</span>
          </div>
          <div id="skip-content" style="display:none" class="mt-2">
            <div class="fw-semibold small mb-1">Skip Reason</div>
            <div id="skip-reason" class="bg-secondary bg-opacity-10 border rounded px-3 py-2 small"></div>
          </div>
        </div>

        <!-- AI Analysis -->
        <div id="tab-ai" class="tab-pane" style="display:none">
          <div id="ai-empty" class="text-muted small fst-italic">No AI analysis available for this test.</div>
          <div id="ai-panel" class="ai-panel" style="display:none"></div>
        </div>

        <!-- Screenshot -->
        <div id="tab-screenshot" class="tab-pane" style="display:none">
          <div id="ss-empty" class="text-muted small fst-italic">No screenshot attached.</div>
          <div id="ss-wrap" class="ss-wrap" style="display:none">
            <img id="ss-img" src="" alt="Screenshot" onclick="openLightbox(this.src)"/>
            <div class="mt-2"><a id="ss-link" href="#" target="_blank" class="btn btn-sm btn-outline-secondary">Open in new tab</a></div>
          </div>
        </div>

        <!-- Screencast -->
        <div id="tab-screencast" class="tab-pane" style="display:none">
          <div id="sc-empty" class="text-muted small fst-italic">No screencast attached.</div>
          <video id="sc-video" class="sc-video" controls style="display:none"></video>
          <div id="sc-link-wrap" style="display:none;margin-top:8px">
            <a id="sc-link" href="#" target="_blank" class="sc-link">
              <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16"><path d="M4.5 6.375a4.125 4.125 0 1 1 8.25 0 4.125 4.125 0 0 1-8.25 0ZM8.625 2.25a4.125 4.125 0 1 0 0 8.25 4.125 4.125 0 0 0 0-8.25Z"/></svg>
              Open Screencast Link
            </a>
          </div>
        </div>

        <!-- Network -->
        <div id="tab-network" class="tab-pane" style="display:none">
          <div id="net-empty" class="text-muted small fst-italic">No network artifacts attached.</div>
          <div id="net-content">
            <div class="mb-2" id="net-har-wrap">
              <a id="net-har" href="#" target="_blank" class="btn btn-sm btn-outline-primary">Download HAR</a>
            </div>
            <div id="net-excel-wrap">
              <a id="net-excel" href="#" target="_blank" class="btn btn-sm btn-outline-success">Download Network Excel</a>
            </div>
          </div>
        </div>

        <!-- Custom Properties -->
        <div id="tab-custom" class="tab-pane" style="display:none">
          <div id="cp-empty" class="text-muted small fst-italic">No custom properties.</div>
          <table class="cp-table" id="cp-table" style="display:none">
            <thead><tr><th>Key</th><th>Value</th></tr></thead>
            <tbody id="cp-body"></tbody>
          </table>
        </div>
      </div><!-- /modal-body -->
    </div>
  </div>
</div>

<!-- ── Lightbox ───────────────────────────────────────────────────────── -->
<div id="lightbox" onclick="closeLightbox()">
  <span id="lightbox-close" onclick="closeLightbox()">&times;</span>
  <img id="lightbox-img" src="" alt="Screenshot"/>
</div>

<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js" crossorigin="anonymous"></script>
<script src="https://unpkg.com/gridjs/dist/gridjs.umd.js"></script>
<script>
(function(){
'use strict';

// ── State ──────────────────────────────────────────────────────────────
let ALL_ROWS = [];       // raw data from data.json
let FILTERED = [];       // after applying filters
let activeStatus = 'all';
let activeSuite  = '';
let activeCat    = '';
let activeBrowser= '';
let activeEnv    = '';
let activeRun    = '';
let searchTerm   = '';
let pageSize     = 50;
let grid;
const PAGE_SIZES = [25, 50, 100, 250, 500];
const RUNS_DATA  = [[RUNS_JSON]];          // per-run summary; empty = no run grouping
const HAS_RUNS   = RUNS_DATA.length >= 1;  // true = at least one RunName present

// ── Load ───────────────────────────────────────────────────────────────
fetch('data.json')
  .then(r => r.json())
  .then(rows => {
    ALL_ROWS = rows;
    document.getElementById('load-msg').style.display = 'none';
    populateDropdowns();
    applyFilters();
    renderStats(ALL_ROWS);
  })
  .catch(e => {
    document.getElementById('load-msg').textContent = 'Error loading data.json: ' + e.message;
  });

// ── Dropdowns ──────────────────────────────────────────────────────────
function populateDropdowns(){
  fillSelect('f-suite',   unique(ALL_ROWS, r => r.testSuite).sort());
  fillSelect('f-cat',     unique(ALL_ROWS, r => r.category).sort());
  fillSelect('f-browser', unique(ALL_ROWS, r => r.browser).sort());
  fillSelect('f-env',     unique(ALL_ROWS, r => r.environment).sort());
  if(HAS_RUNS){
    const runEl = document.getElementById('f-run');
    RUNS_DATA.forEach(rd => { const o=document.createElement('option'); o.value=rd.runName; o.textContent=rd.runName; runEl.appendChild(o); });
    runEl.style.display='';
    document.getElementById('runsBar').style.display='';
    renderRunsBar();
  }
  const ps = document.getElementById('f-pagesize');
  PAGE_SIZES.forEach(n => { const o=document.createElement('option'); o.value=n; o.textContent=n+' / page'; if(n===pageSize)o.selected=true; ps.appendChild(o); });
}
function unique(rows, fn){ return [...new Set(rows.map(fn).filter(Boolean))]; }
function fillSelect(id, opts){
  const sel = document.getElementById(id);
  opts.forEach(v => { const o=document.createElement('option'); o.value=v; o.textContent=v; sel.appendChild(o); });
}

// ── Filter ─────────────────────────────────────────────────────────────
function applyFilters(){
  const q = searchTerm.toLowerCase();
  FILTERED = ALL_ROWS.filter(r => {
    if(activeStatus !== 'all' && r.status !== activeStatus) return false;
    if(activeSuite   && r.testSuite   !== activeSuite)   return false;
    if(activeCat     && r.category    !== activeCat)     return false;
    if(activeBrowser && r.browser     !== activeBrowser) return false;
    if(activeEnv     && r.environment !== activeEnv)     return false;
    if(activeRun     && r.runName     !== activeRun)     return false;
    if(q && ![r.testName,r.testSuite,r.fullName,r.category,r.tags,r.browser,r.runName,r.exceptionMsg].some(f=>f&&f.toLowerCase().includes(q))) return false;
    return true;
  });
  renderGrid(FILTERED);
  renderStats(ALL_ROWS, FILTERED);
}

// ── Stats ──────────────────────────────────────────────────────────────
function renderStats(all, filtered){
  const rows = filtered || all;
  const pass  = all.filter(r=>r.status==='Pass').length;
  const fail  = all.filter(r=>r.status==='Fail').length;
  const skip  = all.filter(r=>r.status==='Skip').length;
  const error = all.filter(r=>r.status==='Error').length;
  const total = all.length;
  const totalDur = all.reduce((s,r)=>s+r.durationMs,0);
  set('sv-total', total);
  set('sv-pass',  pass);
  set('sv-fail',  fail);
  set('sv-skip',  skip);
  set('sv-err',   error);
  set('sv-rate',  total ? Math.round(pass/total*100)+'%' : 'N/A');
  set('sv-dur',   fmtDur(totalDur));
  set('sv-avg',   total ? fmtDur(totalDur/total) : 'N/A');
}
function set(id,v){ const el=document.getElementById(id); if(el) el.textContent=v; }
function fmtDur(ms){
  if(ms<1000)   return ms.toFixed(0)+'ms';
  if(ms<60000)  return (ms/1000).toFixed(1)+'s';
  return (ms/60000).toFixed(1)+'min';
}

// ── Grid ───────────────────────────────────────────────────────────────
function renderGrid(rows){
  const data = rows.map(r => [
    r.__index,           // 0 hidden
    '#'+(rows.indexOf(r)+1),
    r.runName,           // 2 run (hidden when !HAS_RUNS)
    r.testName,
    r.testSuite,
    r.status,
    fmtDur(r.durationMs),
    r.browser,
    r.tags
  ]);

  if(grid){
    grid.updateConfig({ data }).forceRender();
    return;
  }

  grid = new gridjs.Grid({
    columns: [
      { id:'__idx', name:'', hidden:true },
      { id:'num',   name:'#',         width:'50px',  sort:false },
      { id:'run',   name:'Run',       width:'110px', hidden:!HAS_RUNS,
        formatter: cell=>gridjs.html(cell ? `<span class="run-badge">${esc(cell)}</span>` : '') },
      { id:'name',  name:'Test Name', width:'240px',
        formatter:(cell,row)=>gridjs.html(`<span class="cell-link" data-idx="${row.cells[0].data}">${esc(cell)}</span>`) },
      { id:'suite', name:'Suite',     width:'150px' },
      { id:'status',name:'Status',    width:'80px',
        formatter: cell=>gridjs.html(statusBadge(cell)) },
      { id:'dur',   name:'Duration',  width:'90px' },
      { id:'browser',name:'Browser',  width:'110px' },
      { id:'tags',  name:'Tags',      width:'140px',
        formatter: cell=>gridjs.html(cell ? `<span class="text-muted small">${esc(cell)}</span>` : '') }
    ],
    data,
    search: false,
    pagination: { limit: pageSize },
    sort: true,
    style: { table:{'white-space':'nowrap'} }
  }).render(document.getElementById('grid'));

  document.getElementById('grid').addEventListener('click', e=>{
    const el = e.target.closest('.cell-link');
    if(el) openDetail(parseInt(el.dataset.idx));
  });
}

// ── Runs bar renderer ──────────────────────────────────────────────────
function renderRunsBar(){
  const scroll = document.getElementById('runsScroll');
  scroll.innerHTML = RUNS_DATA.map(rd => {
    const pct    = rd.total ? Math.round(rd.pass / rd.total * 100) : 0;
    const barClr = pct === 100 ? '#198754' : pct >= 80 ? '#ffc107' : '#dc3545';
    const isActive = activeRun === rd.runName;
    return `<div class="run-card${isActive?' active':''}" onclick="toggleRunFilter('${esc(rd.runName)}')">
      <div class="run-name" title="${esc(rd.runName)}">${esc(rd.runName)}</div>
      <div class="run-stats">
        <span style="color:var(--c-pass)">${rd.pass}✓</span>
        ${rd.fail  ? `<span style="color:var(--c-fail)">${rd.fail}✗</span>` : ''}
        ${rd.skip  ? `<span style="color:var(--c-skip)">${rd.skip}⊘</span>` : ''}
        ${rd.error ? `<span style="color:var(--c-error)">${rd.error}!</span>` : ''}
      </div>
      <div class="run-progbar"><div class="run-progfill" style="width:${pct}%;background:${barClr}"></div></div>
      <div class="run-rate">${pct}% pass · ${rd.total} test${rd.total!==1?'s':''}</div>
    </div>`;
  }).join('');
}
window.toggleRunFilter = function(run){
  activeRun = (activeRun === run) ? '' : run;
  document.getElementById('f-run').value = activeRun;
  document.querySelectorAll('.run-card').forEach(c =>
    c.classList.toggle('active', activeRun !== '' && c.querySelector('.run-name').title === activeRun)
  );
  applyFilters();
};

function statusBadge(s){
  const cls = {Pass:'pass',Fail:'fail',Skip:'skip',Error:'error'}[s]||'skip';
  return `<span class="badge-${cls}">${esc(s)}</span>`;
}

function esc(s){ return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

// ── Detail modal ───────────────────────────────────────────────────────
const modalEl = document.getElementById('detailModal');
const bsModal = new bootstrap.Modal(modalEl);
let currentStackFull = '';
let stackCollapsed = true;

function openDetail(idx){
  const r = ALL_ROWS[idx];
  if(!r) return;
  document.getElementById('detailTitle').textContent = r.testName + (r.testSuite ? ' · '+r.testSuite : '');
  // Switch to Overview tab
  switchTab('overview');

  // ── Overview ──────────────────────────────────────────────────────
  const ovRows = [
    ['Test Name',    r.testName],
    ['Full Name',    r.fullName],
    ['Suite',        r.testSuite],
    ['Category',     r.category],
    ['Tags',         r.tags],
    ['Status',       statusBadge(r.status)],
    ['Duration',     fmtDur(r.durationMs)],
    ['Start Time',   r.startTime],
    ['Browser',      r.browser],
    ['Environment',  r.environment],
    ['Machine',      r.machineName],
  ].filter(([,v])=>v);
  const tbody = document.getElementById('ov-body');
  tbody.innerHTML = ovRows.map(([k,v])=>`<tr><th class="text-nowrap" style="width:130px">${esc(k)}</th><td>${v}</td></tr>`).join('');

  // ── Failure tab ───────────────────────────────────────────────────
  const hasFailure = r.exceptionType || r.exceptionMsg || r.stackTrace;
  const hasSkip    = r.skipReason;
  document.getElementById('fail-empty').style.display   = (!hasFailure && !hasSkip) ? '' : 'none';
  document.getElementById('fail-content').style.display = hasFailure ? '' : 'none';
  document.getElementById('skip-content').style.display = (!hasFailure && hasSkip) ? '' : 'none';
  if(hasFailure){
    document.getElementById('fail-exc').textContent =
      (r.exceptionType ? r.exceptionType+': ' : '') + (r.exceptionMsg||'');
    const aw = document.getElementById('fail-assert-wrap');
    if(r.assertMsg){ aw.style.display=''; document.getElementById('fail-assert').textContent=r.assertMsg; }
    else aw.style.display='none';
    currentStackFull = r.stackTrace||'';
    stackCollapsed = true;
    renderStack();
  }
  if(hasSkip) document.getElementById('skip-reason').textContent = r.skipReason||'';

  // ── AI Analysis ───────────────────────────────────────────────────
  document.getElementById('ai-empty').style.display = r.aiAnalysis ? 'none' : '';
  const aiPan = document.getElementById('ai-panel');
  if(r.aiAnalysis){
    aiPan.style.display='';
    aiPan.innerHTML = renderAI(r.aiAnalysis);
  } else aiPan.style.display='none';

  // ── Screenshot ────────────────────────────────────────────────────
  document.getElementById('ss-empty').style.display = r.screenshot ? 'none' : '';
  const ssWrap = document.getElementById('ss-wrap');
  if(r.screenshot){
    ssWrap.style.display='';
    document.getElementById('ss-img').src   = r.screenshot;
    document.getElementById('ss-link').href = r.screenshot;
  } else ssWrap.style.display='none';

  // ── Screencast ────────────────────────────────────────────────────
  document.getElementById('sc-empty').style.display = r.screencast ? 'none' : '';
  const scVid  = document.getElementById('sc-video');
  const scLink = document.getElementById('sc-link-wrap');
  scVid.style.display='none'; scLink.style.display='none';
  if(r.screencast){
    if(isVideoUrl(r.screencast)){
      scVid.style.display='';
      scVid.src = r.screencast;
    } else {
      scLink.style.display='';
      document.getElementById('sc-link').href = r.screencast;
    }
  }

  // ── Network ───────────────────────────────────────────────────────
  const hasNet = r.networkHar || r.networkExcel;
  document.getElementById('net-empty').style.display   = hasNet ? 'none' : '';
  document.getElementById('net-content').style.display = hasNet ? '' : 'none';
  if(r.networkHar){
    document.getElementById('net-har-wrap').style.display='';
    document.getElementById('net-har').href=r.networkHar;
  } else document.getElementById('net-har-wrap').style.display='none';
  if(r.networkExcel){
    document.getElementById('net-excel-wrap').style.display='';
    document.getElementById('net-excel').href=r.networkExcel;
  } else document.getElementById('net-excel-wrap').style.display='none';

  // ── Custom Props ─────────────────────────────────────────────────
  const cp = r.customProps && Object.keys(r.customProps).length > 0 ? r.customProps : null;
  document.getElementById('cp-empty').style.display   = cp ? 'none' : '';
  const cpTbl = document.getElementById('cp-table');
  if(cp){
    cpTbl.style.display='';
    document.getElementById('cp-body').innerHTML =
      Object.entries(cp).map(([k,v])=>`<tr><td><strong>${esc(k)}</strong></td><td>${esc(v)}</td></tr>`).join('');
  } else cpTbl.style.display='none';

  bsModal.show();
}

// ── AI text renderer ───────────────────────────────────────────────────
function renderAI(text){
  return text
    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
    .replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>')
    .replace(/^XPATH_CANDIDATE:\s*(.+)$/gm, (_,x)=>`<span class="xpath-candidate">XPATH_CANDIDATE: ${x}</span>`)
    .replace(/\n/g,'<br>');
}

// ── Stack trace helpers ────────────────────────────────────────────────
function renderStack(){
  const MAX = 600;
  const pre = document.getElementById('fail-stack');
  const tog = document.getElementById('stack-toggle');
  if(!currentStackFull){ pre.textContent='(no stack trace)'; tog.style.display='none'; return; }
  if(currentStackFull.length <= MAX){ pre.textContent=currentStackFull; tog.style.display='none'; return; }
  pre.textContent = stackCollapsed ? currentStackFull.substring(0,MAX)+'…' : currentStackFull;
  tog.textContent = stackCollapsed ? 'Show full stack' : 'Collapse stack';
  tog.style.display='';
}

document.getElementById('stack-toggle').addEventListener('click',()=>{
  stackCollapsed=!stackCollapsed; renderStack();
});

document.getElementById('btn-copy-stack').addEventListener('click',()=>{
  navigator.clipboard.writeText(currentStackFull).then(()=>{
    const btn=document.getElementById('btn-copy-stack');
    btn.textContent='Copied!'; setTimeout(()=>btn.textContent='Copy',1500);
  });
});

// ── Tab switching ──────────────────────────────────────────────────────
function switchTab(name){
  document.querySelectorAll('[data-tab]').forEach(b=>b.classList.toggle('active',b.dataset.tab===name));
  ['overview','failure','ai','screenshot','screencast','network','custom'].forEach(t=>{
    document.getElementById('tab-'+t).style.display = t===name ? '' : 'none';
  });
}
document.querySelectorAll('[data-tab]').forEach(b=>b.addEventListener('click',()=>switchTab(b.dataset.tab)));

// Reset video src when modal closes (stop playback)
modalEl.addEventListener('hidden.bs.modal',()=>{
  const v=document.getElementById('sc-video'); v.pause(); v.src='';
});

// ── Screencast detection ───────────────────────────────────────────────
const VIDEO_EXTS = /\.(mp4|webm|ogv|ogg|mov)(\?.*)?$/i;
function isVideoUrl(url){ return VIDEO_EXTS.test(url); }

// ── Lightbox ───────────────────────────────────────────────────────────
window.openLightbox = function(src){
  document.getElementById('lightbox-img').src=src;
  document.getElementById('lightbox').classList.add('open');
};
window.closeLightbox = function(){
  document.getElementById('lightbox').classList.remove('open');
};
document.addEventListener('keydown',e=>{
  if(e.key==='Escape') closeLightbox();
});

// ── Filter event wiring ────────────────────────────────────────────────
document.querySelectorAll('.st-btn').forEach(b=>b.addEventListener('click',()=>{
  document.querySelectorAll('.st-btn').forEach(x=>x.classList.remove('active'));
  b.classList.add('active');
  activeStatus = b.dataset.st;
  applyFilters();
}));
document.getElementById('f-suite'  ).addEventListener('change',e=>{ activeSuite  =e.target.value; applyFilters(); });
document.getElementById('f-cat'    ).addEventListener('change',e=>{ activeCat    =e.target.value; applyFilters(); });
document.getElementById('f-browser').addEventListener('change',e=>{ activeBrowser=e.target.value; applyFilters(); });
document.getElementById('f-env'    ).addEventListener('change',e=>{ activeEnv    =e.target.value; applyFilters(); });
document.getElementById('f-run'    ).addEventListener('change',e=>{
  activeRun = e.target.value;
  document.querySelectorAll('.run-card').forEach(c =>
    c.classList.toggle('active', activeRun !== '' && c.querySelector('.run-name').title === activeRun)
  );
  applyFilters();
});
document.getElementById('f-search' ).addEventListener('input', e=>{ searchTerm   =e.target.value; applyFilters(); });
document.getElementById('f-pagesize').addEventListener('change',e=>{
  pageSize=parseInt(e.target.value);
  if(grid) grid.updateConfig({pagination:{limit:pageSize}}).forceRender();
});
document.getElementById('f-clear').addEventListener('click',()=>{
  activeStatus='all'; activeSuite=''; activeCat=''; activeBrowser=''; activeEnv=''; activeRun=''; searchTerm='';
  document.getElementById('f-search').value='';
  document.getElementById('f-suite').value='';
  document.getElementById('f-cat').value='';
  document.getElementById('f-browser').value='';
  document.getElementById('f-env').value='';
  document.getElementById('f-run').value='';
  document.querySelectorAll('.run-card').forEach(c=>c.classList.remove('active'));
  document.querySelectorAll('.st-btn').forEach(b=>b.classList.toggle('active',b.dataset.st==='all'));
  applyFilters();
});

// ── Keyboard shortcuts ─────────────────────────────────────────────────
document.addEventListener('keydown',e=>{
  const tag = document.activeElement.tagName;
  if(e.key==='/' && tag!=='INPUT' && tag!=='TEXTAREA'){
    e.preventDefault(); document.getElementById('f-search').focus();
  }
  if(e.key==='Escape'){
    document.getElementById('f-search').blur();
    document.getElementById('f-search').value=''; searchTerm=''; applyFilters();
  }
});

// ── Export helpers ─────────────────────────────────────────────────────
document.getElementById('btnCsv').addEventListener('click',()=>exportCsv());
document.getElementById('btnJson').addEventListener('click',()=>exportJson());

function exportCsv(){
  const cols=['runName','testName','testSuite','category','status','durationMs','browser','environment','machineName','tags','exceptionType','exceptionMsg'];
  const header = cols.join(',');
  const lines  = FILTERED.map(r=>cols.map(c=>csvCell(r[c])).join(','));
  download('test-report.csv', header+'\n'+lines.join('\n'), 'text/csv');
}
function exportJson(){
  download('test-report.json', JSON.stringify(FILTERED,null,2), 'application/json');
}
function csvCell(v){
  const s = String(v==null?'':v).replace(/"/g,'""');
  return /[",\n]/.test(s) ? `"${s}"` : s;
}
function download(name,content,type){
  const a=document.createElement('a');
  a.href=URL.createObjectURL(new Blob([content],{type}));
  a.download=name; a.click();
}
})();
</script>
</body>
</html>
""";
    }
}
