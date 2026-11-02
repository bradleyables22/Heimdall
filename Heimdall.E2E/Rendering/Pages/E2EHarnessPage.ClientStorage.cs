using Bs = Heimdall.Bootstrap.Bootstrap;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
	{
		[ContentInvocation(Action_StoragePayload)]
		[RequestTimeout(3000)]
		public static IHtmlContent StoragePayload([ContentPayload] MessagePayload payload)
			=> Status("e2e-storage-payload-result", $"Storage payload: {Normalize(payload?.Message)}");

		[ContentInvocation(Action_StorageDirective)]
		[RequestTimeout(3000)]
		public static IHtmlContent StorageDirective()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().SetStorage(HeimdallHtml.StorageArea.Local, "e2e.theme", "dark")
					.SetStorage(HeimdallHtml.StorageArea.Session, "e2e.checkout", "open")
					.Add(Status("e2e-storage-directive-result", "Storage directive applied"));
			});

		[ContentInvocation(Action_StorageAbort)]
		[RequestTimeout(3000)]
		public static IHtmlContent StorageAbort()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().SetStorage(HeimdallHtml.StorageArea.Local, "e2e.abort", "kept")
					.Add(Status("e2e-storage-abort-result", "Storage abort should not swap"))
					.Abort("e2e-storage-abort");
			});

		[ContentInvocation(Action_StorageSse)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> StorageSse([FromServices] Bifrost bifrost)
		{
			var message = FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().SetStorage(HeimdallHtml.StorageArea.Local, "e2e.sse", "delivered")
					.Add(Status("e2e-storage-sse-result", "Storage SSE delivered"));
			});

			await bifrost.PublishAsync(SseTopic, message, TimeSpan.FromSeconds(10));
			return HtmlString.Empty;
		}

		private static IHtmlContent RenderClientStorageSection()
			=> Section("e2e-client-storage-section", "Client Storage", body =>
			{
				body.Div(target => target
					.Id("e2e-storage-payload-target")
					.Text("Storage payload target original"))
				.Button(button =>
				{
					button.Id("e2e-storage-payload-button")
						.Type("button")
						.Class(Bs.Btn.OutlinePrimary, Bs.Spacing.Mt(2))
						.Text("Send local storage payload");
					button.Heimdall()
						.Click(Action_StoragePayload)
						.PayloadFromStorage(HeimdallHtml.StorageArea.Local, "e2e.payload")
						.Target("#e2e-storage-payload-target")
						.SwapInner();
				})
				.Div(target => target
					.Id("e2e-storage-directive-target")
					.Text("Storage directive target original"))
				.Add(ActionButton(
					id: "e2e-storage-directive-button",
					label: "Set storage values",
					action: Action_StorageDirective,
					targetSelector: "#e2e-storage-directive-target"))
				.Div(target => target
					.Id("e2e-storage-abort-target")
					.Text("Storage abort target original"))
				.Add(ActionButton(
					id: "e2e-storage-abort-button",
					label: "Set storage and abort",
					action: Action_StorageAbort,
					targetSelector: "#e2e-storage-abort-target"))
				.Add(ActionButton(
					id: "e2e-storage-sse-button",
					label: "Publish storage SSE",
					action: Action_StorageSse,
					targetSelector: "#e2e-sse-target",
					swap: HeimdallHtml.Swap.None));
			});
	}
}
