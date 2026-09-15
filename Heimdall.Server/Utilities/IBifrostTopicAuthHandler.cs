namespace Heimdall.Server
{
	/// <summary>
	/// Authorizes a family of Bifrost topics when a subscribe token is requested.
	/// </summary>
	/// <remarks>
	/// Register handlers with <see cref="HeimdallServiceCollection.AddBifrostTopicAuthHandler{THandler}(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>.
	/// Handlers are resolved from the request scope, so constructor-injected scoped services such as a database context
	/// are available during authorization.
	/// </remarks>
	public interface IBifrostTopicAuthHandler
	{
		/// <summary>
		/// Gets whether this handler owns the supplied topic.
		/// </summary>
		/// <param name="topic">The trimmed topic requested by the client.</param>
		/// <returns><see langword="true"/> when this handler should authorize the topic.</returns>
		/// <remarks>
		/// Keep this check synchronous and inexpensive. Do not perform database or other external calls here; perform
		/// those operations in <see cref="AuthorizeAsync(BifrostTopicAuthorizationContext)"/>.
		/// </remarks>
		bool CanHandle(string topic);

		/// <summary>
		/// Authorizes a topic owned by this handler.
		/// </summary>
		/// <param name="context">The authenticated request and topic being authorized.</param>
		/// <returns><see langword="true"/> to issue a subscribe token; otherwise, access is denied.</returns>
		ValueTask<bool> AuthorizeAsync(BifrostTopicAuthorizationContext context);
	}
}
