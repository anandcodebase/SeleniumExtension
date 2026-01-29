using System.Collections.Generic;
using System.IO;
using System.Text;
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
.modal {{ display:none; position:fixed; inset:0; background:#0008; }}
.modal img {{ margin:auto; display:block; max-width:90%; max-height:90%; }}
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

function openImg(src) {{
  const m = document.getElementById('modal');
  m.querySelector('img').src = src;
  m.style.display='block';
}}

new gridjs.Grid({{
  columns: [
    'TestId','Browser','Viewport','Similarity','BaselineStatus',
    {{ name:'Baseline', formatter: img }},
    {{ name:'Actual', formatter: img }},
    {{ name:'Heatmap', formatter: img }},
    'Reasoning'
  ],
  data: data.map(r => [
    r.TestId, r.Browser, r.Viewport, r.Similarity,
    r.BaselineStatus,
    r.BaselineImage, r.ActualImage, r.HeatmapImage,
    r.Reasoning
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
