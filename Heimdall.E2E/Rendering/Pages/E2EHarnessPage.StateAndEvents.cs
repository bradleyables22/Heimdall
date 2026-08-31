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
	}
}
