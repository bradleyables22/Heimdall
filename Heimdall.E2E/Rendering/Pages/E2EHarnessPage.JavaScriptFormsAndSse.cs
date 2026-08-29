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

		[ContentInvocation(Action_Upload)]
		[RequestTimeout(3000)]
		public static async Task<IHtmlContent> Upload(
			[ContentPayload] UploadRequest request,
			IFormFile attachment,
			CancellationToken cancellationToken)
		{
			using var reader = new StreamReader(attachment.OpenReadStream());
			var contents = await reader.ReadToEndAsync(cancellationToken);

			return Status(
				"e2e-upload-success",
				$"Uploaded: {Normalize(request?.Caption)}|{attachment.FileName}|{attachment.Length}|{contents}");
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

		private static IHtmlContent RenderUploadSection()
			=> Section("e2e-upload-section", "File Upload", body =>
			{
				body.Form(form =>
				{
					form.Id("e2e-upload-form")
						.MultipartFormData();
					form.Heimdall()
						.Submit(Action_Upload)
						.PayloadFromClosestForm()
						.Target("#e2e-upload-result")
						.SwapInner()
						.PreventDefault(true);

					form.Input(Html.InputType.text, input => input
						.Id("e2e-upload-caption")
						.Name(nameof(UploadRequest.Caption)))
					.Input(Html.InputType.file, input => input
						.Id("e2e-upload-file")
						.Name("attachment")
						.Accept("text/plain")
						.Required())
					.Button(button => button
						.Id("e2e-upload-submit")
						.Type("submit")
						.Text("Upload file"));
				})
				.Div(target => target.Id("e2e-upload-result").Text("Upload target original"));
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
	}
}
