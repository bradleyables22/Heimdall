using Bs = Heimdall.Bootstrap.Bootstrap;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.E2E.Rendering.Pages
{
	public static class E2EHarnessPage
	{
		public const string Action_Load = "e2e.load";
		public const string Action_DynamicBoot = "e2e.dynamic.boot";
		public const string Action_DynamicLoaded = "e2e.dynamic.loaded";
		public const string Action_Swap = "e2e.swap";
		public const string Action_SwapOuter = "e2e.swap.outer";
		public const string Action_SwapBeforeEnd = "e2e.swap.before-end";
		public const string Action_SwapAfterBegin = "e2e.swap.after-begin";
		public const string Action_SwapNone = "e2e.swap.none";
		public const string Action_Oob = "e2e.oob";
		public const string Action_Abort = "e2e.abort";
		public const string Action_Redirect = "e2e.redirect";
		public const string Action_Js = "e2e.js";
		public const string Action_Form = "e2e.form";
		public const string Action_Sse = "e2e.sse";
		public const string Action_SseOob = "e2e.sse.oob";
		public const string Action_SseJs = "e2e.sse.js";
		public const string Action_Programmatic = "e2e.programmatic";
		public const string Action_PayloadRef = "e2e.payload-ref";
		public const string Action_SelfPayload = "e2e.self-payload";
		public const string Action_StateIncrement = "e2e.state.increment";
		public const string Action_StateDecrement = "e2e.state.decrement";
		public const string Action_StateReset = "e2e.state.reset";
		public const string Action_Input = "e2e.input";
		public const string Action_Change = "e2e.change";
		public const string Action_Keydown = "e2e.keydown";
		public const string Action_Blur = "e2e.blur";
		public const string Action_Hover = "e2e.hover";
		public const string Action_Marker = "e2e.marker";
		public const string Action_SlowDisable = "e2e.slow-disable";
		public const string Action_Error = "e2e.error";
		public const string Action_AuthRequired = "e2e.auth.required";

		private const string SseTopic = "e2e-harness";
		public const string SseTopic_AuthRequired = "e2e-auth-required";

		private const string ClientHarnessScript = """
window.HeimdallE2E = {
  calls: [],
  sseCalls: [],
  payload: { message: "global payload initial" },
  setPayload: function(message) {
    this.payload = { message: message };
  },
  record: function(phase, value) {
    var target = document.querySelector("#e2e-js-target");
    var targetText = target ? target.textContent.trim() : "";
    var entry = { phase: phase, value: value, targetText: targetText };
    this.calls.push(entry);

    var log = document.querySelector("#e2e-js-log");
    if (log) {
      log.textContent = this.calls.map(function(call) {
        return call.phase + ":" + call.value + ":" + call.targetText;
      }).join("|");
    }

    return { discarded: true };
  },
  recordSse: function(value) {
    var target = document.querySelector("#e2e-sse-target");
    var targetText = target ? target.textContent.trim() : "";
    var entry = { value: value, targetText: targetText };
    this.sseCalls.push(entry);

    var log = document.querySelector("#e2e-sse-js-log");
    if (log) {
      log.textContent = value + ":" + targetText + ":" + this.sseCalls.length;
    }

    return "discarded-sse-result";
  }
};
""";

		public sealed class CounterState
		{
			public int Count { get; set; }
		}

		public sealed class FieldPayload
		{
			public string Value { get; set; } = string.Empty;
			public string Choice { get; set; } = string.Empty;
			public string KeyText { get; set; } = string.Empty;
			public string BlurText { get; set; } = string.Empty;
		}

		public sealed class HarnessFormRequest
		{
			public string Name { get; set; } = string.Empty;
		}

		public sealed class MessagePayload
		{
			public string Message { get; set; } = string.Empty;
		}

		public static IHtmlContent Render()
			=> FluentHtml.Div(root =>
			{
				root.Id("e2e-harness")
				.Class(Bs.Layout.Container, Bs.Spacing.Py(4))
				.Script(script => script.Raw(ClientHarnessScript))
				.H1(h => h.Text("Heimdall E2E Harness"))
				.P(p =>
				{
					p.Class(Bs.Text.BodySecondary)
					.Text("Stable browser scenarios for Heimdall server/runtime integration tests.");
				})
				.Add(
					RenderLoadSection(),
					RenderDynamicBootSection(),
					RenderSwapSection(),
					RenderSwapModesSection(),
					RenderOobSection(),
					RenderAbortSection(),
					RenderStateSection(),
					RenderProgrammaticSection(),
					RenderPayloadRefSection(),
					RenderSelfPayloadSection(),
					RenderDelegatedEventsSection(),
					RenderBehaviorSection(),
					RenderJsSection(),
					RenderFormSection(),
					RenderSseSection(),
					RenderAuthSection(),
					RenderErrorSection(),
					RenderRedirectSection());
			});

		[ContentInvocation(Action_Load)]
		[RequestTimeout(3000)]
		public static IHtmlContent Load()
			=> Status("e2e-load-result", "Load completed");

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

		[ContentInvocation(Action_Redirect)]
		[RequestTimeout(3000)]
		public static IHtmlContent Redirect()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-redirect-result", "Redirect target should not swap"));
				fragment.Heimdall().Redirect("#e2e-redirected");
			});

		[ContentInvocation(Action_StateIncrement)]
		[RequestTimeout(3000)]
		public static IHtmlContent StateIncrement([ContentPayload] CounterState state)
			=> RenderStateHost((state?.Count ?? 0) + 1);

		[ContentInvocation(Action_StateDecrement)]
		[RequestTimeout(3000)]
		public static IHtmlContent StateDecrement([ContentPayload] CounterState state)
			=> RenderStateHost((state?.Count ?? 0) - 1);

		[ContentInvocation(Action_StateReset)]
		[RequestTimeout(3000)]
		public static IHtmlContent StateReset([ContentPayload] CounterState state)
			=> RenderStateHost(0);

		[ContentInvocation(Action_Programmatic)]
		[RequestTimeout(3000)]
		public static IHtmlContent Programmatic([ContentPayload] MessagePayload payload)
			=> Status("e2e-programmatic-result", $"Programmatic: {Normalize(payload?.Message)}");

		[ContentInvocation(Action_PayloadRef)]
		[RequestTimeout(3000)]
		public static IHtmlContent PayloadRef([ContentPayload] MessagePayload payload)
			=> Status("e2e-payload-ref-result", $"Payload ref: {Normalize(payload?.Message)}");

		[ContentInvocation(Action_SelfPayload)]
		[RequestTimeout(3000)]
		public static IHtmlContent SelfPayload([ContentPayload] MessagePayload payload)
			=> Status("e2e-self-payload-result", $"Self payload: {Normalize(payload?.Message)}");

		[ContentInvocation(Action_Input)]
		[RequestTimeout(3000)]
		public static IHtmlContent Input([ContentPayload] FieldPayload payload)
			=> Status("e2e-input-result-value", $"Input: {Normalize(payload?.Value)}");

		[ContentInvocation(Action_Change)]
		[RequestTimeout(3000)]
		public static IHtmlContent Change([ContentPayload] FieldPayload payload)
			=> Status("e2e-change-result-value", $"Choice: {Normalize(payload?.Choice)}");

		[ContentInvocation(Action_Keydown)]
		[RequestTimeout(3000)]
		public static IHtmlContent Keydown([ContentPayload] FieldPayload payload)
			=> Status("e2e-key-result-value", $"Key: {Normalize(payload?.KeyText)}");

		[ContentInvocation(Action_Blur)]
		[RequestTimeout(3000)]
		public static IHtmlContent Blur([ContentPayload] FieldPayload payload)
			=> Status("e2e-blur-result-value", $"Blur: {Normalize(payload?.BlurText)}");

		[ContentInvocation(Action_Hover)]
		[RequestTimeout(3000)]
		public static IHtmlContent Hover()
			=> Status("e2e-hover-result-value", "Hover completed");

		[ContentInvocation(Action_Marker)]
		[RequestTimeout(3000)]
		public static IHtmlContent Marker([ContentPayload] MessagePayload payload)
			=> Status("e2e-behavior-result-value", $"Marker: {Normalize(payload?.Message)}");

		[ContentInvocation(Action_SlowDisable)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> SlowDisable()
		{
			await Task.Delay(700);
			return Status("e2e-disable-result-value", "Disable completed");
		}

		[ContentInvocation(Action_Js)]
		[RequestTimeout(3000)]
		public static IHtmlContent Js()
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().JsInvokeVoidBefore("window.HeimdallE2E.record", "before", "ok");
				fragment.Add(Status("e2e-js-result", "JS target swapped"));
				fragment.Heimdall().JsInvokeVoidAfter("window.HeimdallE2E.record", "after", "ok");
			});

		[ContentInvocation(Action_Form)]
		[RequestTimeout(3000)]
		public static IHtmlContent Form([ContentPayload] HarnessFormRequest request)
		{
			var name = Normalize(request?.Name);
			return string.IsNullOrWhiteSpace(name)
				? Status("e2e-form-error", "Name is required.")
				: Status("e2e-form-success", $"Hello, {name}.");
		}

		[ContentInvocation(Action_Sse)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> Sse([FromServices] Bifrost bifrost)
		{
			await bifrost.PublishAsync(
				SseTopic,
				Status("e2e-sse-result", "SSE delivered"),
				TimeSpan.FromSeconds(10));

			return HtmlString.Empty;
		}

		[ContentInvocation(Action_SseOob)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> SseOob([FromServices] Bifrost bifrost)
		{
			var message = FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-sse-oob-main-result", "SSE OOB main"));
				fragment.Heimdall().Invocation(
					targetSelector: "#e2e-sse-oob-target",
					swap: HeimdallHtml.Swap.Inner,
					payload: Status("e2e-sse-oob-side-result", "SSE OOB side"),
					wrapInTemplate: false);
			});

			await bifrost.PublishAsync(SseTopic, message, TimeSpan.FromSeconds(10));

			return HtmlString.Empty;
		}

		[ContentInvocation(Action_SseJs)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> SseJs([FromServices] Bifrost bifrost)
		{
			var message = FluentHtml.Fragment(fragment =>
			{
				fragment.Add(Status("e2e-sse-js-main-result", "SSE JS main"));
				fragment.Heimdall().JsInvokeVoidAfter("window.HeimdallE2E.recordSse", "sse-ok");
			});

			await bifrost.PublishAsync(SseTopic, message, TimeSpan.FromSeconds(10));

			return HtmlString.Empty;
		}

		[ContentInvocation(Action_Error)]
		[RequestTimeout(3000)]
		public static IHtmlContent Error()
			=> throw new InvalidOperationException("E2E expected failure.");

		[Authorize]
		[ContentInvocation(Action_AuthRequired)]
		[RequestTimeout(3000)]
		public static IHtmlContent AuthRequired()
			=> Status("e2e-auth-result", "Authenticated content should not render for anonymous users");

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

		private static IHtmlContent RenderStateSection()
			=> Section("e2e-state-section", "Closest State", body =>
			{
				body.Add(RenderStateHost(0));
			});

		private static IHtmlContent RenderProgrammaticSection()
			=> Section("e2e-programmatic-section", "Programmatic Invoke", body =>
			{
				body.Div(target => target.Id("e2e-programmatic-target").Text("Programmatic target original"));
			});

		private static IHtmlContent RenderPayloadRefSection()
			=> Section("e2e-payload-ref-section", "Payload Ref", body =>
			{
				body.Div(target => target.Id("e2e-payload-ref-target").Text("Payload ref target original"))
				.Button(button =>
				{
					button.Id("e2e-payload-ref-button")
					.Type("button")
					.Class(Bs.Btn.OutlinePrimary, Bs.Spacing.Mt(2))
					.Text("Run payload ref");
					button.Heimdall()
						.Click(Action_PayloadRef)
						.PayloadRef("HeimdallE2E.payload")
						.Target("#e2e-payload-ref-target")
						.SwapInner();
				});
			});

		private static IHtmlContent RenderSelfPayloadSection()
			=> Section("e2e-self-payload-section", "Self Payload", body =>
			{
				body.Div(target => target.Id("e2e-self-payload-target").Text("Self payload target original"))
				.Button(button =>
				{
					button.Id("e2e-self-payload-button")
					.Type("button")
					.Class(Bs.Btn.OutlinePrimary, Bs.Spacing.Mt(2))
					.Data("message", "from self")
					.Text("Run self payload");
					button.Heimdall()
						.Click(Action_SelfPayload)
						.PayloadFromSelf()
						.Target("#e2e-self-payload-target")
						.SwapInner();
				});
			});

		private static IHtmlContent RenderDelegatedEventsSection()
			=> Section("e2e-events-section", "Delegated Events", body =>
			{
				body.Form(form =>
				{
					form.Id("e2e-events-form");

					form.Label(label =>
					{
						label.For("e2e-input")
						.Class(Bs.Form.Label)
						.Text("Input");
					})
					.Input(Html.InputType.text, input =>
					{
						input.Id("e2e-input")
						.Name(nameof(FieldPayload.Value))
						.Class(Bs.Form.Control, Bs.Spacing.Mb(2));
						input.Heimdall()
							.Input(Action_Input)
							.PayloadFromClosestForm()
							.Target("#e2e-input-result")
							.SwapInner()
							.DebounceMs(0);
					})
					.Div(target => target.Id("e2e-input-result").Text("Input target original"))
					.Label(label =>
					{
						label.For("e2e-change")
						.Class(Bs.Form.Label, Bs.Spacing.Mt(3))
						.Text("Change");
					})
					.Select(select =>
					{
						select.Id("e2e-change")
						.Name(nameof(FieldPayload.Choice))
						.Class(Bs.Form.Select, Bs.Spacing.Mb(2));
						select.Heimdall()
							.Change(Action_Change)
							.PayloadFromClosestForm()
							.Target("#e2e-change-result")
							.SwapInner();
						select.Option(option => option.Value("").Text("Choose"));
						select.Option(option => option.Value("alpha").Text("Alpha"));
						select.Option(option => option.Value("beta").Text("Beta"));
					})
					.Div(target => target.Id("e2e-change-result").Text("Change target original"))
					.Label(label =>
					{
						label.For("e2e-key-input")
						.Class(Bs.Form.Label, Bs.Spacing.Mt(3))
						.Text("Keydown");
					})
					.Input(Html.InputType.text, input =>
					{
						input.Id("e2e-key-input")
						.Name(nameof(FieldPayload.KeyText))
						.Class(Bs.Form.Control, Bs.Spacing.Mb(2));
						input.Heimdall()
							.KeyDown(Action_Keydown)
							.Key("Enter")
							.PayloadFromClosestForm()
							.Target("#e2e-key-result")
							.SwapInner();
					})
					.Div(target => target.Id("e2e-key-result").Text("Key target original"))
					.Label(label =>
					{
						label.For("e2e-blur-input")
						.Class(Bs.Form.Label, Bs.Spacing.Mt(3))
						.Text("Blur");
					})
					.Input(Html.InputType.text, input =>
					{
						input.Id("e2e-blur-input")
						.Name(nameof(FieldPayload.BlurText))
						.Class(Bs.Form.Control, Bs.Spacing.Mb(2));
						input.Heimdall()
							.Blur(Action_Blur)
							.PayloadFromClosestForm()
							.Target("#e2e-blur-result")
							.SwapInner();
					})
					.Div(target => target.Id("e2e-blur-result").Text("Blur target original"));
				})
				.Div(hover =>
				{
					hover.Id("e2e-hover-trigger")
					.Class(Bs.Raw("border"), Bs.Spacing.P(3), Bs.Spacing.Mt(3))
					.Text("Hover trigger");
					hover.Heimdall()
						.Hover(Action_Hover)
						.PayloadEmptyObject()
						.Target("#e2e-hover-result")
						.SwapInner()
						.HoverDelayMs(0);
				})
				.Div(target => target.Id("e2e-hover-result").Text("Hover target original"));
			});

		private static IHtmlContent RenderBehaviorSection()
			=> Section("e2e-behavior-section", "Event Behavior", body =>
			{
				body.Div(target => target.Id("e2e-behavior-result").Text("Behavior target original"))
				.Div(scope =>
				{
					scope.Id("e2e-scope-self-trigger")
					.Class(Bs.Raw("border"), Bs.Spacing.P(2), Bs.Spacing.Mt(2))
					.Text("Scope self trigger");
					scope.Heimdall()
						.Click(Action_Marker)
						.Payload(new MessagePayload { Message = "scope self" })
						.Target("#e2e-behavior-result")
						.SwapInner()
						.ScopeSelf();
					scope.Button(button =>
					{
						button.Id("e2e-scope-self-child")
						.Type("button")
						.Class(Bs.Btn.OutlineSecondary, Bs.Spacing.Ms(2))
						.Text("Child");
					});
				})
				.Div(ignoreParent =>
				{
					ignoreParent.Id("e2e-ignore-parent")
					.Class(Bs.Raw("border"), Bs.Spacing.P(2), Bs.Spacing.Mt(2))
					.Text("Ignore parent");
					ignoreParent.Heimdall()
						.Click(Action_Marker)
						.Payload(new MessagePayload { Message = "ignore parent" })
						.Target("#e2e-behavior-result")
						.SwapInner();
					ignoreParent.Span(boundary =>
					{
						boundary.Id("e2e-ignore-boundary")
						.Class(Bs.Spacing.Ms(2));
						boundary.Heimdall().IgnoreAll();
						boundary.Button(button =>
						{
							button.Id("e2e-ignore-child")
							.Type("button")
							.Class(Bs.Btn.OutlineSecondary)
							.Text("Ignored child");
						});
					});
				})
				.A(anchor =>
				{
					anchor.Id("e2e-prevent-link")
					.Href("#e2e-should-not-change")
					.Class(Bs.Raw("d-inline-block"), Bs.Spacing.Mt(2))
					.Text("Prevented link");
					anchor.Heimdall()
						.Click(Action_Marker)
						.Payload(new MessagePayload { Message = "prevented link" })
						.Target("#e2e-behavior-result")
						.SwapInner()
						.PreventDefault(true);
				})
				.Br(_ => { })
				.Button(button =>
				{
					button.Id("e2e-disable-button")
					.Type("button")
					.Class(Bs.Btn.OutlinePrimary, Bs.Spacing.Mt(2))
					.Text("Disable while running");
					button.Heimdall()
						.Click(Action_SlowDisable)
						.PayloadEmptyObject()
						.Target("#e2e-disable-result")
						.SwapInner()
						.Disable(true);
				})
				.Div(target => target.Id("e2e-disable-result").Text("Disable target original"));
			});

		private static IHtmlContent RenderJsSection()
			=> Section("e2e-js-section", "JavaScript Invocation", body =>
			{
				body.Div(target => target.Id("e2e-js-target").Text("JS target original"))
				.Div(log => log.Id("e2e-js-log").Text("JS log empty"))
				.Add(ActionButton(
					id: "e2e-js-button",
					label: "Run JS",
					action: Action_Js,
					targetSelector: "#e2e-js-target"));
			});

		private static IHtmlContent RenderFormSection()
			=> Section("e2e-form-section", "Form", body =>
			{
				body.Form(form =>
				{
					form.Id("e2e-form");
					form.Heimdall()
						.Submit(Action_Form)
						.PayloadFromClosestForm()
						.Target("#e2e-form-result")
						.SwapInner()
						.PreventDefault(true);

					form.Label(label =>
					{
						label.For("e2e-name")
						.Class(Bs.Form.Label)
						.Text("Name");
					})
					.Input(Html.InputType.text, input =>
					{
						input.Id("e2e-name")
						.Name(nameof(HarnessFormRequest.Name))
						.Class(Bs.Form.Control, Bs.Spacing.Mb(2));
					})
					.Button(button =>
					{
						button.Id("e2e-form-submit")
						.Type("submit")
						.Class(Bs.Btn.Primary)
						.Text("Submit harness form");
					});
				})
				.Div(target => target.Id("e2e-form-result").Text("Form target original"));
			});

		private static IHtmlContent RenderSseSection()
			=> Section("e2e-sse-section", "SSE", body =>
			{
				body.Div(host =>
				{
					host.Id("e2e-sse-host")
					.Add(
						HeimdallHtml.SseTopic(SseTopic),
						HeimdallHtml.SseTarget("#e2e-sse-target"),
						HeimdallHtml.SseSwapMode(HeimdallHtml.Swap.Inner));
				})
				.Div(target => target.Id("e2e-sse-target").Text("SSE target original"))
				.Add(ActionButton(
					id: "e2e-sse-button",
					label: "Publish SSE",
					action: Action_Sse,
					targetSelector: "#e2e-sse-target",
					swap: HeimdallHtml.Swap.None))
				.Div(target => target.Id("e2e-sse-oob-target").Text("SSE OOB target original"))
				.Add(ActionButton(
					id: "e2e-sse-oob-button",
					label: "Publish SSE OOB",
					action: Action_SseOob,
					targetSelector: "#e2e-sse-target",
					swap: HeimdallHtml.Swap.None))
				.Div(log => log.Id("e2e-sse-js-log").Text("SSE JS log empty"))
				.Add(ActionButton(
					id: "e2e-sse-js-button",
					label: "Publish SSE JS",
					action: Action_SseJs,
					targetSelector: "#e2e-sse-target",
					swap: HeimdallHtml.Swap.None));
			});

		private static IHtmlContent RenderErrorSection()
			=> Section("e2e-error-section", "Error", body =>
			{
				body.Div(target => target.Id("e2e-error-target").Text("Error target original"));
			});

		private static IHtmlContent RenderAuthSection()
			=> Section("e2e-auth-section", "Auth Redirect", body =>
			{
				body.Div(target => target.Id("e2e-auth-target").Text("Auth target original"))
				.Add(ActionButton(
					id: "e2e-auth-button",
					label: "Run auth redirect",
					action: Action_AuthRequired,
					targetSelector: "#e2e-auth-target"));
			});

		private static IHtmlContent RenderRedirectSection()
			=> Section("e2e-redirect-section", "Redirect", body =>
			{
				body.Div(target => target.Id("e2e-redirect-target").Text("Redirect target original"))
				.Add(ActionButton(
					id: "e2e-redirect-button",
					label: "Run redirect",
					action: Action_Redirect,
					targetSelector: "#e2e-redirect-target"));
			});

		private static IHtmlContent RenderStateHost(int count)
			=> FluentHtml.Div(host =>
			{
				host.Id("e2e-state-host")
				.Class(Bs.Display.InlineBlock, Bs.Raw("border"), Bs.Spacing.P(3))
				.Add(HeimdallHtml.State(new CounterState { Count = count }))
				.Div(label => label.Class(Bs.Text.BodySecondary, Bs.Text.Small).Text("Count"))
				.Div(value => value.Id("e2e-state-count").Class(Bs.Raw("display-6")).Text(count.ToString()))
				.Div(buttons =>
				{
					buttons.Class(Bs.Display.Flex, Bs.Spacing.Gap(2))
					.Add(
						StateButton("e2e-state-decrement", "-", Action_StateDecrement),
						StateButton("e2e-state-increment", "+", Action_StateIncrement),
						StateButton("e2e-state-reset", "Reset", Action_StateReset));
				});
			});

		private static IHtmlContent StateButton(string id, string label, string action)
			=> FluentHtml.Button(button =>
			{
				button.Id(id)
				.Type("button")
				.Class(Bs.Btn.OutlinePrimary)
				.Text(label);
				button.Heimdall()
					.Click(action)
					.PayloadFromClosestState()
					.Target("#e2e-state-host")
					.SwapOuter();
			});

		private static IHtmlContent ActionButton(
			string id,
			string label,
			string action,
			string targetSelector,
			HeimdallHtml.Swap swap = HeimdallHtml.Swap.Inner)
			=> FluentHtml.Button(button =>
			{
				button.Id(id)
				.Type("button")
				.Class(Bs.Btn.OutlinePrimary, Bs.Spacing.Mt(2))
				.Text(label);
				button.Heimdall()
					.Click(action)
					.PayloadEmptyObject()
					.Target(targetSelector)
					.Swap(swap);
			});

		private static IHtmlContent Section(string id, string title, Action<FluentHtml.ElementBuilder> build)
			=> FluentHtml.Section(section =>
			{
				section.Id(id)
				.Class(Bs.Raw("border"), Bs.Spacing.P(3), Bs.Spacing.Mb(4))
				.H2(h => h.Class(Bs.Raw("h5")).Text(title));
				build(section);
			});

		private static IHtmlContent Status(string id, string text)
			=> FluentHtml.Span(span =>
			{
				span.Id(id).Text(text);
			});

		private static string Normalize(string? value)
			=> (value ?? string.Empty).Trim();
	}
}
