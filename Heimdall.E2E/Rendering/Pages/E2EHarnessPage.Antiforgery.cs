using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
	{
		[RequireAntiforgeryToken(false)]
		[ContentInvocation(Action_AntiforgeryDisabled)]
		[RequestTimeout(3000)]
		public static IHtmlContent AntiforgeryDisabled()
			=> Status("e2e-antiforgery-success", "Antiforgery-disabled action completed");

		private static IHtmlContent RenderAntiforgerySection()
			=> Section("e2e-antiforgery-section", "Antiforgery", body =>
			{
				body.Div(target => target
					.Id("e2e-antiforgery-target")
					.Text("Antiforgery target original"))
					.Add(ActionButton(
						id: "e2e-antiforgery-button",
						label: "Run without antiforgery",
						action: Action_AntiforgeryDisabled,
						targetSelector: "#e2e-antiforgery-target"));
			});
	}
}
