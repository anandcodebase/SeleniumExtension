using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace SimpleSeleniumSupport.Network
{
    /// <summary>
    /// Passed to a route handler registered via <c>router.Route(pattern, handler)</c>.
    /// Call <see cref="Fulfill"/>, <see cref="Abort"/>, or <see cref="Continue"/> to control
    /// how the intercepted request is handled.
    /// </summary>
    public sealed class Route
    {
        private readonly RouteDecision _decision;
        private bool _handled;

        /// <summary>Details of the intercepted request.</summary>
        public RouteRequest Request { get; }

        internal Route(RouteRequest request, RouteDecision decision)
        {
            Request = request;
            _decision = decision;
        }

        // ─── Decision methods ─────────────────────────────────────────────────────

        /// <summary>Returns a mocked response to the browser.</summary>
        public void Fulfill(
            int statusCode = 200,
            string? body = null,
            string? contentType = null,
            IDictionary<string, string>? headers = null)
        {
            ThrowIfHandled();
            _handled = true;
            _decision.Type = RouteDecisionType.Fulfill;
            _decision.StatusCode = statusCode;
            _decision.Body = body ?? "";
            _decision.ContentType = contentType ?? "text/plain";
            _decision.ResponseHeaders = headers;
        }

        /// <summary>Aborts the request. The browser receives a network error.</summary>
        public void Abort()
        {
            ThrowIfHandled();
            _handled = true;
            _decision.Type = RouteDecisionType.Abort;
        }

        /// <summary>
        /// Lets the request continue to the network (with optional overrides).
        /// A fresh HTTP call is made from C# and the real response is returned.
        /// </summary>
        public void Continue(
            string? url = null,
            string? method = null,
            IDictionary<string, string>? headers = null,
            string? postData = null)
        {
            ThrowIfHandled();
            _handled = true;
            _decision.Type = RouteDecisionType.Continue;
            _decision.OverrideUrl = url;
            _decision.OverrideMethod = method;
            _decision.OverrideHeaders = headers;
            _decision.OverridePostData = postData;
        }

        private void ThrowIfHandled()
        {
            if (_handled)
                throw new InvalidOperationException(
                    "Route has already been handled by a previous Fulfill/Abort/Continue call.");
        }
    }

    // ─── Internal plumbing ────────────────────────────────────────────────────────

    internal enum RouteDecisionType { NotSet, Fulfill, Abort, Continue }

    internal sealed class RouteDecision
    {
        internal RouteDecisionType Type = RouteDecisionType.NotSet;
        internal int StatusCode;
        internal string Body = "";
        internal string ContentType = "text/plain";
        internal IDictionary<string, string>? ResponseHeaders;
        internal string? OverrideUrl;
        internal string? OverrideMethod;
        internal IDictionary<string, string>? OverrideHeaders;
        internal string? OverridePostData;
    }

    /// <summary>
    /// Internal wrapper that pairs a URL pattern with a user-supplied route action and
    /// creates a <see cref="NetworkRequestHandler"/> with the appropriate response supplier.
    /// </summary>
    internal sealed class RouteEntry
    {
        internal readonly string Pattern;
        internal readonly Action<Route> Action;
        internal readonly NetworkRequestHandler Handler;

        internal RouteEntry(string pattern, Action<Route> action)
        {
            Pattern = pattern;
            Action = action;
            Handler = BuildHandler(pattern, action);
        }

        private static NetworkRequestHandler BuildHandler(string pattern, Action<Route> action)
        {
            return new NetworkRequestHandler
            {
                RequestMatcher = req => RoutePattern.IsMatch(pattern, req.Url ?? ""),
                ResponseSupplier = req =>
                {
                    var routeReq = new RouteRequest
                    {
                        Url = req.Url ?? "",
                        Method = req.Method ?? "GET",
                        Headers = CopyHeaders(req.Headers),
                        PostData = req.PostData
                    };

                    var decision = new RouteDecision();
                    var route = new Route(routeReq, decision);

                    try { action(route); }
                    catch { decision.Type = RouteDecisionType.Continue; }

                    if (decision.Type == RouteDecisionType.NotSet)
                        decision.Type = RouteDecisionType.Continue;

                    switch (decision.Type)
                    {
                        case RouteDecisionType.Fulfill:
                            return BuildFulfillResponse(decision);

                        case RouteDecisionType.Abort:
                            // Return a valid HTTP response so CDP doesn't hang.
                            // Status 0 causes Fetch.fulfillRequest to fail and leaves
                            // Chrome waiting for a response timeout (42s+ hang).
                            var abortResp = new HttpResponseData { StatusCode = 200, Body = "" };
                            abortResp.Headers["Content-Length"] = "0";
                            return abortResp;

                        default: // Continue
                            return ProxySync(req, decision);
                    }
                }
            };
        }

        private static HttpResponseData BuildFulfillResponse(RouteDecision d)
        {
            var resp = new HttpResponseData
            {
                StatusCode = d.StatusCode,
                Body = d.Body
            };
            resp.Headers["Content-Type"] = d.ContentType;
            if (d.ResponseHeaders != null)
                foreach (var kv in d.ResponseHeaders)
                    resp.Headers[kv.Key] = kv.Value;
            return resp;
        }

        private static HttpResponseData ProxySync(HttpRequestData req, RouteDecision d)
        {
            try
            {
                using var client = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseCookies = false
                });

                var method = new HttpMethod(d.OverrideMethod ?? req.Method ?? "GET");
                var url = d.OverrideUrl ?? req.Url ?? "";
                using var requestMessage = new HttpRequestMessage(method, url);

                var headers = d.OverrideHeaders ?? (IDictionary<string, string>)CopyHeaders(req.Headers);
                foreach (var kv in headers)
                    try { requestMessage.Headers.TryAddWithoutValidation(kv.Key, kv.Value); } catch { /* skip */ }

                var postData = d.OverridePostData ?? req.PostData;
                if (!string.IsNullOrEmpty(postData) && method != HttpMethod.Get)
                    requestMessage.Content = new StringContent(postData);

                using var response = client.SendAsync(requestMessage).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var resp = new HttpResponseData { StatusCode = (int)response.StatusCode, Body = body };
                foreach (var h in response.Headers)
                    resp.Headers[h.Key] = string.Join(", ", h.Value);
                foreach (var h in response.Content.Headers)
                    resp.Headers[h.Key] = string.Join(", ", h.Value);
                return resp;
            }
            catch
            {
                return new HttpResponseData { StatusCode = 0, Body = "" };
            }
        }

        private static Dictionary<string, string> CopyHeaders(IDictionary<string, string>? src)
        {
            if (src == null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return new Dictionary<string, string>(src, StringComparer.OrdinalIgnoreCase);
        }
    }
}
