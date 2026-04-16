namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap navigation-related CSS class names for building nav components.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap nav class names, including base navigation,
        /// items, links, and layout variants such as tabs and pills. It is intended to simplify the process of applying
        /// consistent navigation styling across UI components.</remarks>
        public static class Nav
        {
            public const string Base = "nav";
            public const string Item = "nav-item";
            public const string Link = "nav-link";
            public const string Tabs = "nav nav-tabs";
            public const string Pills = "nav nav-pills";
            public const string Fill = "nav-fill";
            public const string Justified = "nav-justified";

        }
    }
}