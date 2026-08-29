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

		public const string Action_Load = "e2e.load";
		public const string Action_LocalTimeLoad = "e2e.local-time.load";
		public const string Action_LocalTime = "e2e.local-time.action";
		public const string Action_LocalTimeOob = "e2e.local-time.oob";
		public const string Action_LocalTimeSse = "e2e.local-time.sse";
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
		public const string Action_Upload = "e2e.upload";
		public const string Action_Mutation = "e2e.mutation";
		public const string Action_MutationState = "e2e.mutation.state";
		public const string Action_MutationSse = "e2e.mutation.sse";
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
		public const string Action_Sync = "e2e.sync";
		public const string Action_SyncState = "e2e.sync.state";
		public const string Action_SyncOuterTarget = "e2e.sync.outer-target";
		public const string Action_Lifecycle = "e2e.lifecycle";
		public const string Action_LifecycleRequestCancel = "e2e.lifecycle.request-cancel";
		public const string Action_LifecycleSwapCancel = "e2e.lifecycle.swap-cancel";
		public const string Action_Error = "e2e.error";
		public const string Action_AuthRequired = "e2e.auth.required";
		public const string Action_AntiforgeryDisabled = "e2e.antiforgery.disabled";
		public const string Action_ClientInfo = "e2e.client-info";

		private const string SseTopic = "e2e-harness";
		private const string LocalTimeSseTopic = "e2e-local-time";
		public const string SseTopic_AuthRequired = "e2e-auth-required";
		private static readonly DateTimeOffset LocalTimeSample =
			new(2026, 8, 6, 8, 5, 7, 123, TimeSpan.Zero);
		private static readonly (string Suffix, string Format)[] LocalTimeFormats =
		[
			("standard-d", "d"),
			("standard-D", "D"),
			("standard-t", "t"),
			("standard-T", "T"),
			("standard-g", "g"),
			("standard-G", "G"),
			("day-1", "%d"),
			("day-2", "dd"),
			("day-3", "ddd"),
			("day-4", "dddd"),
			("month-1", "%M"),
			("month-2", "MM"),
			("month-3", "MMM"),
			("month-4", "MMMM"),
			("year-1", "%y"),
			("year-2", "yy"),
			("year-3", "yyy"),
			("year-4", "yyyy"),
			("hour12-1", "%h"),
			("hour12-2", "hh"),
			("hour24-1", "%H"),
			("hour24-2", "HH"),
			("minute-1", "%m"),
			("minute-2", "mm"),
			("second-1", "%s"),
			("second-2", "ss"),
			("period-1", "%t"),
			("period-2", "tt"),
			("fraction-1", "%f"),
			("fraction-2", "ff"),
			("fraction-3", "fff"),
			("offset-1", "%z"),
			("offset-2", "zz"),
			("offset-3", "zzz"),
			("single-quote", "'literal' yyyy"),
			("double-quote", "\"double literal\" yyyy"),
			("escaped", "yyyy \\y"),
			("composite", "dddd, MMMM d, yyyy 'at' h:mm:ss.fff tt zzz")
		];

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

		public sealed class UploadRequest
		{
			public string Caption { get; set; } = string.Empty;
		}

		public sealed class MessagePayload
		{
			public string Message { get; set; } = string.Empty;
		}

		public sealed class SyncPayload
		{
			public string Label { get; set; } = string.Empty;
			public int DelayMs { get; set; }
		}

		public sealed class SyncStatePayload
		{
			public int Count { get; set; }
			public int DelayMs { get; set; }
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
					RenderLocalTimeSection(),
					RenderDynamicBootSection(),
					RenderSwapSection(),
					RenderSwapModesSection(),
					RenderOobSection(),
					RenderAbortSection(),
					RenderMutationSection(),
					RenderStateSection(),
					RenderProgrammaticSection(),
					RenderPayloadRefSection(),
					RenderSelfPayloadSection(),
					RenderDelegatedEventsSection(),
					RenderBehaviorSection(),
					RenderNativeCommandSection(),
					RenderRequestSyncSection(),
					RenderLifecycleSection(),
					RenderJsSection(),
					RenderFormSection(),
					RenderUploadSection(),
					RenderSseSection(),
					RenderAntiforgerySection(),
					RenderClientInfoSection(),
					RenderAuthSection(),
					RenderErrorSection(),
					RenderRedirectSection());
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

		private static IHtmlContent SyncActionButton(
			string id,
			string label,
			string targetSelector,
			SyncPayload payload,
			HeimdallHtml.RequestSync? strategy = null,
			string? group = null)
			=> FluentHtml.Button(button =>
			{
				button.Id(id)
					.Type("button")
					.Class(Bs.Btn.OutlinePrimary, Bs.Spacing.Mt(2), Bs.Spacing.Me(2))
					.Text(label);

				var action = button.Heimdall()
					.Click(Action_Sync)
					.Payload(payload)
					.Target(targetSelector)
					.SwapInner()
					.Disable(false);

				switch (strategy)
				{
					case HeimdallHtml.RequestSync.Replace:
						action.SyncReplace(group);
						break;
					case HeimdallHtml.RequestSync.Drop:
						action.SyncDrop(group);
						break;
					case HeimdallHtml.RequestSync.QueueLatest:
						action.SyncQueueLatest(group);
						break;
					case HeimdallHtml.RequestSync.Parallel:
						action.SyncParallel(group);
						break;
				}
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

		private static IHtmlContent LocalTimeValue(string id, string format)
			=> FluentHtml.Span(time => time
				.Id(id)
				.LocalizeTime(LocalTimeSample, format));

		private static IHtmlContent RenderLocalTimeMatrix(string idPrefix)
			=> FluentHtml.Div(matrix =>
			{
				matrix.Id($"{idPrefix}-matrix");
				foreach (var (suffix, format) in LocalTimeFormats)
				{
					matrix.Span(time => time
						.Id($"{idPrefix}-{suffix}")
						.LocalizeTime(LocalTimeSample, format));
				}
			});

		private static string Normalize(string? value)
			=> (value ?? string.Empty).Trim();
	}
}
