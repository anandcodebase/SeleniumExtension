using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeleniumExtention
{
    public class ResponseReceived
    {
        public string RequestId { get; set; }
        public string ResponseUrl { get; set; }
        public long ResponseStatusCode { get; set; }
        public string ResponseBody { get; set; } // Note: This might be null for large responses or if not explicitly retrieved
        public string ResponseContent { get; set; } // Selenium's NetworkResponseReceivedEventArgs.ResponseContent might contain body if available
        public string ResponseResourceType { get; set; }
        public Dictionary<string, string> ResponseHeaders { get; set; }
        public DateTime ResponseTimestamp { get; set; }
    }
}
