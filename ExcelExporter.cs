
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Generated the Excel Report
    /// </summary>
    public static class ExcelExporter
    {
        /// <summary>
        /// Export network info rows to an Excel .xlsx file using NPOI.
        /// Writes timestamps as true Excel datetime cells.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="path">The path.</param>
        public static void ExportNetworkInfoToExcel(IEnumerable<FullNetworkInfo> items, string path)
        {
            var list = items?.ToList() ?? new List<FullNetworkInfo>();

            IWorkbook workbook = new XSSFWorkbook(); // .xlsx
            var sheet = workbook.CreateSheet("Network");

            // Create a date cell style (Excel datetime)
            var dataFormat = workbook.CreateDataFormat();
            var dateStyle = workbook.CreateCellStyle();
            // Excel-style format, shows date + time; adjust if you prefer other formatting.
            dateStyle.DataFormat = dataFormat.GetFormat("yyyy-mm-dd hh:mm:ss");

            // Optionally create a wrap style for header/body columns
            var wrapStyle = workbook.CreateCellStyle();
            wrapStyle.WrapText = true;

            // header row
            var header = sheet.CreateRow(0);
            header.CreateCell(0).SetCellValue("RequestId");
            header.CreateCell(1).SetCellValue("RequestUrl");
            header.CreateCell(2).SetCellValue("RequestMethod");
            header.CreateCell(3).SetCellValue("RequestTimestamp (UTC)");
            header.CreateCell(4).SetCellValue("ResponseTimestamp (UTC)");
            header.CreateCell(5).SetCellValue("LatencyMs");
            header.CreateCell(6).SetCellValue("Status");
            header.CreateCell(7).SetCellValue("RequestHeaders");
            header.CreateCell(8).SetCellValue("ResponseHeaders");
            header.CreateCell(9).SetCellValue("RequestBody");
            header.CreateCell(10).SetCellValue("ResponseBody");
            header.CreateCell(11).SetCellValue("ResponseResourceType");

            int rowIndex = 1;
            foreach (var e in list)
            {
                var row = sheet.CreateRow(rowIndex++);

                row.CreateCell(0).SetCellValue(e.RequestId ?? "");
                row.CreateCell(1).SetCellValue(e.RequestUrl ?? "");
                row.CreateCell(2).SetCellValue(e.RequestMethod ?? "");

                // Request timestamp as Excel Date cell (UTC)
                var reqCell = row.CreateCell(3);
                if (e.RequestTimestamp.HasValue)
                {
                    // Convert to UTC to have a consistent timezone in the sheet
                    DateTime dt = e.RequestTimestamp.Value.ToUniversalTime();
                    reqCell.SetCellValue(dt);
                    reqCell.CellStyle = dateStyle;
                }
                else
                {
                    reqCell.SetCellValue("");
                }

                // Response timestamp as Excel Date cell (UTC)
                var respCell = row.CreateCell(4);
                if (e.ResponseTimestamp.HasValue)
                {
                    DateTime dt = e.ResponseTimestamp.Value.ToUniversalTime();
                    respCell.SetCellValue(dt);
                    respCell.CellStyle = dateStyle;
                }
                else
                {
                    respCell.SetCellValue("");
                }

                // Latency: prefer stored LatencyMs, otherwise compute from timestamps if both present
                long? latency = e.LatencyMs;
                if (!latency.HasValue && e.RequestTimestamp.HasValue && e.ResponseTimestamp.HasValue)
                {
                    latency = (long)(e.ResponseTimestamp.Value - e.RequestTimestamp.Value).TotalMilliseconds;
                }

                var latencyCell = row.CreateCell(5);
                if (latency.HasValue)
                    latencyCell.SetCellValue((double)latency.Value);
                else
                    latencyCell.SetCellValue("");

                // Status code
                var statusCell = row.CreateCell(6);
                statusCell.SetCellValue((double)e.ResponseStatusCode);

                // Headers serialized as multi-line text
                var reqHeadersText = e.RequestHeaders != null ? string.Join("\n", e.RequestHeaders.Select(kv => $"{kv.Key}: {kv.Value}")) : "";
                var resHeadersText = e.ResponseHeaders != null ? string.Join("\n", e.ResponseHeaders.Select(kv => $"{kv.Key}: {kv.Value}")) : "";

                var reqHeadersCell = row.CreateCell(7);
                reqHeadersCell.SetCellValue(reqHeadersText);
                reqHeadersCell.CellStyle = wrapStyle;

                var resHeadersCell = row.CreateCell(8);
                resHeadersCell.SetCellValue(resHeadersText);
                resHeadersCell.CellStyle = wrapStyle;

                var reqBodyCell = row.CreateCell(9);
                reqBodyCell.SetCellValue(e.RequestPostData ?? "");
                reqBodyCell.CellStyle = wrapStyle;

                var respBodyCell = row.CreateCell(10);
                respBodyCell.SetCellValue(e.ResponseBody ?? "");
                respBodyCell.CellStyle = wrapStyle;

                row.CreateCell(11).SetCellValue(e.ResponseResourceType ?? "");
            }

            // Auto-size columns (best-effort). For very large sheets this may be slow.
            for (int c = 0; c <= 11; c++)
            {
                try
                {
                    sheet.AutoSizeColumn(c);
                }
                catch
                {
                    // ignore exceptions
                }
            }

            // Ensure output directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            // Save workbook to disk
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fs);
            }
        }
    }
}
