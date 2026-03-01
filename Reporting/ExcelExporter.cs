using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using SimpleSeleniumSupport.Network;

namespace SimpleSeleniumSupport.Reporting
{
    public static class ExcelExporter
    {
        // Excel's maximum number of characters in a cell
        // Official limit: 32,767
        private const int DefaultMaxCellLength = 32767;

        /// <summary>
        /// Export network info rows to an Excel .xlsx file using NPOI.
        /// Writes timestamps as true Excel datetime cells, trims long text to Excel limits,
        /// and enables AutoFilter on the header row.
        /// </summary>
        /// <param name="items">Network info rows</param>
        /// <param name="path">Output .xlsx path</param>
        /// <param name="maxCellLength">Optional: maximum characters to write per cell (default 32767)</param>
        public static void ExportNetworkInfoToExcel(IEnumerable<FullNetworkInfo> items, string path, int maxCellLength = DefaultMaxCellLength)
        {
            if (maxCellLength <= 0) maxCellLength = DefaultMaxCellLength;

            var list = items?.ToList() ?? new List<FullNetworkInfo>();

            IWorkbook workbook = new XSSFWorkbook(); // .xlsx
            var sheet = workbook.CreateSheet("Network");

            // Create a date cell style (Excel datetime)
            var dataFormat = workbook.CreateDataFormat();
            var dateStyle = workbook.CreateCellStyle();
            dateStyle.DataFormat = dataFormat.GetFormat("yyyy-mm-dd hh:mm:ss");

            // Wrap style for large text columns
            var wrapStyle = workbook.CreateCellStyle();
            wrapStyle.WrapText = true;

            // Optionally apply a monospace font for code-like columns (request/response body) - left as default.

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

                CreateStringCell(row, 0, e.RequestId ?? "", null, maxCellLength);
                CreateStringCell(row, 1, e.RequestUrl ?? "", null, maxCellLength);
                CreateStringCell(row, 2, e.RequestMethod ?? "", null, maxCellLength);

                // Request timestamp as Excel Date cell (UTC)
                var reqCell = row.CreateCell(3);
                if (e.RequestTimestamp.HasValue)
                {
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

                // Latency
                long? latency = e.LatencyMs;
                if (!latency.HasValue && e.RequestTimestamp.HasValue && e.ResponseTimestamp.HasValue)
                {
                    latency = (long)(e.ResponseTimestamp.Value - e.RequestTimestamp.Value).TotalMilliseconds;
                }

                if (latency.HasValue)
                    row.CreateCell(5).SetCellValue((double)latency.Value);
                else
                    row.CreateCell(5).SetCellValue("");

                // Status code (numeric)
                row.CreateCell(6).SetCellValue((double)e.ResponseStatusCode);

                // Headers serialized as multi-line text (trimmed)
                var reqHeadersText = e.RequestHeaders != null ? string.Join("\n", e.RequestHeaders.Select(kv => $"{kv.Key}: {kv.Value}")) : "";
                var resHeadersText = e.ResponseHeaders != null ? string.Join("\n", e.ResponseHeaders.Select(kv => $"{kv.Key}: {kv.Value}")) : "";

                CreateStringCell(row, 7, reqHeadersText, wrapStyle, maxCellLength);
                CreateStringCell(row, 8, resHeadersText, wrapStyle, maxCellLength);
                CreateStringCell(row, 9, e.RequestPostData ?? "", wrapStyle, maxCellLength);
                CreateStringCell(row, 10, e.ResponseBody ?? "", wrapStyle, maxCellLength);
                CreateStringCell(row, 11, e.ResponseResourceType ?? "", null, maxCellLength);
            }

            // Enable AutoFilter on header row across all used columns (0..11)
            try
            {
                sheet.SetAutoFilter(new CellRangeAddress(0, rowIndex - 1, 0, 11));
                // Freeze header row for convenience
                sheet.CreateFreezePane(0, 1);
            }
            catch
            {
                // ignore if auto-filter fails for some reason
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
                    // ignore potential exceptions for very wide content
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

        /// <summary>
        /// Helper to create string cells that trims values to Excel's maximum allowed cell length.
        /// Optionally applies a cell style (e.g., wrap).
        /// </summary>
        private static void CreateStringCell(IRow row, int colIndex, string value, ICellStyle? style, int maxCellLength)
        {
            var cell = row.CreateCell(colIndex);
            if (string.IsNullOrEmpty(value))
            {
                cell.SetCellValue("");
                if (style != null) cell.CellStyle = style;
                return;
            }

            string trimmed = value;
            if (trimmed.Length > maxCellLength)
            {
                // Simple truncation; you could append ellipsis or a summary if desired.
                trimmed = trimmed.Substring(0, maxCellLength);
                // Optionally add a small marker to denote truncation:
                // trimmed = trimmed.Substring(0, maxCellLength - 3) + "...";
            }

            cell.SetCellValue(trimmed);
            if (style != null) cell.CellStyle = style;
        }
    }
}
