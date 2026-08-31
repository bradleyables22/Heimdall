using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
	{
		[ContentInvocation(Action_DocumentVisible)]
		[RequestTimeout(3000)]
		public static IHtmlContent DocumentVisible()
			=> Status("e2e-document-visible-result-value", "Document visible completed");

		[ContentInvocation(Action_Online)]
		[RequestTimeout(3000)]
		public static IHtmlContent Online()
			=> Status("e2e-online-result-value", "Online completed");

		private static IHtmlContent RenderPageLifecycleSection()
			=> Section("e2e-page-lifecycle-section", "Page Lifecycle", body =>
			{
				body.Div(trigger =>
				{
					trigger.Id("e2e-document-visible-trigger")
						.Text("Refresh when this document becomes visible");
					trigger.Heimdall()
						.DocumentVisible(Action_DocumentVisible)
						.PayloadEmptyObject()
						.Target("#e2e-document-visible-target")
						.SwapInner();
				})
				.Div(target => target
					.Id("e2e-document-visible-target")
					.Text("Document visible target original"))
				.Div(trigger =>
				{
					trigger.Id("e2e-online-trigger")
						.Text("Refresh when the browser comes online");
					trigger.Heimdall()
						.Online(Action_Online)
						.PayloadEmptyObject()
						.Target("#e2e-online-target")
						.SwapInner();
				})
				.Div(target => target
					.Id("e2e-online-target")
					.Text("Online target original"))
				.Div(target => target
					.Id("e2e-offline-result")
					.Text("Offline events: 0"));
			});
	}
}
