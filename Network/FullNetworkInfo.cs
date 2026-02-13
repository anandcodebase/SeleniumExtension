

namespace SimpleSeleniumSupport.Network
{
    public class FullNetworkInfo
    {
        public string RequestId { get; set; }

        // Use nullable DateTime so helper code can leave timestamps empty when not available.
        public DateTime? RequestTimestamp { get; set; }
        public string RequestMethod { get; set; }
        public string RequestUrl { get; set; }
        public Dictionary<string, string> RequestHeaders { get; set; }
        public string RequestPostData { get; set; }

        // Nullable Response timestamp as well
        public DateTime? ResponseTimestamp { get; set; }
        public string ResponseUrl { get; set; }
        public long ResponseStatusCode { get; set; }
        public Dictionary<string, string> ResponseHeaders { get; set; }
        public string ResponseBody { get; set; }
        public string ResponseContent { get; set; }

        // Keep existing property name from your uploaded file
        public string ResponseResourceType { get; set; }

        public long? LatencyMs { get; set; } // Time between request sent and response received (optional)
    }
}
