using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.E2E.Rendering.Pages
{
	public static partial class E2EHarnessPage
	{
		[ContentInvocation(Action_RequestHeaders)]
		public static IHtmlContent RequestHeaders(HttpContext context)
		{
			var authorization = Normalize(context.Request.Headers.Authorization.FirstOrDefault());
			var custom = Normalize(context.Request.Headers["X-Heimdall-E2E"].FirstOrDefault());
			return Status("e2e-request-headers-result", $"{authorization}|{custom}");
		}

		[ContentInvocation(Action_Unauthorized)]
		public static IHtmlContent Unauthorized(HttpContext context)
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Status("e2e-unauthorized-response", "Authentication required");
		}
	}
}
