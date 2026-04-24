
namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting Heimdall-compatible HTML attributes.
	/// </summary>
	/// <remarks>
	/// This class centralizes all Heimdall attribute names and provides safe, composable helpers
	/// for triggers, payload handling, swap behavior, and server-sent event configuration.
	/// </remarks>
	public static partial class HeimdallHtml
	{
		/// <summary>
		/// Emits a Heimdall trigger attribute for the specified trigger and action.
		/// </summary>
		public static Html.HtmlAttr On(Trigger trigger, ActionId action)
			=> Html.Attr(TriggerToAttr(trigger), action.Value);

		private static string TriggerToAttr(Trigger trigger) => trigger switch
		{
			Trigger.Load => Attrs.Load,
			Trigger.Click => Attrs.Click,
			Trigger.Change => Attrs.Change,
			Trigger.Input => Attrs.Input,
			Trigger.Submit => Attrs.Submit,
			Trigger.KeyDown => Attrs.KeyDown,
			Trigger.Blur => Attrs.Blur,
			Trigger.Hover => Attrs.Hover,
			Trigger.Visible => Attrs.Visible,
			Trigger.Scroll => Attrs.Scroll,
			_ => throw new ArgumentOutOfRangeException(nameof(trigger))
		};


		/// <summary>
		/// Specifies the DOM target that will receive the response content.
		/// </summary>
		public static Html.HtmlAttr Target(string selector) => Html.Attr(Attrs.Target, selector);

		/// <summary>
		/// Specifies the swap strategy to use when updating the target element with the response content.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnLoad(ActionId action) => Html.Attr(Attrs.Load, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnClick(ActionId action) => Html.Attr(Attrs.Click, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnChange(ActionId action) => Html.Attr(Attrs.Change, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnInput(ActionId action) => Html.Attr(Attrs.Input, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnSubmit(ActionId action) => Html.Attr(Attrs.Submit, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnKeyDown(ActionId action) => Html.Attr(Attrs.KeyDown, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnBlur(ActionId action) => Html.Attr(Attrs.Blur, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnHover(ActionId action) => Html.Attr(Attrs.Hover, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnVisible(ActionId action) => Html.Attr(Attrs.Visible, action.Value);
		/// <summary>
		/// Specifies a trigger that will cause the element to send a request to the server, and the associated action to invoke on the server when that trigger occurs.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Html.HtmlAttr OnScroll(ActionId action) => Html.Attr(Attrs.Scroll, action.Value);

	}
}
