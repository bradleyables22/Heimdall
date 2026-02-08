
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
	public sealed class HeimdallPageSettings
	{
		/// <summary>
		/// The route pattern used to map this Heimdall page.
		/// 
		/// This value is passed directly to the endpoint routing system and determines
		/// which incoming requests will render this page (e.g. "/", "/dashboard", "/users/{id}").
		/// 
		/// Required. Must be a valid ASP.NET route pattern.
		/// </summary>
		public string Pattern { get; set; } = string.Empty;

		/// <summary>
		/// The relative path to the primary HTML page to render.
		/// 
		/// This file is typically loaded from wwwroot and represents the main body
		/// of the page before any layout is applied.
		/// 
		/// Example: "pages/index.html"
		/// </summary>
		public string PagePath { get; set; } = string.Empty;

		/// <summary>
		/// The relative path to the layout HTML file used to wrap the page content.
		/// 
		/// The layout provides the shared structure for the page (headers, footers,
		/// navigation, etc.) and contains a placeholder where the page content
		/// will be injected.
		/// 
		/// Optional. If not provided, the page is rendered without a layout.
		/// </summary>
		public string LayoutPath { get; set; } = string.Empty;

		/// <summary>
		/// The placeholder token within the layout file that will be replaced
		/// with the rendered page content.
		/// 
		/// This value should match a marker inside the layout HTML
		/// (e.g. "{{body}}", "<!-- content -->").
		/// </summary>
		public string LayoutPlaceholder { get; set; } = string.Empty;

		/// <summary>
		/// A collection of named layout components that can be rendered dynamically
		/// into the layout at request time.
		/// 
		/// Each component is resolved per request and is provided both the
		/// current IServiceProvider and HttpContext, allowing components to:
		/// - Resolve scoped services
		/// - Access the authenticated user
		/// - Read headers, route data, or query values
		/// 
		/// Keys are case-insensitive and correspond to placeholders in the layout.
		/// </summary>
		public Dictionary<string, Func<IServiceProvider, HttpContext, IHtmlContent>> LayoutComponents
		{ get; set; } = new(StringComparer.OrdinalIgnoreCase);
	}
}
