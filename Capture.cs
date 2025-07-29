using OpenQA.Selenium;
using OpenQA.Selenium.DevTools.V138.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace SeleniumExtention
{
    public class Capture
    {
        private Task _monitoringTask;
        private ConcurrentDictionary<string, ResponseReceived> _responseReceivedMap = new ConcurrentDictionary<string, ResponseReceived>();
        private ConcurrentDictionary<string, RequestSent> _requestSentMap = new ConcurrentDictionary<string, RequestSent>();
        private readonly WebDriver _driver; // Store the driver instance

        public Capture(WebDriver driver)
        {
            if (driver is not OpenQA.Selenium.Chrome.ChromeDriver chromeDriver)
            {
                throw new ArgumentException("Network monitoring is currently only supported for ChromeDriver.", nameof(driver));
            }
            _driver = driver; // Store the driver
            chromeDriver.Manage().Network.NetworkResponseReceived += ResponseReceivedHandler;
            chromeDriver.Manage().Network.NetworkRequestSent += RequestSentHandler;
        }

        public async Task StartMonitoringAsync()
        {
            if (_monitoringTask != null && !_monitoringTask.IsCompleted)
            {
                Console.WriteLine("Monitoring is already active.");
                return;
            }

            _monitoringTask = ((OpenQA.Selenium.Chrome.ChromeDriver)_driver).Manage().Network.StartMonitoring();
            await _monitoringTask;
            Console.WriteLine("Network monitoring started.");
        }

        public async Task StopMonitoringAsync()
        {
            if (_monitoringTask == null || _monitoringTask.IsCompleted)
            {
                Console.WriteLine("Monitoring is not active.");
                return;
            }

            _monitoringTask = ((OpenQA.Selenium.Chrome.ChromeDriver)_driver).Manage().Network.StopMonitoring();
            await _monitoringTask;
            _monitoringTask.Dispose();
            Console.WriteLine("Network monitoring stopped.");
        }

        private void ResponseReceivedHandler(object sender, NetworkResponseReceivedEventArgs e)
        {
            var responseContent = string.Empty;
            if (e.ResponseContent != null)
            {
                responseContent = e.ResponseContent.ToString();
            }
            var response = new ResponseReceived
            {
                RequestId = e.RequestId,
                ResponseUrl = e.ResponseUrl,
                ResponseStatusCode = e.ResponseStatusCode,
                ResponseBody = e.ResponseBody,
                ResponseContent = responseContent,
                ResponseResourceType = e.ResponseResourceType,
                ResponseHeaders = e.ResponseHeaders != null ? new Dictionary<string, string>(e.ResponseHeaders) : new Dictionary<string, string>(),
                ResponseTimestamp = DateTime.Now
            };
            _responseReceivedMap.TryAdd(e.RequestId, response);
        }

        private void RequestSentHandler(object sender, NetworkRequestSentEventArgs e)
        {
            var request = new RequestSent
            {
                RequestHeaders = e.RequestHeaders != null ? new Dictionary<string, string>(e.RequestHeaders) : new Dictionary<string, string>(),
                RequestId = e.RequestId,
                RequestUrl = e.RequestUrl,
                RequestMethod = e.RequestMethod,
                RequestPostData = e.RequestPostData,
                RequestTimestamp = DateTime.Now
            };
            _requestSentMap.TryAdd(e.RequestId, request);
        }

        public IReadOnlyList<FullNetworkInfo> GetCombinedNetworkInfo()
        {
            List<FullNetworkInfo> combinedList = new List<FullNetworkInfo>();

            foreach (var requestId in _requestSentMap.Keys.Union(_responseReceivedMap.Keys).Distinct())
            {
                _requestSentMap.TryGetValue(requestId, out var request);
                _responseReceivedMap.TryGetValue(requestId, out var response);

                var fullInfo = new FullNetworkInfo
                {
                    RequestId = requestId,
                    RequestTimestamp = request?.RequestTimestamp ?? DateTime.MinValue,
                    RequestMethod = request?.RequestMethod ?? "N/A",
                    RequestUrl = request?.RequestUrl ?? "N/A",
                    RequestHeaders = request?.RequestHeaders ?? new Dictionary<string, string>(),
                    RequestPostData = request?.RequestPostData,
                    ResponseTimestamp = response?.ResponseTimestamp ?? DateTime.MinValue,
                    ResponseUrl = response?.ResponseUrl ?? "N/A",
                    ResponseStatusCode = response?.ResponseStatusCode ?? 0,
                    ResponseHeaders = response?.ResponseHeaders ?? new Dictionary<string, string>(),
                    ResponseBody = response?.ResponseBody,
                    ResponseContent = response?.ResponseContent,
                    ResponseResourceType = response?.ResponseResourceType ?? "N/A"
                };

                if (request != null && response != null)
                {
                    fullInfo.LatencyMs = (long?)(response.ResponseTimestamp - request.RequestTimestamp).TotalMilliseconds;
                }

                combinedList.Add(fullInfo);
            }

            return combinedList.OrderBy(x => x.RequestTimestamp).ToList().AsReadOnly();
        }

        public void ClearCapturedData()
        {
            _requestSentMap.Clear();
            _responseReceivedMap.Clear();
            Console.WriteLine("Cleared all captured network data.");
        }
    }
}