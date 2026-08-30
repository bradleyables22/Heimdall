using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
	{
		[ContentInvocation(Action_HistoryPush)]
		[RequestTimeout(3000)]
		public static IHtmlContent HistoryPush()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-history-push-result", "History pushed"));
				fragment.Heimdall().HistoryPush("history/pushed");
			});

		[ContentInvocation(Action_HistoryReplace)]
		[RequestTimeout(3000)]
		public static IHtmlContent HistoryReplace()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-history-replace-result", "History replaced"));
				fragment.Heimdall().HistoryReplace("/history/replaced");
			});

		private static IHtmlContent RenderHistorySection()
			=> Section("e2e-history-section", "History", body =>
			{
				body.Div(target => target.Id("e2e-history-target").Text("History target original"))
					.Add(ActionButton(
						id: "e2e-history-push-button",
						label: "Push history",
						action: Action_HistoryPush,
						targetSelector: "#e2e-history-target"))
					.Add(ActionButton(
						id: "e2e-history-replace-button",
						label: "Replace history",
						action: Action_HistoryReplace,
						targetSelector: "#e2e-history-target"));
			});
	}
}
