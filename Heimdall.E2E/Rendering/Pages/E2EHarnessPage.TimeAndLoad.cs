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
		private const string LocalTimeSseTopic = "e2e-local-time";
		private static readonly DateTimeOffset LocalTimeSample =
			new(2026, 8, 6, 8, 5, 7, 123, TimeSpan.Zero);
		private static readonly (string Group, string Label, string Suffix, string Format)[] LocalTimeFormats =
		[
			("Standard formats", "Short date", "standard-d", "d"),
			("Standard formats", "Long date", "standard-D", "D"),
			("Standard formats", "Short time", "standard-t", "t"),
			("Standard formats", "Long time", "standard-T", "T"),
			("Standard formats", "Short date and time", "standard-g", "g"),
			("Standard formats", "Long date and time", "standard-G", "G"),
			("Date tokens", "Day number", "day-1", "%d"),
			("Date tokens", "Day number, padded", "day-2", "dd"),
			("Date tokens", "Day name, abbreviated", "day-3", "ddd"),
			("Date tokens", "Day name, full", "day-4", "dddd"),
			("Date tokens", "Month number", "month-1", "%M"),
			("Date tokens", "Month number, padded", "month-2", "MM"),
			("Date tokens", "Month name, abbreviated", "month-3", "MMM"),
			("Date tokens", "Month name, full", "month-4", "MMMM"),
			("Date tokens", "Two-digit year", "year-1", "%y"),
			("Date tokens", "Two-digit year, padded", "year-2", "yy"),
			("Date tokens", "Year, at least three digits", "year-3", "yyy"),
			("Date tokens", "Four-digit year", "year-4", "yyyy"),
			("Time tokens", "12-hour clock", "hour12-1", "%h"),
			("Time tokens", "12-hour clock, padded", "hour12-2", "hh"),
			("Time tokens", "24-hour clock", "hour24-1", "%H"),
			("Time tokens", "24-hour clock, padded", "hour24-2", "HH"),
			("Time tokens", "Minute", "minute-1", "%m"),
			("Time tokens", "Minute, padded", "minute-2", "mm"),
			("Time tokens", "Second", "second-1", "%s"),
			("Time tokens", "Second, padded", "second-2", "ss"),
			("Time tokens", "AM/PM initial", "period-1", "%t"),
			("Time tokens", "AM/PM designator", "period-2", "tt"),
			("Time tokens", "Tenths of a second", "fraction-1", "%f"),
			("Time tokens", "Hundredths of a second", "fraction-2", "ff"),
			("Time tokens", "Milliseconds", "fraction-3", "fff"),
			("Time tokens", "UTC offset hours", "offset-1", "%z"),
			("Time tokens", "UTC offset hours, padded", "offset-2", "zz"),
			("Time tokens", "UTC offset hours and minutes", "offset-3", "zzz"),
			("Literals and composite", "Single-quoted literal", "single-quote", "'literal' yyyy"),
			("Literals and composite", "Double-quoted literal", "double-quote", "\"double literal\" yyyy"),
			("Literals and composite", "Escaped character", "escaped", "yyyy \\y"),
			("Literals and composite", "Composite format", "composite", "dddd, MMMM d, yyyy 'at' h:mm:ss.fff tt zzz")
		];

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
				body.P(intro => intro
					.Class(Bs.Text.BodySecondary)
					.Text("Every row starts from the same UTC instant. The browser converts it to its own locale and time zone; the automated run uses en-US and America/New_York."))
					.Div(fixture => fixture
						.Class(Bs.Raw("alert"), Bs.Raw("alert-secondary"), Bs.Raw("py-2"), Bs.Spacing.Mb(3))
						.Strong(label => label.Text("UTC fixture: "))
						.Tag("code", value => value.Text("2026-08-06T08:05:07.123Z")))
					.H3(heading => heading
						.Class(Bs.Raw("h5"), Bs.Spacing.Mb(1))
						.Text("Initial document formats"))
					.P(note => note
						.Class(Bs.Text.BodySecondary, Bs.Text.Small)
						.Text("These values are present in the first HTML response and localized during client boot."))
					.Add(RenderLocalTimeMatrix("e2e-local-time-initial"))
					.H3(heading => heading
						.Class(Bs.Raw("h5"), Bs.Spacing.Mt(4), Bs.Spacing.Mb(1))
						.Text("Delivery path checks"))
					.P(note => note
						.Class(Bs.Text.BodySecondary, Bs.Text.Small)
						.Text("Each card exercises a different way localized HTML can enter the page. Trigger a card and inspect only its result panel."))
					.Div(grid =>
					{
						grid.Class(Bs.Raw("row"), Bs.Raw("g-3"));

						grid.Div(column => column
							.Class(Bs.Raw("col-12"), Bs.Raw("col-xl-6"))
							.Add(LocalTimeDeliveryCard(
								"Load trigger",
								"Automatic action",
								"Runs once when Heimdall boots the element.",
								card => card
									.Div(trigger =>
									{
										trigger.Id("e2e-local-time-load-trigger")
											.Class(Bs.Text.BodySecondary, Bs.Text.Small)
											.Text("Load trigger ready");
										trigger.Heimdall()
											.Load(Action_LocalTimeLoad)
											.PayloadEmptyObject()
											.Target("#e2e-local-time-load-target")
											.SwapInner();
									})
									.Div(target => LocalTimeResultPanel(target, "e2e-local-time-load-target", "Local time load pending")))));

						grid.Div(column => column
							.Class(Bs.Raw("col-12"), Bs.Raw("col-xl-6"))
							.Add(LocalTimeDeliveryCard(
								"Action response",
								"Main swap",
								"Returns the full format matrix from a click action.",
								card => card
									.Add(ActionButton(
										id: "e2e-local-time-action-button",
										label: "Render local time matrix",
										action: Action_LocalTime,
										targetSelector: "#e2e-local-time-action-target"))
									.Div(target => LocalTimeResultPanel(target, "e2e-local-time-action-target", "Local time action pending")))));

						grid.Div(column => column
							.Class(Bs.Raw("col-12"), Bs.Raw("col-xl-6"))
							.Add(LocalTimeDeliveryCard(
								"Out-of-band response",
								"Main + invocation",
								"The main target inherits English; the invocation target explicitly inherits fr-FR.",
								card => card
									.Add(ActionButton(
										id: "e2e-local-time-oob-button",
										label: "Render local time OOB",
										action: Action_LocalTimeOob,
										targetSelector: "#e2e-local-time-oob-main-target"))
									.Div(label => label.Class(Bs.Text.BodySecondary, Bs.Text.Small, Bs.Spacing.Mt(3)).Text("Main target · en"))
									.Div(target => LocalTimeResultPanel(target, "e2e-local-time-oob-main-target", "Local time OOB main pending"))
									.Div(label => label.Class(Bs.Text.BodySecondary, Bs.Text.Small, Bs.Spacing.Mt(2)).Text("Invocation target · fr-FR"))
									.Div(target =>
									{
										LocalTimeResultPanel(target, "e2e-local-time-oob-side-target", "Local time OOB side pending");
										target.Lang("fr-FR");
									}))));

						grid.Div(column => column
							.Class(Bs.Raw("col-12"), Bs.Raw("col-xl-6"))
							.Add(LocalTimeDeliveryCard(
								"Server-sent event",
								"SSE main swap",
								"Publishes localized markup through the subscribed topic.",
								card => card
									.Div(host => host
										.Id("e2e-local-time-sse-host")
										.Add(
											HeimdallHtml.SseTopic(LocalTimeSseTopic),
											HeimdallHtml.SseTarget("#e2e-local-time-sse-target"),
											HeimdallHtml.SseSwapMode(HeimdallHtml.Swap.Inner)))
									.Add(ActionButton(
										id: "e2e-local-time-sse-button",
										label: "Publish local time SSE",
										action: Action_LocalTimeSse,
										targetSelector: "#e2e-local-time-sse-target",
										swap: HeimdallHtml.Swap.None))
									.Div(target => LocalTimeResultPanel(target, "e2e-local-time-sse-target", "Local time SSE pending")))));
					});
			});

		private static IHtmlContent LocalTimeDeliveryCard(
			string title,
			string path,
			string description,
			Action<FluentHtml.ElementBuilder> build)
			=> FluentHtml.Div(card => card
				.Class(Bs.Raw("card"), Bs.Raw("h-100"))
				.Div(content =>
				{
					content.Class(Bs.Raw("card-body"))
						.Div(header => header
							.Class(Bs.Display.Flex, Bs.Raw("justify-content-between"), Bs.Raw("align-items-start"), Bs.Spacing.Gap(2))
							.H4(heading => heading.Class(Bs.Raw("h6"), Bs.Spacing.Mb(0)).Text(title))
							.Span(badge => badge.Class(Bs.Raw("badge"), Bs.Raw("text-bg-secondary")).Text(path)))
						.P(note => note.Class(Bs.Text.BodySecondary, Bs.Text.Small, Bs.Spacing.Mt(2), Bs.Spacing.Mb(2)).Text(description));
					build(content);
				}));

		private static void LocalTimeResultPanel(
			FluentHtml.ElementBuilder target,
			string id,
			string pendingText)
			=> target.Id(id)
				.Class(
					Bs.Raw("border"),
					Bs.Raw("rounded"),
					Bs.Raw("bg-body-tertiary"),
					Bs.Raw("font-monospace"),
					Bs.Spacing.P(2),
					Bs.Spacing.Mt(2))
				.Text(pendingText);

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

		private static IHtmlContent LocalTimeValue(string id, string format)
			=> FluentHtml.Span(time => time
				.Id(id)
				.LocalizeTime(LocalTimeSample, format));

		private static IHtmlContent RenderLocalTimeMatrix(string idPrefix)
			=> FluentHtml.Div(matrix =>
			{
				matrix.Id($"{idPrefix}-matrix");
				foreach (var group in LocalTimeFormats.GroupBy(testCase => testCase.Group))
				{
					matrix.Div(groupPanel =>
					{
						groupPanel.Class(Bs.Spacing.Mb(3))
							.H3(heading => heading
								.Class(Bs.Raw("h6"), Bs.Spacing.Mb(2))
								.Text(group.Key))
							.Div(responsive => responsive
								.Class(Bs.Table.Responsive)
								.Table(table =>
								{
									table.Class(
										Bs.Table.Base,
										Bs.Table.Sm,
										Bs.Table.Bordered,
										Bs.Raw("align-middle"),
										Bs.Spacing.Mb(0))
										.TableHead(head => head
											.Class(Bs.Raw("table-light"))
											.TableRow(row => row
												.TableHeaderCell(cell => cell.Attr("scope", "col").Text("Case"))
												.TableHeaderCell(cell => cell.Attr("scope", "col").Text("C# format"))
												.TableHeaderCell(cell => cell.Attr("scope", "col").Text("Browser-localized value"))))
										.TableBody(body =>
										{
											foreach (var testCase in group)
											{
												body.TableRow(row => row
													.Attr("data-local-time-case", testCase.Suffix)
													.TableHeaderCell(cell => cell
														.Attr("scope", "row")
														.Text(testCase.Label))
													.TableDataCell(cell => cell
														.Tag("code", code => code.Text(testCase.Format)))
													.TableDataCell(cell => cell
														.Class(Bs.Raw("font-monospace"))
														.Span(time => time
															.Id($"{idPrefix}-{testCase.Suffix}")
															.LocalizeTime(LocalTimeSample, testCase.Format))));
											}
										});
								}));
					});
				}
			});
	}
}
