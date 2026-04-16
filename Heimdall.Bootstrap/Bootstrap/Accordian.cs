namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides CSS class name constants for styling accordion components in user interfaces.
        /// </summary>
        /// <remarks>Use these constants to apply consistent and maintainable class names when building or
        /// customizing accordion elements, such as items, headers, buttons, and bodies. This class is intended to reduce
        /// errors from hard-coded strings and to facilitate updates if class names change.</remarks>
        public static class Accordion
        {
            public const string Base = "accordion";
            public const string Item = "accordion-item";
            public const string Header = "accordion-header";
            public const string Button = "accordion-button";
            public const string Collapse = "accordion-collapse";
            public const string Body = "accordion-body";
            public const string Flush = "accordion-flush";

            public const string ButtonCollapsed = "collapsed";
        }
    }
}
