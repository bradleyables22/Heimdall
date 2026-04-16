namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants for working with Bootstrap toast component CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to construct and style toast notifications,
        /// including the container, root element, header, and body sections.
        /// 
        /// These utilities are commonly used for transient UI messaging such as alerts,
        /// confirmations, and status updates.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Toast
        {
            public const string Container = "toast-container";
            public const string ToastBase = "toast";
            public const string Header = "toast-header";
            public const string Body = "toast-body";
        }
    }
}