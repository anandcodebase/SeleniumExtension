

namespace SimpleSeleniumSupport.Network
{
    /// <summary>
    /// Combines Captured network information
    /// </summary>
    public static class CaptureExtensions
    {
        /// <summary>
        /// Convert Capture's request/response logs into FullNetworkInfo objects.
        /// This is defensive: if Capture already exposes a combined list, return that.
        /// Otherwise, attempt to map RequestSent and ResponseReceived into FullNetworkInfo.
        /// </summary>
        /// <param name="capture">The capture.</param>
        /// <returns></returns>
        public static List<FullNetworkInfo> GetCombinedNetworkInfo(this Capture capture)
        {
            var result = new List<FullNetworkInfo>();
            if (capture == null) return result;

            // If Capture already has a combined list property, use it (reflection to avoid compile-time coupling)
            try
            {
                var prop = capture.GetType().GetProperty("CombinedNetworkInfo");
                if (prop != null)
                {
                    var val = prop.GetValue(capture) as IEnumerable<FullNetworkInfo>;
                    if (val != null) return val.ToList();
                }
            }
            catch { /* ignore */ }

            // Attempt to find RequestSent and ResponseReceived lists (by property name)
            try
            {
                var reqProp = capture.GetType().GetProperty("Requests");
                var respProp = capture.GetType().GetProperty("Responses");

                var requests = reqProp?.GetValue(capture) as IEnumerable<RequestSent>;
                var responses = respProp?.GetValue(capture) as IEnumerable<ResponseReceived>;

                if (requests != null)
                {
                    foreach (var r in requests)
                    {
                        // try to find matching response
                        ResponseReceived? matched = null;
                        if (responses != null)
                        {
                            matched = responses.FirstOrDefault(x => !string.IsNullOrEmpty(x.RequestId) && x.RequestId == r.RequestId
                                                                   || (!string.IsNullOrEmpty(x.ResponseUrl) && x.ResponseUrl == r.RequestUrl));
                        }

                        var info = new FullNetworkInfo
                        {
                            RequestId = r.RequestId,
                            RequestUrl = r.RequestUrl,
                            RequestMethod = r.RequestMethod,
                            RequestHeaders = r.RequestHeaders,
                            RequestPostData = r.RequestPostData,
                            RequestTimestamp = r.RequestTimestamp,
                            ResponseTimestamp = matched != null ? matched.ResponseTimestamp : (DateTime?)null,
                            ResponseStatusCode = matched?.ResponseStatusCode ?? 0,
                            ResponseHeaders = matched?.ResponseHeaders,
                            ResponseBody = matched?.ResponseBody,
                            ResponseContent = matched?.ResponseContent,
                            ResponseResourceType = matched?.ResponseResourceType
                        };

                        // optional latency calculation (if both timestamps exist)
                        if (info.RequestTimestamp.HasValue && info.ResponseTimestamp.HasValue)
                        {
                            info.LatencyMs = (long)(info.ResponseTimestamp.Value - info.RequestTimestamp.Value).TotalMilliseconds;
                        }

                        result.Add(info);
                    }
                }
                else if (responses != null)
                {
                    // Only responses present (map them)
                    foreach (var r in responses)
                    {
                        var info = new FullNetworkInfo
                        {
                            RequestId = r.RequestId,
                            RequestUrl = r.ResponseUrl,
                            ResponseTimestamp = r.ResponseTimestamp,
                            ResponseStatusCode = r.ResponseStatusCode,
                            ResponseHeaders = r.ResponseHeaders,
                            ResponseBody = r.ResponseBody,
                            ResponseContent = r.ResponseContent,
                            ResponseResourceType = r.ResponseResourceType
                        };
                        result.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CaptureExtensions] Failed to extract network info: {ex.Message}");
            }

            return result;
        }
    }
}
