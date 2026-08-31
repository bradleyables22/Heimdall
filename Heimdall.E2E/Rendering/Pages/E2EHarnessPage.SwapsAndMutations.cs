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
		[ContentInvocation(Action_DynamicBoot)]
		[RequestTimeout(3000)]
		public static IHtmlContent DynamicBoot()
			=> FluentHtml.Div(root =>
			{
				root.Id("e2e-dynamic-loaded-root")
				.Div(trigger =>
				{
					trigger.Id("e2e-dynamic-load-trigger")
					.Text("Dynamic load trigger ready");
					trigger.Heimdall()
						.Load(Action_DynamicLoaded)
						.PayloadEmptyObject()
						.Target("#e2e-dynamic-load-target")
						.SwapInner();
				})
				.Div(target => target.Id("e2e-dynamic-load-target").Text("Dynamic load pending"));
			});

		[ContentInvocation(Action_DynamicLoaded)]
		[RequestTimeout(3000)]
		public static IHtmlContent DynamicLoaded()
			=> Status("e2e-dynamic-load-result", "Dynamic load completed");

		[ContentInvocation(Action_Swap)]
		[RequestTimeout(3000)]
		public static IHtmlContent Swap()
			=> Status("e2e-swap-result", "Swap completed");

		[ContentInvocation(Action_SwapOuter)]
		[RequestTimeout(3000)]
		public static IHtmlContent SwapOuter()
			=> FluentHtml.Div(div =>
			{
				div.Id("e2e-outer-target")
				.Add(Status("e2e-outer-result", "Outer swapped"));
			});

		[ContentInvocation(Action_SwapBeforeEnd)]
		[RequestTimeout(3000)]
		public static IHtmlContent SwapBeforeEnd()
			=> Status("e2e-beforeend-result", "Appended");

		[ContentInvocation(Action_SwapAfterBegin)]
		[RequestTimeout(3000)]
		public static IHtmlContent SwapAfterBegin()
			=> Status("e2e-afterbegin-result", "Prepended");

		[ContentInvocation(Action_SwapNone)]
		[RequestTimeout(3000)]
		public static IHtmlContent SwapNone()
			=> Status("e2e-none-result", "Should not swap");

		[ContentInvocation(Action_Oob)]
		[RequestTimeout(3000)]
		public static IHtmlContent Oob()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-oob-main-result", "OOB main completed"));
				fragment.Heimdall().Invocation(
					targetSelector: "#e2e-oob-side-target",
					swap: HeimdallHtml.Swap.Inner,
					payload: Status("e2e-oob-side-result", "OOB side completed"),
					wrapInTemplate: false);
			});

		[ContentInvocation(Action_Abort)]
		[RequestTimeout(3000)]
		public static IHtmlContent Abort()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-abort-main-result", "Abort main should not swap"));
				fragment.Heimdall().Invocation(
					targetSelector: "#e2e-abort-side",
					swap: HeimdallHtml.Swap.Inner,
					payload: Status("e2e-abort-side-result", "Abort side completed"),
					wrapInTemplate: false);
				fragment.Heimdall().Abort("e2e-abort");
			});

		[ContentInvocation(Action_Mutation)]
		[RequestTimeout(3000)]
		public static IHtmlContent Mutation()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall()
					.Invocation(
						targetSelector: "#e2e-mutation-order-host",
						swap: HeimdallHtml.Swap.Inner,
						payload: FluentHtml.Div(created => created
							.Id("e2e-mutation-order-created")
							.Text("Created before mutation")),
						wrapInTemplate: true)
					.Mutate("#e2e-mutation-order-created", mutation => mutation
						.Attr("data-command-order", "invocation-then-mutation")
						.AddClass("ordered"))
					.Mutate("#e2e-mutation-panel", mutation => mutation
						.Attr("data-server", "action")
						.RemoveAttr("data-remove")
						.RemoveClass("pending")
						.AddClass("ready")
						.State(new CounterState { Count = 41 }))
					.Mutate(
						"#e2e-mutation-panel",
						mutation => mutation.Attr("data-child", "action"),
						HeimdallHtml.MutationScope.Matching(".e2e-mutation-child"));

				fragment.Add(Status("e2e-mutation-main-result", "Mutation main swapped"));
			});

		[ContentInvocation(Action_MutationState)]
		[RequestTimeout(3000)]
		public static IHtmlContent MutationState([ContentPayload] CounterState state)
			=> Status("e2e-mutation-state-value", $"Mutation state count: {state?.Count ?? -1}");

		[ContentInvocation(Action_MutationSse)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> MutationSse([FromServices] Bifrost bifrost)
		{
			var message = FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().Mutate("#e2e-mutation-panel", mutation => mutation
					.Attr("data-server", "sse")
					.AddClass("sse-ready"));
				fragment.Add(Status("e2e-mutation-sse-main-result", "Mutation SSE main swapped"));
			});

			await bifrost.PublishAsync(SseTopic, message, TimeSpan.FromSeconds(10));
			return HtmlString.Empty;
		}

		[ContentInvocation(Action_Redirect)]
		[RequestTimeout(3000)]
		public static IHtmlContent Redirect()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-redirect-result", "Redirect target should not swap"));
				fragment.Heimdall().Redirect("#e2e-redirected");
			});

		private static IHtmlContent RenderSwapSection()
			=> Section("e2e-swap-section", "Swap", body =>
			{
				body.Div(target => target.Id("e2e-swap-target").Text("Swap target original"))
				.Add(ActionButton(
					id: "e2e-swap-button",
					label: "Run swap",
					action: Action_Swap,
					targetSelector: "#e2e-swap-target"));
			});

		private static IHtmlContent RenderSwapModesSection()
			=> Section("e2e-swap-modes-section", "Swap Modes", body =>
			{
				body.Div(target => target.Id("e2e-outer-target").Text("Outer original"))
				.Add(ActionButton(
					id: "e2e-outer-button",
					label: "Outer",
					action: Action_SwapOuter,
					targetSelector: "#e2e-outer-target",
					swap: HeimdallHtml.Swap.Outer))
				.Div(target =>
				{
					target.Id("e2e-beforeend-target")
					.Span(span => span.Id("e2e-beforeend-original").Text("Start"));
				})
				.Add(ActionButton(
					id: "e2e-beforeend-button",
					label: "Before end",
					action: Action_SwapBeforeEnd,
					targetSelector: "#e2e-beforeend-target",
					swap: HeimdallHtml.Swap.BeforeEnd))
				.Div(target =>
				{
					target.Id("e2e-afterbegin-target")
					.Span(span => span.Id("e2e-afterbegin-original").Text("End"));
				})
				.Add(ActionButton(
					id: "e2e-afterbegin-button",
					label: "After begin",
					action: Action_SwapAfterBegin,
					targetSelector: "#e2e-afterbegin-target",
					swap: HeimdallHtml.Swap.AfterBegin))
				.Div(target => target.Id("e2e-none-target").Text("None target original"))
				.Add(ActionButton(
					id: "e2e-none-button",
					label: "None",
					action: Action_SwapNone,
					targetSelector: "#e2e-none-target",
					swap: HeimdallHtml.Swap.None));
			});

		private static IHtmlContent RenderOobSection()
			=> Section("e2e-oob-section", "Out Of Band", body =>
			{
				body.Div(target => target.Id("e2e-oob-main-target").Text("OOB main original"))
				.Div(target => target.Id("e2e-oob-side-target").Text("OOB side original"))
				.Add(ActionButton(
					id: "e2e-oob-button",
					label: "Run OOB",
					action: Action_Oob,
					targetSelector: "#e2e-oob-main-target"));
			});

		private static IHtmlContent RenderAbortSection()
			=> Section("e2e-abort-section", "Abort", body =>
			{
				body.Div(target => target.Id("e2e-abort-target").Text("Abort target original"))
				.Div(target => target.Id("e2e-abort-side").Text("Abort side original"))
				.Add(ActionButton(
					id: "e2e-abort-button",
					label: "Run abort",
					action: Action_Abort,
					targetSelector: "#e2e-abort-target"));
			});

		private static IHtmlContent RenderMutationSection()
			=> Section("e2e-mutation-section", "In-place Mutation", body =>
			{
				body.Div(panel =>
				{
					panel.Id("e2e-mutation-panel")
						.Class("pending", "keep")
						.Data("remove", "yes")
						.Input(Html.InputType.text, input => input
							.Id("e2e-mutation-input")
							.Class("e2e-mutation-child")
							.Value("typed value"))
						.Button(button =>
						{
							button.Id("e2e-mutation-state-button")
								.Type("button")
								.Class("e2e-mutation-child")
								.Text("Read mutated state");
							button.Heimdall()
								.Click(Action_MutationState)
								.PayloadFromClosestState()
								.Target("#e2e-mutation-state-result")
								.SwapInner();
						});
				})
				.Div(target => target.Id("e2e-mutation-main-target").Text("Mutation main original"))
				.Div(target => target.Id("e2e-mutation-order-host").Text("Mutation order original"))
				.Div(target => target.Id("e2e-mutation-state-result").Text("Mutation state original"))
				.Add(ActionButton(
					id: "e2e-mutation-button",
					label: "Run mutation",
					action: Action_Mutation,
					targetSelector: "#e2e-mutation-main-target"))
				.Add(ActionButton(
					id: "e2e-mutation-sse-button",
					label: "Publish mutation SSE",
					action: Action_MutationSse,
					targetSelector: "#e2e-sse-target",
					swap: HeimdallHtml.Swap.None));
			});
	}
}
