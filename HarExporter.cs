
using System.Text.Json;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Minimal HAR exporter that writes a HAR 1.2-compatible JSON file from FullNetworkInfo entries.
    /// It is tolerant to missing timestamps/values.
    /// </summary>
    public static class HarExporter
    {
        /// <summary>
        /// Exports to har.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="filePath">The file path.</param>
        public static void ExportToHar(IEnumerable<FullNetworkInfo> entries, string filePath)
        {
            var list = entries?.ToList() ?? new List<FullNetworkInfo>();

            // Build HAR log
            var har = new
            {
                log = new
                {
                    version = "1.2",
                    creator = new { name = "SimpleSeleniumSupport.HarExporter", version = "1.0" },
                    pages = new object[0],
                    entries = list.Select(e => BuildEntry(e)).ToArray()
                }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(har, options);

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Builds the entry.
        /// </summary>
        /// <param name="e">The e.</param>
        /// <returns></returns>
        private static object BuildEntry(FullNetworkInfo e)
        {
            // Ensure we have a non-null DateTime for formatting
            DateTime started = e.RequestTimestamp ?? e.ResponseTimestamp ?? DateTime.UtcNow;
            var startedDateTime = started.ToString("o");

            // compute timings (best effort). If we have both timestamps use difference, else zero.
            double dns = -1, connect = -1, send = 0, wait = 0, receive = 0, blocked = -1;
            if (e.RequestTimestamp.HasValue && e.ResponseTimestamp.HasValue)
            {
                // subtract two DateTime values (both non-null here) to compute total milliseconds
                var totalMs = (e.ResponseTimestamp.Value - e.RequestTimestamp.Value).TotalMilliseconds;
                // crude split: assume send 10ms, receive 10ms, rest is wait
                send = 10;
                receive = 10;
                wait = Math.Max(0, totalMs - (send + receive));
            }

            var requestHeaders = SerializeHeaders(e.RequestHeaders);
            var responseHeaders = SerializeHeaders(e.ResponseHeaders);

            var queryString = BuildQueryStringFromUrl(e.RequestUrl);

            var postData = string.IsNullOrEmpty(e.RequestPostData) ? null : new
            {
                mimeType = e.RequestHeaders?.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? "application/octet-stream",
                text = e.RequestPostData
            };

            var responseContentText = e.ResponseBody;
            var mimeType = e.ResponseHeaders?.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? "application/octet-stream";

            return new
            {
                startedDateTime,
                time = Math.Max(0, send + wait + receive),
                request = new
                {
                    method = e.RequestMethod ?? "GET",
                    url = e.RequestUrl ?? string.Empty,
                    httpVersion = "HTTP/1.1",
                    headers = requestHeaders,
                    queryString = queryString,
                    headersSize = -1,
                    bodySize = string.IsNullOrEmpty(e.RequestPostData) ? 0 : (e.RequestPostData.Length),
                    postData
                },
                response = new
                {
                    status = e.ResponseStatusCode,
                    statusText = "", // unknown
                    httpVersion = "HTTP/1.1",
                    headers = responseHeaders,
                    content = new
                    {
                        size = responseContentText?.Length ?? 0,
                        mimeType = mimeType,
                        text = responseContentText
                    },
                    redirectURL = "",
                    headersSize = -1,
                    bodySize = responseContentText?.Length ?? 0
                },
                cache = new { },
                timings = new { blocked, dns, connect, send, wait, receive }
            };
        }

        /// <summary>
        /// Serializes the headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        private static object[] SerializeHeaders(IDictionary<string, string> headers)
        {
            if (headers == null) return Array.Empty<object>();
            return headers.Select(kv => new { name = kv.Key ?? "", value = kv.Value ?? "" }).ToArray();
        }

        /// <summary>
        /// Builds the query string from URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        private static object[] BuildQueryStringFromUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return Array.Empty<object>();
                var uri = new Uri(url);
                var q = uri.Query;
                if (string.IsNullOrWhiteSpace(q)) return Array.Empty<object>();
                // trim leading '?'
                var qs = q.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                return qs.Select(pair =>
                {
                    var parts = pair.Split('=', 2);
                    return new { name = Uri.UnescapeDataString(parts[0]), value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "" };
                }).ToArray();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }
    }
}
