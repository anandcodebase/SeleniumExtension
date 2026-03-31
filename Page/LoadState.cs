namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Playwright-compatible page load state used by <see cref="SeleniumPage.WaitForLoadState"/>.
    /// </summary>
    public enum LoadState
    {
        /// <summary>
        /// Wait until the HTML document has been loaded and parsed
        /// (<c>document.readyState == "interactive"</c>).
        /// </summary>
        DomContentLoaded,

        /// <summary>
        /// Wait until all sub-resources (images, stylesheets, scripts) have been loaded
        /// (<c>document.readyState == "complete"</c>).
        /// </summary>
        Load,

        /// <summary>
        /// Wait until there are no pending network requests for at least 500 ms.
        /// Implemented as <c>document.readyState == "complete"</c> + 500 ms quiet period.
        /// </summary>
        NetworkIdle
    }
}
