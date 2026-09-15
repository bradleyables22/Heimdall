using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
	/// <summary>
	/// Request context supplied to a Bifrost topic authorization handler.
	/// </summary>
	/// <param name="HttpContext">The token-minting HTTP request.</param>
	/// <param name="Topic">The trimmed topic being requested.</param>
	public sealed record BifrostTopicAuthorizationContext(
		HttpContext HttpContext,
		string Topic)
	{
		/// <summary>
		/// Gets the principal authenticated for the token request.
		/// </summary>
		public ClaimsPrincipal User => HttpContext.User;

		/// <summary>
		/// Gets a cancellation token that is cancelled when the token request is aborted.
		/// </summary>
		public CancellationToken RequestAborted => HttpContext.RequestAborted;
	}
}
