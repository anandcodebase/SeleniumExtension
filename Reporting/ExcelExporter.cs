using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using SimpleSeleniumSupport.Network;

namespace SimpleSeleniumSupport.Reporting
{
    public static class ExcelExporter
    {
        // ── TestResult export ──────────────────────────────────────────────────

        /// <summary>
        /// Exports a collection of <see cref="TestResult"/> objects to an Excel <c>.xlsx</c> workbook.
        /// One row per test result. Columns cover all identity, outcome, environment, failure,
        /// artifact, AI-analysis, run-name, and custom-property fields.
        /// Status cells are colour-coded (green / red / orange / grey).
        /// </summary>
        /// <param name="results">Test results to export.</param>
        /// <param name="path">Output <c>.xlsx</c> file path. Directory is created if needed.</param>
        public static void ExportTestResultsToExcel(IEnumerable<TestResult> results, string path)
        {
            var list = results?.ToList() ?? new List<TestResult>();

            IWorkbook wb    = new XSSFWorkbook();
            var sheet       = wb.CreateSheet("Test Results");
            var dataFormat  = wb.CreateDataFormat();

            // ── Cell styles ───────────────────────────────────────────────────
            var dateStyle = wb.CreateCellStyle();
            dateStyle.DataFormat = dataFormat.GetFormat("yyyy-mm-dd hh:mm:ss");

            var headerStyle = wb.CreateCellStyle();
            headerStyle.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;
            headerStyle.FillPattern         = FillPattern.SolidForeground;
            var headerFont = wb.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);

            var wrapStyle = wb.CreateCellStyle();
            wrapStyle.WrapText = true;

            // Status colour styles (IndexedColors — broadest NPOI compatibility)
            var passStyle  = MakeColorStyle(wb, IndexedColors.LightGreen.Index);
            var failStyle  = MakeColorStyle(wb, IndexedColors.Rose.Index);
            var errorStyle = MakeColorStyle(wb, IndexedColors.LightOrange.Index);
            var skipStyle  = MakeColorStyle(wb, IndexedColors.Grey25Percent.Index);

            // ── Header row ────────────────────────────────────────────────────
            string[] headers =
            [
                "#", "Run", "Test Name", "Suite", "Full Name", "Category", "Tags",
                "Status", "Duration (ms)", "Start Time (UTC)",
                "Browser", "Environment", "Machine",
                "Exception Type", "Exception Message", "Stack Trace", "Assert Message", "Skip Reason",
                "Screenshot", "Screencast", "Diagnostics Folder", "Network HAR", "Network Excel",
                "AI Analysis", "Custom Properties"
            ];

            var hdr = sheet.CreateRow(0);
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = hdr.CreateCell(c);
                cell.SetCellValue(headers[c]);
                cell.CellStyle = headerStyle;
            }

            // ── Data rows ─────────────────────────────────────────────────────
            var jsonOpts = new JsonSerializerOptions { WriteIndented = false };

            for (int i = 0; i < list.Count; i++)
            {
                var r   = list[i];
                var row = sheet.CreateRow(i + 1);
                int c   = 0;

                row.CreateCell(c++).SetCellValue(i + 1);                             // #
                Str(row, c++, r.RunName);                                            // Run
                Str(row, c++, r.TestName);                                           // Test Name
                Str(row, c++, r.TestSuite);                                          // Suite
                Str(row, c++, r.FullName);                                           // Full Name
                Str(row, c++, r.Category);                                           // Category
                Str(row, c++, r.Tags);                                               // Tags

                // Status — colour coded
                var statusCell = row.CreateCell(c++);
                statusCell.SetCellValue(r.Status.ToString());
                statusCell.CellStyle = r.Status switch
                {
                    TestStatus.Pass  => passStyle,
                    TestStatus.Fail  => failStyle,
                    TestStatus.Error => errorStyle,
                    TestStatus.Skip  => skipStyle,
                    _                => skipStyle
                };

                row.CreateCell(c++).SetCellValue(r.DurationMs);                      // Duration (ms)

                // Start Time as real Excel datetime
                var dtCell = row.CreateCell(c++);
                dtCell.SetCellValue(r.StartTime.ToUniversalTime());
                dtCell.CellStyle = dateStyle;

                Str(row, c++, r.Browser);                                            // Browser
                Str(row, c++, r.Environment);                                        // Environment
                Str(row, c++, r.MachineName);                                        // Machine
                Str(row, c++, r.ExceptionType);                                      // Exception Type
                WrapStr(row, c++, r.ExceptionMessage, wrapStyle);                    // Exception Message
                WrapStr(row, c++, r.StackTrace,       wrapStyle);                    // Stack Trace
                WrapStr(row, c++, r.AssertMessage,    wrapStyle);                    // Assert Message
                Str(row, c++, r.SkipReason);                                         // Skip Reason
                Str(row, c++, r.ScreenshotPath);                                     // Screenshot
                Str(row, c++, r.ScreencastPath);                                     // Screencast
                Str(row, c++, r.DiagnosticsFolder);                                  // Diagnostics
                Str(row, c++, r.NetworkHarPath);                                     // HAR
                Str(row, c++, r.NetworkExcelPath);                                   // Network Excel
                WrapStr(row, c++, r.AiAnalysis, wrapStyle);                          // AI Analysis

                // Custom Properties — serialised as "key=value" pairs, one per line
                var cp = r.CustomProperties?.Count > 0
                    ? string.Join("\n", r.CustomProperties.Select(kv => $"{kv.Key}={kv.Value}"))
                    : "";
                WrapStr(row, c++, cp, wrapStyle);                                    // Custom Properties
            }

            // ── AutoFilter + freeze header ────────────────────────────────────
            int lastCol = headers.Length - 1;
            try
            {
                sheet.SetAutoFilter(new CellRangeAddress(0, list.Count, 0, lastCol));
                sheet.CreateFreezePane(0, 1);
            }
            catch { /* non-fatal */ }

            // ── Column widths ─────────────────────────────────────────────────
            // Auto-size narrow columns; cap wide text columns to avoid extreme widths.
            int[] autoSizeCols  = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 17, 18, 19, 20, 21];
            int[] wideTextCols  = [13, 14, 15, 16, 22, 23, 24];   // cap at 60 chars wide
            foreach (var col in autoSizeCols) { try { sheet.AutoSizeColumn(col); } catch { } }
            foreach (var col in wideTextCols) { try { sheet.SetColumnWidth(col, 60 * 256); } catch { } }

            // ── Write file ────────────────────────────────────────────────────
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            wb.Write(fs);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ICellStyle MakeColorStyle(IWorkbook wb, short indexedColor)
        {
            var style = wb.CreateCellStyle();
            style.FillForegroundColor = indexedColor;
            style.FillPattern         = FillPattern.SolidForeground;
            return style;
        }

        private static void Str(IRow row, int col, string? value)
        {
            row.CreateCell(col).SetCellValue(Trim(value));
        }

        private static void WrapStr(IRow row, int col, string? value, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.SetCellValue(Trim(value));
            cell.CellStyle = style;
        }

        private static string Trim(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length > 32767 ? value.Substring(0, 32767) : value;
        }


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
