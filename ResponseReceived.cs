using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleSeleniumSupport
{
    public class ResponseReceived
    {
        public string RequestId { get; set; }
        public string ResponseUrl { get; set; }
        public long ResponseStatusCode { get; set; }
        public string ResponseBody { get; set; } 
        public string ResponseContent { get; set; } 
        public string ResponseResourceType { get; set; }
        public Dictionary<string, string> ResponseHeaders { get; set; }
        public DateTime ResponseTimestamp { get; set; }
    }
}
