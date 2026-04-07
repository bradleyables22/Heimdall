
namespace Heimdall.Server
{
	/// <summary>
	/// This attribute makes the method invokable via content dispatcher.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class ContentInvocationAttribute : Attribute
	{
        /// <summary>
        /// Empty Constructor. The invocation name can be set via the Invocation property.
        /// </summary>
        public ContentInvocationAttribute() { }

		/// <summary>
		/// Initializes a new instance of the ContentInvocationAttribute class with the specified invocation string.
		/// </summary>
		/// <param name="invocation">The invocation string that identifies the content invocation. Cannot be null.</param>
		public ContentInvocationAttribute(string invocation) => Invocation = invocation;

		/// <summary>
		/// Optional explicit invocation name. If null/empty, the dispatcher will default to the className.methodName.
		/// </summary>
		public string? Invocation { get; init; }
	}
}
