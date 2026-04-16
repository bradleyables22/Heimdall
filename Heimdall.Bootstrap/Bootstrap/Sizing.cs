namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants for working with Bootstrap sizing utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to control element dimensions, including width, height,
        /// viewport-based sizing, and maximum sizing constraints.
        /// 
        /// These utilities are commonly used when building responsive layouts or enforcing
        /// consistent sizing behavior across components.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Sizing
        {
            // width
            public const string W25 = "w-25";
            public const string W50 = "w-50";
            public const string W75 = "w-75";
            public const string W100 = "w-100";
            public const string WAuto = "w-auto";

            // height
            public const string H25 = "h-25";
            public const string H50 = "h-50";
            public const string H75 = "h-75";
            public const string H100 = "h-100";
            public const string HAuto = "h-auto";

            // viewport sizing
            public const string Vw100 = "vw-100";
            public const string Vh100 = "vh-100";
            public const string MinVw100 = "min-vw-100";
            public const string MinVh100 = "min-vh-100";

            // max sizing
            public const string Mw100 = "mw-100";
            public const string Mh100 = "mh-100";
        }
    }
}