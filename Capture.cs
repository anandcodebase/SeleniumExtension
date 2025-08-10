using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium.DevTools.V138.Network;

namespace SimpleSeleniumSupport
{
    public class Capture
    {
        private readonly IWebDriver _driver;
        private readonly ConcurrentDictionary<string, RequestSent> _requestSentMap = new ConcurrentDictionary<string, RequestSent>();
        private readonly ConcurrentDictionary<string, ResponseReceived> _responseReceivedMap = new ConcurrentDictionary<string, ResponseReceived>();
        private INetwork _networkInterceptor;

        public Capture(IWebDriver driver)
        {
            if (driver is not IDevTools)
            {
                throw new ArgumentException("Network monitoring is only supported for drivers that implement IHasDevTools (e.g., ChromeDriver, EdgeDriver).", nameof(driver));
            }
            _driver = driver;
        }

        /// <summary>
        /// Starts monitoring network traffic synchronously.
        /// </summary>
        public void StartMonitoring()
        {
            if (_networkInterceptor != null)
            {
                Console.WriteLine("Monitoring is already active.");
                return;
            }

            // Use the high-level network manager
            _networkInterceptor = _driver.Manage().Network;
            _networkInterceptor.NetworkRequestSent += RequestSentHandler;
            _networkInterceptor.NetworkResponseReceived += ResponseReceivedHandler;
            _networkInterceptor.StartMonitoring().GetAwaiter().GetResult();
            Console.WriteLine("Network monitoring started.");
        }

        /// <summary>
        /// Stops monitoring network traffic synchronously.
        /// </summary>
        public void StopMonitoring()
        {
            if (_networkInterceptor == null)
            {
                Console.WriteLine("Monitoring is not active.");
                return;
            }

            _networkInterceptor.StopMonitoring().GetAwaiter().GetResult();
            _networkInterceptor.NetworkRequestSent -= RequestSentHandler;
            _networkInterceptor.NetworkResponseReceived -= ResponseReceivedHandler;
            _networkInterceptor = null;
            Console.WriteLine("Network monitoring stopped.");
        }

        private void ResponseReceivedHandler(object sender, NetworkResponseReceivedEventArgs e)
        {
            var response = new ResponseReceived
            {
                RequestId = e.RequestId,
                ResponseUrl = e.ResponseUrl,
                ResponseStatusCode = e.ResponseStatusCode,
                ResponseHeaders = e.ResponseHeaders.ToDictionary(k => k.Key, v => v.Value.ToString()),
                ResponseResourceType = e.ResponseResourceType.ToString(),
                ResponseBody = e.ResponseBody,
                ResponseTimestamp = DateTime.Now
            };
            _responseReceivedMap.TryAdd(e.RequestId, response);
        }

        private void RequestSentHandler(object sender, NetworkRequestSentEventArgs e)
        {
            var request = new RequestSent
            {
                RequestId = e.RequestId,
                RequestUrl = e.RequestUrl,
                RequestMethod = e.RequestMethod,
                RequestHeaders = e.RequestHeaders.ToDictionary(k => k.Key, v => v.Value.ToString()),
                RequestPostData = e.RequestPostData,
                RequestTimestamp = DateTime.Now
            };
            _requestSentMap.TryAdd(e.RequestId, request);
        }

        public IReadOnlyList<FullNetworkInfo> GetCombinedNetworkInfo()
        {
            var combinedList = new List<FullNetworkInfo>();
            var allRequestIds = _requestSentMap.Keys.Union(_responseReceivedMap.Keys).Distinct();

            foreach (var requestId in allRequestIds)
            {
                _requestSentMap.TryGetValue(requestId, out var request);
                _responseReceivedMap.TryGetValue(requestId, out var response);

                var fullInfo = new FullNetworkInfo
                {
                    RequestId = requestId,
                    RequestTimestamp = request?.RequestTimestamp ?? DateTime.MinValue,
                    RequestMethod = request?.RequestMethod ?? "N/A",
                    RequestUrl = request?.RequestUrl ?? response?.ResponseUrl ?? "N/A",
                    RequestHeaders = request?.RequestHeaders ?? new Dictionary<string, string>(),
                    RequestPostData = request?.RequestPostData,
                    ResponseTimestamp = response?.ResponseTimestamp ?? DateTime.MinValue,
                    ResponseUrl = response?.ResponseUrl ?? "N/A",
                    ResponseStatusCode = response?.ResponseStatusCode ?? 0,
                    ResponseHeaders = response?.ResponseHeaders ?? new Dictionary<string, string>(),
                    ResponseBody = response?.ResponseBody,
                    ResponseResourceType = response?.ResponseResourceType ?? "N/A",
                    LatencyMs = (request != null && response != null) ? (long?)(response.ResponseTimestamp - request.RequestTimestamp).TotalMilliseconds : null
                };
                combinedList.Add(fullInfo);
            }
            return combinedList.OrderBy(x => x.RequestTimestamp).ToList().AsReadOnly();
        }

        /// <summary>
        /// Waits for a specific network request containing the given URL part to complete.
        /// </summary>
        public FullNetworkInfo WaitForRequest(string urlPart, int timeoutInSeconds = 30)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutInSeconds));
            return wait.Until(d =>
            {
                return GetCombinedNetworkInfo()
                    .FirstOrDefault(info => info.RequestUrl.Contains(urlPart) && info.ResponseStatusCode != 0);
            });
        }

        /// <summary>
        /// Waits for all currently tracked network requests to complete.
        /// </summary>
        public void WaitForAllRequests(int timeoutInSeconds = 30)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutInSeconds));
            wait.Until(d =>
            {
                // Considers completion when all sent requests have a corresponding response.
                return _requestSentMap.Keys.All(id => _responseReceivedMap.ContainsKey(id));
            });
        }

        public void ClearCapturedData()
        {
            _requestSentMap.Clear();
            _responseReceivedMap.Clear();
            Console.WriteLine("Cleared all captured network data.");
        }
    }
}
