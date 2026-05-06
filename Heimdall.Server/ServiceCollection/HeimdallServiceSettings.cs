
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
	/// <summary>
	/// Settings for Heimdall service behavior.
	/// </summary>
	public sealed class HeimdallServiceSettings
	{
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
