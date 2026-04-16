namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants for working with Bootstrap z-index utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to control the stacking order of elements.
        /// These utilities allow elements to be layered above or below others using
        /// predefined z-index values provided by Bootstrap.
        /// 
        /// This is commonly used when working with overlays, modals, tooltips,
        /// and other layered UI components.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class ZIndex
        {
            public const string N1 = "z-n1";
            public const string Z0 = "z-0";
            public const string Z1 = "z-1";
            public const string Z2 = "z-2";
            public const string Z3 = "z-3";
        }
    }
}