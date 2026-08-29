using Bs = Heimdall.Bootstrap.Bootstrap;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
{
		[ContentInvocation(Action_Load)]
		[RequestTimeout(3000)]
		public static IHtmlContent Load()
			=> Status("e2e-load-result", "Load completed");

		[ContentInvocation(Action_LocalTimeLoad)]
		[RequestTimeout(3000)]
		public static IHtmlContent LocalTimeLoad()
			=> LocalTimeValue(
				"e2e-local-time-load-result",
				"yyyy-MM-dd HH:mm:ss.fff zzz");

		[ContentInvocation(Action_LocalTime)]
		[RequestTimeout(3000)]
		public static IHtmlContent LocalTimeAction()
			=> RenderLocalTimeMatrix("e2e-local-time-action");

		[ContentInvocation(Action_LocalTimeOob)]
		[RequestTimeout(3000)]
		public static IHtmlContent LocalTimeOob()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(LocalTimeValue(
					"e2e-local-time-oob-main-result",
					"MMMM d HH:mm"));
				fragment.Heimdall().Invocation(
					targetSelector: "#e2e-local-time-oob-side-target",
					swap: HeimdallHtml.Swap.Inner,
					payload: LocalTimeValue(
						"e2e-local-time-oob-side-result",
						"MMMM d HH:mm"),
					wrapInTemplate: true);
			});

		[ContentInvocation(Action_LocalTimeSse)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> LocalTimeSse([FromServices] Bifrost bifrost)
		{
			await bifrost.PublishAsync(
				LocalTimeSseTopic,
				LocalTimeValue(
					"e2e-local-time-sse-result",
					"yyyy-MM-dd HH:mm:ss.fff zzz"),
				TimeSpan.FromSeconds(10));

			return HtmlString.Empty;
		}

		private static IHtmlContent RenderLoadSection()
			=> Section("e2e-load-section", "Load", body =>
			{
				body.Div(trigger =>
				{
					trigger.Id("e2e-load-trigger")
					.Text("Load trigger ready");
					trigger.Heimdall()
						.Load(Action_Load)
						.PayloadEmptyObject()
						.Target("#e2e-load-target")
						.SwapInner();
				})
				.Div(target => target.Id("e2e-load-target").Text("Load target original"));
			});

		private static IHtmlContent RenderLocalTimeSection()
			=> Section("e2e-local-time-section", "Local Time", body =>
			{
				body.Add(RenderLocalTimeMatrix("e2e-local-time-initial"))
					.Div(trigger =>
					{
						trigger.Id("e2e-local-time-load-trigger")
							.Text("Local time load trigger ready");
						trigger.Heimdall()
							.Load(Action_LocalTimeLoad)
							.PayloadEmptyObject()
							.Target("#e2e-local-time-load-target")
							.SwapInner();
					})
					.Div(target => target.Id("e2e-local-time-load-target").Text("Local time load pending"))
					.Div(target => target.Id("e2e-local-time-action-target").Text("Local time action pending"))
					.Add(ActionButton(
						id: "e2e-local-time-action-button",
						label: "Render local time matrix",
						action: Action_LocalTime,
						targetSelector: "#e2e-local-time-action-target"))
					.Div(target => target.Id("e2e-local-time-oob-main-target").Text("Local time OOB main pending"))
					.Div(target => target
						.Id("e2e-local-time-oob-side-target")
						.Attr("lang", "fr-FR")
						.Text("Local time OOB side pending"))
					.Add(ActionButton(
						id: "e2e-local-time-oob-button",
						label: "Render local time OOB",
						action: Action_LocalTimeOob,
						targetSelector: "#e2e-local-time-oob-main-target"))
					.Div(host => host
						.Id("e2e-local-time-sse-host")
						.Add(
							HeimdallHtml.SseTopic(LocalTimeSseTopic),
							HeimdallHtml.SseTarget("#e2e-local-time-sse-target"),
							HeimdallHtml.SseSwapMode(HeimdallHtml.Swap.Inner)))
					.Div(target => target.Id("e2e-local-time-sse-target").Text("Local time SSE pending"))
					.Add(ActionButton(
						id: "e2e-local-time-sse-button",
						label: "Publish local time SSE",
						action: Action_LocalTimeSse,
						targetSelector: "#e2e-local-time-sse-target",
						swap: HeimdallHtml.Swap.None));
			});

		private static IHtmlContent RenderDynamicBootSection()
			=> Section("e2e-dynamic-section", "Dynamic Boot", body =>
			{
				body.Div(target => target.Id("e2e-dynamic-target").Text("Dynamic target original"))
				.Add(ActionButton(
					id: "e2e-dynamic-button",
					label: "Render dynamic load",
					action: Action_DynamicBoot,
					targetSelector: "#e2e-dynamic-target"));
			});
	}
}
