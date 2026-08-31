using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
	{
		[ContentInvocation(Action_ClientInfo)]
		[RequestTimeout(3000)]
		public static IHtmlContent ClientInfo(HeimdallClientInfo client)
			=> Status(
				"e2e-client-info-result",
				string.Join("|",
					client.IsAvailable,
					client.TimeZone,
					client.Locale,
					client.ScreenWidth,
					client.ScreenHeight,
					client.DeviceCategory,
					client.ColorScheme,
					client.Online));

		private static IHtmlContent RenderClientInfoSection()
			=> Section("e2e-client-info-section", "Client Browser Information", body =>
			{
				body.Div(target => target
					.Id("e2e-client-info-target")
					.Text("Client information target original"))
					.Add(ActionButton(
						id: "e2e-client-info-button",
						label: "Send client information",
						action: Action_ClientInfo,
						targetSelector: "#e2e-client-info-target"));
			});
	}
}
