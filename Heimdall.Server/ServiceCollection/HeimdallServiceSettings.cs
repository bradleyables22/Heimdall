
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
	/// <summary>
	/// Settings for Heimdall service behavior.
	/// </summary>
	public sealed class HeimdallServiceSettings
	{
		/// <summary>
		/// Whether Heimdall validates antiforgery tokens for content actions and Bifrost subscribe-token requests.
		/// Defaults to <see langword="true"/>.
		/// </summary>
		/// <remarks>
		/// Disabling this removes CSRF protection from every Heimdall action. Prefer applying
		/// <c>[RequireAntiforgeryToken(false)]</c> to narrowly scoped actions when possible.
		/// </remarks>
		public bool EnableAntiforgery { get; set; } = true;

		/// <summary>
		/// Whether to enable detailed error messages in responses. Defaults to false.
		/// </summary>
		public bool EnableDetailedErrors { get; set; } = false;

		/// <summary>
		/// Optional authorization policy name used when minting Bifrost subscribe tokens.
		/// </summary>
		public string? BifrostTopicPolicy { get; set; }

		/// <summary>
		/// Optional callback used when minting Bifrost subscribe tokens. Return <see langword="true"/> to allow the topic.
		/// </summary>
		public Func<HttpContext, string, ValueTask<bool>>? AuthorizeBifrostTopic { get; set; }

		/// <summary>
		/// Interval used to send idle SSE heartbeat comments for Bifrost streams. Defaults to 15 seconds.
		/// </summary>
		public TimeSpan BifrostHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
	}
}
