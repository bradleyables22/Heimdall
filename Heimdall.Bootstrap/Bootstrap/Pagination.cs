namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants for working with Bootstrap pagination CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to construct and style pagination components.
        /// These constants represent the standard Bootstrap classes used for pagination containers,
        /// items, links, and common states such as active and disabled.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Pagination
        {
            public const string Base = "pagination";
            public const string Sm = "pagination pagination-sm";
            public const string Lg = "pagination pagination-lg";
            public const string Item = "page-item";
            public const string Link = "page-link";

            public const string Active = "active";
            public const string Disabled = "disabled";
        }
    }
}