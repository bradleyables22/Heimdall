namespace Heimdall.Server
{
    /// <summary>
    /// Defines the base directory used to resolve relative static site output paths.
    /// </summary>
    public enum HeimdallStaticSiteOutputRoot
    {
        /// <summary>
        /// Resolve relative output paths from the application content root.
        /// </summary>
        ContentRoot,

        /// <summary>
        /// Resolve relative output paths from the ASP.NET Core web root.
        /// </summary>
        WebRoot
    }
}
