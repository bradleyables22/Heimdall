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
		[ContentInvocation(Action_Sync)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> Sync([ContentPayload] SyncPayload payload)
		{
			var delayMs = Math.Clamp(payload?.DelayMs ?? 0, 0, 1500);
			await Task.Delay(delayMs);
			return Status("e2e-sync-result-value", $"Sync: {Normalize(payload?.Label)}");
		}

		[ContentInvocation(Action_SyncState)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> SyncState([ContentPayload] SyncStatePayload payload)
		{
			var delayMs = Math.Clamp(payload?.DelayMs ?? 0, 0, 1500);
			await Task.Delay(delayMs);

			var next = new SyncStatePayload
			{
				Count = (payload?.Count ?? 0) + 1,
				DelayMs = 20
			};

			return FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().Mutate("#e2e-sync-live-state", mutation => mutation.Attr(
					"data-heimdall-state",
					System.Text.Json.JsonSerializer.Serialize(next)));
				fragment.Add(Status("e2e-sync-live-state-value", $"Queued state: {next.Count}"));
			});
		}

		[ContentInvocation(Action_SyncOuterTarget)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> SyncOuterTarget()
		{
			await Task.Delay(180);
			return FluentHtml.Div(target => target
				.Id("e2e-sync-replaced-target")
				.Text("Outer target first response"));
		}

		[ContentInvocation(Action_Lifecycle)]
		[RequestTimeout(3000)]
		public static IHtmlContent Lifecycle(
			HttpContext context,
			[ContentPayload] MessagePayload payload)
		{
			var message = Normalize(payload?.Message);
			var header = Normalize(context.Request.Headers["X-Heimdall-E2E"].FirstOrDefault());

			return FluentHtml.Fragment(fragment =>
			{
				fragment.Heimdall().Invocation(
					targetSelector: "#e2e-lifecycle-side",
					swap: HeimdallHtml.Swap.Inner,
					payload: Status("e2e-lifecycle-side-result", $"Lifecycle side: {message}"),
					wrapInTemplate: false);
				fragment.Add(Status(
					"e2e-lifecycle-main-result",
					$"Lifecycle: {message} | header: {header}"));
			});
		}

		[ContentInvocation(Action_LifecycleRequestCancel)]
		[RequestTimeout(3000)]
		public static IHtmlContent LifecycleRequestCancel()
			=> Status("e2e-lifecycle-request-cancel-result", "Request cancellation failed");

		[ContentInvocation(Action_LifecycleSwapCancel)]
		[RequestTimeout(3000)]
		public static IHtmlContent LifecycleSwapCancel()
			=> Status("e2e-lifecycle-swap-cancel-result", "Swap cancellation failed");

		private static IHtmlContent RenderErrorSection()
			=> Section("e2e-error-section", "Error", body =>
			{
				body.Div(target => target.Id("e2e-error-target").Text("Error target original"));
			});

		private static IHtmlContent RenderRequestSyncSection()
			=> Section("e2e-sync-section", "Request Synchronization", body =>
			{
				body.Div(target => target.Id("e2e-sync-parallel-target").Text("Parallel target original"))
					.Add(
						SyncActionButton(
							id: "e2e-sync-parallel-slow",
							label: "Parallel slow",
							targetSelector: "#e2e-sync-parallel-target",
							payload: new SyncPayload { Label = "parallel-slow", DelayMs = 250 }),
						SyncActionButton(
							id: "e2e-sync-parallel-fast",
							label: "Parallel fast",
							targetSelector: "#e2e-sync-parallel-target",
							payload: new SyncPayload { Label = "parallel-fast", DelayMs = 20 }))
					.Div(target => target.Id("e2e-sync-replace-target").Text("Replace target original"))
					.Add(
						SyncActionButton(
							id: "e2e-sync-replace-slow",
							label: "Replace slow",
							targetSelector: "#e2e-sync-replace-target",
							payload: new SyncPayload { Label = "replace-slow", DelayMs = 250 },
							strategy: HeimdallHtml.RequestSync.Replace,
							group: "e2e-sync-replace"),
						SyncActionButton(
							id: "e2e-sync-replace-fast",
							label: "Replace fast",
							targetSelector: "#e2e-sync-replace-target",
							payload: new SyncPayload { Label = "replace-fast", DelayMs = 20 },
							strategy: HeimdallHtml.RequestSync.Replace,
							group: "e2e-sync-replace"))
					.Div(target => target.Id("e2e-sync-drop-target").Text("Drop target original"))
					.Add(
						SyncActionButton(
							id: "e2e-sync-drop-slow",
							label: "Drop slow",
							targetSelector: "#e2e-sync-drop-target",
							payload: new SyncPayload { Label = "drop-slow", DelayMs = 180 },
							strategy: HeimdallHtml.RequestSync.Drop,
							group: "e2e-sync-drop"),
						SyncActionButton(
							id: "e2e-sync-drop-fast",
							label: "Drop fast",
							targetSelector: "#e2e-sync-drop-target",
							payload: new SyncPayload { Label = "drop-fast", DelayMs = 20 },
							strategy: HeimdallHtml.RequestSync.Drop,
							group: "e2e-sync-drop"))
					.Div(target => target.Id("e2e-sync-queue-target").Text("Queue target original"))
					.Add(
						SyncActionButton(
							id: "e2e-sync-queue-first",
							label: "Queue first",
							targetSelector: "#e2e-sync-queue-target",
							payload: new SyncPayload { Label = "queue-first", DelayMs = 180 },
							strategy: HeimdallHtml.RequestSync.QueueLatest,
							group: "e2e-sync-queue"),
						SyncActionButton(
							id: "e2e-sync-queue-second",
							label: "Queue second",
							targetSelector: "#e2e-sync-queue-target",
							payload: new SyncPayload { Label = "queue-second", DelayMs = 20 },
							strategy: HeimdallHtml.RequestSync.QueueLatest,
							group: "e2e-sync-queue"),
						SyncActionButton(
							id: "e2e-sync-queue-third",
							label: "Queue third",
							targetSelector: "#e2e-sync-queue-target",
							payload: new SyncPayload { Label = "queue-third", DelayMs = 20 },
							strategy: HeimdallHtml.RequestSync.QueueLatest,
							group: "e2e-sync-queue"))
					.Div(state =>
					{
						state.Id("e2e-sync-live-state")
							.Add(HeimdallHtml.State(new SyncStatePayload { Count = 0, DelayMs = 180 }))
							.Button(button =>
							{
								button.Id("e2e-sync-live-state-button")
									.Type("button")
									.Text("Queue state increment");
								button.Heimdall()
									.Click(Action_SyncState)
									.PayloadFromClosestState()
									.Target("#e2e-sync-live-state-result")
									.SwapInner()
									.Disable(false)
									.SyncQueueLatest("e2e-sync-live-state");
							});
					})
					.Div(target => target.Id("e2e-sync-live-state-result").Text("Queued state: 0"))
					.Div(target => target.Id("e2e-sync-replaced-target").Text("Replaceable target original"));
			});

		private static IHtmlContent RenderNativeCommandSection()
			=> Section("e2e-native-command-section", "Native HTML Commands", body =>
			{
				body.Button(button =>
				{
					button.Id("e2e-native-command-open")
						.Type("button")
						.Class(Bs.Btn.OutlinePrimary)
						.CommandFor("e2e-native-command-dialog")
						.Command(Html.CommandType.show_modal)
						.Text("Open native dialog");
				})
				.Dialog(dialog =>
				{
					dialog.Id("e2e-native-command-dialog")
						.P(message => message.Id("e2e-native-command-message").Text("Native dialog opened"))
						.Button(button =>
						{
							button.Id("e2e-native-command-close")
								.Type("button")
								.CommandFor("e2e-native-command-dialog")
								.Command(Html.CommandType.close)
								.Text("Close native dialog");
						});
				});
			});

		private static IHtmlContent RenderLifecycleSection()
			=> Section("e2e-lifecycle-section", "Request And Swap Lifecycle", body =>
			{
				body.Div(target => target.Id("e2e-lifecycle-primary").Text("Lifecycle primary original"))
					.Div(target => target.Id("e2e-lifecycle-secondary").Text("Lifecycle secondary original"))
					.Div(target => target.Id("e2e-lifecycle-side").Text("Lifecycle side original"))
					.Div(target => target.Id("e2e-lifecycle-request-cancel-target").Text("Request cancel target original"))
					.Div(target => target.Id("e2e-lifecycle-swap-cancel-target").Text("Swap cancel target original"))
					.Div(target => target.Id("e2e-timeout-target").Text("Timeout target original"))
					.Div(target => target.Id("e2e-external-abort-target").Text("External abort target original"));
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
	}
}
