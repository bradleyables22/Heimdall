namespace Heimdall.Server
{
    /// <summary>
    /// Marks a content action parameter as the request payload.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class ContentPayloadAttribute : Attribute
    {
    }
}
