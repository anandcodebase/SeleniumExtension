using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeleniumExtention
{
    public class ExcelExporter
    {
        public static void ExportNetworkInfoToExcel(IReadOnlyList<FullNetworkInfo> networkInfo, string filePath)
        {
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Network Traffic");

            IRow headerRow = sheet.CreateRow(0);
            int colIndex = 0;
            headerRow.CreateCell(colIndex++).SetCellValue("Request ID");
            headerRow.CreateCell(colIndex++).SetCellValue("Request Timestamp");
            headerRow.CreateCell(colIndex++).SetCellValue("Method");
            headerRow.CreateCell(colIndex++).SetCellValue("Request URL");
            headerRow.CreateCell(colIndex++).SetCellValue("Request Headers");
            headerRow.CreateCell(colIndex++).SetCellValue("Request Post Data");
            headerRow.CreateCell(colIndex++).SetCellValue("Response Timestamp");
            headerRow.CreateCell(colIndex++).SetCellValue("Response URL");
            headerRow.CreateCell(colIndex++).SetCellValue("Status Code");
            headerRow.CreateCell(colIndex++).SetCellValue("Response Headers");
            headerRow.CreateCell(colIndex++).SetCellValue("Response Body (First 500 Chars)");
            headerRow.CreateCell(colIndex++).SetCellValue("Response Content (First 500 Chars)");
            headerRow.CreateCell(colIndex++).SetCellValue("Resource Type");
            headerRow.CreateCell(colIndex++).SetCellValue("Latency (ms)");

            int rowIndex = 1;
            foreach (var info in networkInfo)
            {
                IRow dataRow = sheet.CreateRow(rowIndex++);
                colIndex = 0;
                dataRow.CreateCell(colIndex++).SetCellValue(info.RequestId);
                dataRow.CreateCell(colIndex++).SetCellValue(info.RequestTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                dataRow.CreateCell(colIndex++).SetCellValue(info.RequestMethod);
                dataRow.CreateCell(colIndex++).SetCellValue(info.RequestUrl);
                dataRow.CreateCell(colIndex++).SetCellValue(FormatDictionary(info.RequestHeaders));
                dataRow.CreateCell(colIndex++).SetCellValue(info.RequestPostData);
                dataRow.CreateCell(colIndex++).SetCellValue(info.ResponseTimestamp != DateTime.MinValue ? info.ResponseTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff") : "N/A");
                dataRow.CreateCell(colIndex++).SetCellValue(info.ResponseUrl);
                dataRow.CreateCell(colIndex++).SetCellValue(info.ResponseStatusCode);
                dataRow.CreateCell(colIndex++).SetCellValue(FormatDictionary(info.ResponseHeaders));
                dataRow.CreateCell(colIndex++).SetCellValue(TruncateString(info.ResponseBody, 500));
                dataRow.CreateCell(colIndex++).SetCellValue(TruncateString(info.ResponseContent, 500));
                dataRow.CreateCell(colIndex++).SetCellValue(info.ResponseResourceType);
                dataRow.CreateCell(colIndex++).SetCellValue(info.LatencyMs.HasValue ? info.LatencyMs.Value.ToString() : "N/A");
            }



            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fileStream);
            }

            Console.WriteLine($"Network traffic data exported to: {filePath}");
        }

        private static string FormatDictionary(Dictionary<string, string> dict)
        {
            if (dict == null || !dict.Any())
                return "";
            return string.Join(";", dict.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        private static string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}
