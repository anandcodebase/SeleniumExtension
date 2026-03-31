using System.Collections.Generic;

namespace SimpleSeleniumSupport.Network
{
    /// <summary>
    /// Represents an intercepted HTTP request inside a <see cref="Route"/> handler.
    /// Read-only snapshot of the intercepted request data.
    /// </summary>
    public sealed class RouteRequest
    {
        /// <summary>The full request URL.</summary>
        public string Url { get; internal set; } = string.Empty;

        /// <summary>HTTP method (GET, POST, …).</summary>
        public string Method { get; internal set; } = string.Empty;

        /// <summary>Request headers.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; internal set; }
            = new Dictionary<string, string>();

        /// <summary>POST body, or <c>null</c> for requests without a body.</summary>
        public string? PostData { get; internal set; }

        /// <summary>Resource type hint as reported by the browser (e.g. <c>XHR</c>, <c>Document</c>, <c>Image</c>).</summary>
        public string? ResourceType { get; internal set; }
    }
}
