
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
	}
}
