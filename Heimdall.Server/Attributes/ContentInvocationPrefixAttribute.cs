namespace Heimdall.Server
{
    /// <summary>
    /// Applies a shared id prefix to content invocations declared on the attributed type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ContentInvocationPrefixAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentInvocationPrefixAttribute"/> class.
        /// </summary>
        /// <param name="prefix">The content invocation id prefix, such as <c>orders</c> or <c>admin.users</c>.</param>
        public ContentInvocationPrefixAttribute(string prefix) => Prefix = prefix;

        /// <summary>
        /// The content invocation id prefix applied to methods on this type.
        /// </summary>
        public string Prefix { get; }
    }
}
