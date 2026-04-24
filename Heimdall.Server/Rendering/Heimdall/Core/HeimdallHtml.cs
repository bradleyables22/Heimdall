
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
		/// Represents a strongly-typed Heimdall action identifier.
		/// </summary>
		public readonly record struct ActionId(string Value)
		{
			/// <summary>
			/// Cast ActionId to string implicitly, returning the underlying value.
			/// </summary>
			/// <returns></returns>
			public override string ToString() => Value ?? string.Empty;
			/// <summary>
			/// Allows implicit conversion from ActionId to string and vice versa for seamless integration with HTML attributes.
			/// </summary>
			/// <param name="id"></param>
			public static implicit operator string(ActionId id) => id.Value;
			/// <summary>
			/// Allows implicit conversion from string to ActionId, enabling easy creation of action identifiers from string literals.
			/// </summary>
			/// <param name="value"></param>
			public static implicit operator ActionId(string value) => new(value);
		}

		/// <summary>
		/// Defines supported Heimdall trigger types.
		/// </summary>
		public enum Trigger
		{
			/// <summary>
			/// The "Load" trigger is activated when the page or a specific element finishes loading. This is useful for initializing components or fetching data as soon as the content is ready.
			/// </summary>
			Load,
			/// <summary>
			/// The "Click" trigger is activated when a user clicks on an element. This is commonly used for buttons, links, or any interactive elements to perform actions in response to user input.
			/// </summary>
			Click,
			/// <summary>
			///	The "Change" trigger is activated when the value of an input element changes. This is particularly useful for form elements like text inputs, checkboxes, and select dropdowns to react to user modifications.
			/// </summary>
			Change,
			/// <summary>
			/// The "Input" trigger is activated on every input event, such as when a user types into a text field. This allows for real-time updates and interactions based on user input, making it ideal for features like live search or instant validation.
			/// </summary>
			Input,
			/// <summary>
			/// The "Submit" trigger is activated when a form is submitted. This is essential for handling form submissions, allowing you to process the data entered by the user and perform actions such as sending it to the server or updating the UI accordingly.
			/// </summary>
			Submit,
			/// <summary>
			/// The "KeyDown" trigger is activated when a user presses a key while an element is focused. This is useful for implementing keyboard shortcuts, form navigation, or any functionality that relies on specific key presses to enhance user interaction and accessibility.
			/// </summary>
			KeyDown,
			/// <summary>
			/// The "KeyUp" trigger is activated when a user releases a key while an element is focused. This can be used in conjunction with the KeyDown trigger to create more complex keyboard interactions, such as detecting specific key combinations or implementing features that require actions to occur after a key is released.
			/// </summary>
			Blur,
			/// <summary>
			/// The "Hover" trigger is activated when a user hovers over an element with their mouse cursor. This is commonly used for displaying tooltips, dropdown menus, or any interactive elements that provide additional information or options when the user hovers over them.
			/// </summary>
			Hover,
			/// <summary>
			/// The "Focus" trigger is activated when an element receives focus, either through user interaction (e.g., clicking on an input field) or programmatically (e.g., using JavaScript to set focus). This is useful for highlighting form fields, displaying additional information, or triggering actions that should occur when an element becomes active.
			/// </summary>
			Visible,
			/// <summary>
			/// The "Scroll" trigger is activated when a user scrolls within an element or the page. This can be used to implement features like infinite scrolling, lazy loading of content, or triggering animations and interactions based on the user's scroll position.
			/// </summary>
			Scroll
		}

		/// <summary>
		/// Defines DOM swap behaviors supported by Heimdall.
		/// </summary>
		public enum Swap
		{
			/// <summary>
			/// The "Inner" swap behavior replaces the inner content of the target element with the new content. This is useful for updating a specific section of the page without affecting the surrounding structure or attributes of the target element.
			/// </summary>
			Inner,
			/// <summary>
			///		The "Outer" swap behavior replaces the entire target element, including its attributes and content, with the new content. This is useful when you want to completely replace an element and its associated properties, such as when updating a component or replacing a section of the page with a new one.
			/// </summary>
			Outer,
			/// <summary>
			/// The "BeforeBegin" swap behavior inserts the new content immediately before the target element in the DOM. This is useful for adding new elements or content adjacent to an existing element without modifying the existing element itself.
			/// </summary>
			BeforeEnd,
			/// <summary>
			/// The "AfterEnd" swap behavior inserts the new content immediately after the target element in the DOM. This is useful for adding new elements or content adjacent to an existing element without modifying the existing element itself.
			/// </summary>
			AfterBegin,
			/// <summary>
			/// The "None" swap behavior indicates that no DOM manipulation should occur. This can be used in scenarios where you want to trigger an action or event without modifying the page's structure, such as when sending data to the server or performing a background task without updating the UI.
			/// </summary>
			None
		}
	}
}
