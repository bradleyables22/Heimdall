
namespace Heimdall.Server
{
	/// <summary>
	/// This attribute makes the method invokable via content dispatcher.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class ContentInvocationAttribute : Attribute
	{
		public ContentInvocationAttribute() { }

		public ContentInvocationAttribute(string invocation) => Invocation = invocation;

		/// <summary>
		/// Optional explicit invocation name. If null/empty, the dispatcher will default to the className.methodName.
		/// </summary>
		public string? Invocation { get; init; }
	}
}
