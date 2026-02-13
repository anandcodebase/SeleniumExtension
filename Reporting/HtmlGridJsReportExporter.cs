using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SimpleSeleniumSupport.Reporting
{
    public sealed class HtmlGridJsReportExporter
    {
        private readonly List<VisualGridRow> _visualRows = new();

        public void AddVisualResult(VisualGridRow row)
        {
            _visualRows.Add(row);
        }

        public void Save(string outputPath)
        {
            var json = JsonSerializer.Serialize(_visualRows);

            var html = $@"
<!doctype html>
<html>
<head>
<link href='https://unpkg.com/gridjs/dist/theme/mermaid.min.css' rel='stylesheet'/>
<script src='https://unpkg.com/gridjs/dist/gridjs.umd.js'></script>
<style>
.thumb {{ max-width: 80px; cursor: pointer; }}
.modal {{ display:none; position:fixed; inset:0; background:#0008; z-index:999; }}
.modal img {{ margin:auto; display:block; max-width:90%; max-height:90%; padding-top:5%; }}
.pass {{ color: #4caf50; font-weight: bold; }}
.fail {{ color: #f44336; font-weight: bold; }}
</style>
</head>
<body>
<div id='grid'></div>
<div id='modal' class='modal' onclick='this.style.display=""none""'>
<img/>
</div>

<script>
const data = {json};

function img(src) {{
  if (!src) return '';
  return gridjs.html(`<img src='${{src}}' class='thumb'
    onclick='openImg(""${{src}}"")'/>`);
}}

function passFail(v) {{
  return gridjs.html(v
    ? `<span class='pass'>PASS</span>`
    : `<span class='fail'>FAIL</span>`);
}}

function openImg(src) {{
  const m = document.getElementById('modal');
  m.querySelector('img').src = src;
  m.style.display='block';
}}

new gridjs.Grid({{
  columns: [
    'TestId','Browser','Viewport','Similarity',
    'SSIM','Edge','PixelDiff',
    {{ name:'Pass', formatter: (c) => passFail(c) }},
    'Threshold','BaselineStatus',
    {{ name:'Baseline', formatter: (c) => img(c) }},
    {{ name:'Actual', formatter: (c) => img(c) }},
    {{ name:'Heatmap', formatter: (c) => img(c) }},
    {{ name:'Diff', formatter: (c) => img(c) }},
    'Reasoning','AiReasoning'
  ],
  data: data.map(r => [
    r.TestId, r.Browser, r.Viewport, r.Similarity,
    r.SsimPercent, r.EdgePercent, r.PixelDiffPercent,
    r.Passed, r.Threshold, r.BaselineStatus,
    r.BaselineImage, r.ActualImage, r.HeatmapImage,
    r.DiffImage,
    r.Reasoning, r.AiReasoning
  ]),
  search:true,
  pagination:{{ limit:10 }},
  sort:true
}}).render(document.getElementById('grid'));
</script>
</body>
</html>";

            File.WriteAllText(outputPath, html);
        }
    }
}
