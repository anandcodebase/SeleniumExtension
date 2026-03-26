namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Controls how artifact paths (screenshots, screencasts, HAR files, diagnostics)
    /// are stored in reports and turned into clickable browser links.
    /// <para>
    /// Configure once at test-suite startup via
    /// <see cref="SimpleSeleniumSupportDefaults.ArtifactLinkMode"/>.
    /// </para>
    /// </summary>
    public enum ArtifactLinkMode
    {
        /// <summary>
        /// Default. Artifact paths are resolved to absolute Windows paths at export time.
        /// The HTML report builds <c>file:///</c> links from these absolute paths.
        /// Works reliably on the same machine the tests ran on; links will not resolve
        /// on other machines or in browsers that block local-file access.
        /// <para>No additional configuration required.</para>
        /// </summary>
        Local,

        /// <summary>
        /// Artifact paths are stored relative to
        /// <see cref="SimpleSeleniumSupportDefaults.ReportRootDirectory"/>.
        /// The HTML report prefixes them with
        /// <see cref="SimpleSeleniumSupportDefaults.ReportBaseUrl"/> to produce
        /// fully-qualified web URLs.
        /// <para>
        /// Suitable for IIS, nginx, or any web server with Directory Browsing enabled
        /// that serves the artifact root folder.  All artifacts must be saved inside
        /// <see cref="SimpleSeleniumSupportDefaults.ReportRootDirectory"/>; files
        /// outside that folder fall back to an absolute path with a warning logged.
        /// </para>
        /// <para>
        /// Requires both <see cref="SimpleSeleniumSupportDefaults.ReportRootDirectory"/>
        /// and <see cref="SimpleSeleniumSupportDefaults.ReportBaseUrl"/> to be set.
        /// An <see cref="System.InvalidOperationException"/> is thrown at export time
        /// if either is missing or invalid.
        /// </para>
        /// </summary>
        WebUrl
    }
}
