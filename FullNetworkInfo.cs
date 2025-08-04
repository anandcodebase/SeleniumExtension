using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleSeleniumSupport
{
    public class FullNetworkInfo
    {
        public string RequestId { get; set; }
        public DateTime RequestTimestamp { get; set; }
        public string RequestMethod { get; set; }
        public string RequestUrl { get; set; }
        public Dictionary<string, string> RequestHeaders { get; set; }
        public string RequestPostData { get; set; }

        public DateTime ResponseTimestamp { get; set; }
        public string ResponseUrl { get; set; }
        public long ResponseStatusCode { get; set; }
        public Dictionary<string, string> ResponseHeaders { get; set; }
        public string ResponseBody { get; set; }
        public string ResponseContent { get; set; }
        public string ResponseResourceType { get; set; }
        public long? LatencyMs { get; set; } // Time between request sent and response received
    }
}
