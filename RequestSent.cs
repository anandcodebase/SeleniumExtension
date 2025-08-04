using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleSeleniumSupport
{
    public class RequestSent
    {
        public string RequestId { get; set; }
        public string RequestUrl { get; set; }
        public string RequestMethod { get; set; }
        public Dictionary<string, string> RequestHeaders { get; set; }
        public string RequestPostData { get; set; }
        public DateTime RequestTimestamp { get; set; }
    }
}
