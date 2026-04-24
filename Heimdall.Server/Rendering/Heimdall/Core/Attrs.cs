
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
		/// Heimdall Attribute constants
		/// </summary>
		public static class Attrs
		{
			/// <summary>
			/// Load is the primary trigger for Heimdall content updates. It can be used on any element to specify that it should trigger a content update when the specified event occurs. The other triggers (Click, Change, Input, etc.) are more specific and can be used to target specific user interactions, but Load is the most general and can be used in a wide variety of scenarios.
			/// </summary>
			public const string Load = "heimdall-content-load";
			/// <summary>
			/// Click is a common trigger for user interactions, and it can be used to specify that a content update should occur when the user clicks on the element. This is often used for buttons, links, or any interactive element where a click is the primary action. While Load can be used for a wide range of events, Click is specifically designed for click interactions, making it more intuitive and easier to use in those cases.
			/// </summary>
			public const string Click = "heimdall-content-click";
			/// <summary>
			/// Change is typically used for form elements like input fields, select dropdowns, or checkboxes. It specifies that a content update should occur when the value of the element changes. This is particularly useful for dynamic forms or interactive filters where you want to update the content based on user input without requiring a full page reload. While Load can be used for any event, Change is specifically designed for value changes, making it more suitable for form interactions.
			/// </summary>
			public const string Change = "heimdall-content-change";
			/// <summary>
			/// Input is used for real-time updates as the user types or interacts with an input element. It specifies that a content update should occur whenever the value of the element changes, even if the user hasn't finished their input. This is ideal for features like live search, auto-suggestions, or any scenario where you want to provide immediate feedback based on user input. While Load can be used for any event, Input is specifically designed for real-time interactions, making it more effective for scenarios that require instant updates based on user input.
			/// </summary>
			public const string Input = "heimdall-content-input";
			/// <summary>
			/// Submit is used for form submissions. It specifies that a content update should occur when the form is submitted, allowing you to handle form data and update the content accordingly. This is particularly useful for dynamic forms where you want to process the form data and update the page without a full reload. While Load can be used for any event, Submit is specifically designed for form submissions, making it more appropriate for handling form-related interactions.
			/// </summary>
			public const string Submit = "heimdall-content-submit";
			/// <summary>
			/// KeyDown is used for keyboard interactions. It specifies that a content update should occur when a key is pressed down while the element is focused. This can be useful for implementing keyboard shortcuts, navigation, or any scenario where you want to trigger updates based on specific key presses. While Load can be used for any event, KeyDown is specifically designed for keyboard interactions, making it more suitable for scenarios that require handling user input from the keyboard.
			/// </summary>
			public const string KeyDown = "heimdall-content-keydown";
			/// <summary>
			/// Blur is used for focus interactions. It specifies that a content update should occur when the element loses focus. This can be useful for validating input fields, updating content based on user interactions, or any scenario where you want to trigger updates when the user moves away from an element. While Load can be used for any event, Blur is specifically designed for focus interactions, making it more effective for scenarios that require handling user focus and validation.
			/// </summary>
			public const string Blur = "heimdall-content-blur";
			/// <summary>
			/// Hover is used for mouse interactions. It specifies that a content update should occur when the user hovers over the element. This can be useful for tooltips, dynamic menus, or any scenario where you want to provide additional information or options when the user hovers over an element. While Load can be used for any event, Hover is specifically designed for mouse interactions, making it more suitable for scenarios that require handling user hover behavior.
			/// </summary>
			public const string Hover = "heimdall-content-hover";
			/// <summary>
			/// Visible is used for visibility interactions. It specifies that a content update should occur when the element becomes visible in the viewport. This can be useful for lazy loading content, triggering animations, or any scenario where you want to update the content based on the user's scroll position. While Load can be used for any event, Visible is specifically designed for visibility interactions, making it more effective for scenarios that require handling user scroll and visibility behavior.
			/// </summary>
			public const string Visible = "heimdall-content-visible";
			/// <summary>
			/// Scroll is used for scroll interactions. It specifies that a content update should occur when the user scrolls the element. This can be useful for infinite scrolling, dynamic content loading, or any scenario where you want to update the content based on the user's scroll behavior. While Load can be used for any event, Scroll is specifically designed for scroll interactions, making it more suitable for scenarios that require handling user scroll behavior and dynamic content loading.
			/// </summary>
			public const string Scroll = "heimdall-content-scroll";
			/// <summary>
			/// Target is used to specify the target element for the content update. It allows you to define where the updated content should be swapped in the DOM. This is particularly useful for scenarios where you want to update a specific section of the page without affecting the entire layout. While Load can be used for any event, Target is specifically designed for defining the target element for content updates, making it more effective for scenarios that require precise control over where the updated content is rendered in the DOM.
			/// </summary>
			public const string Target = "heimdall-content-target";
			/// <summary>
			/// Swap is used to specify the swap behavior for the content update. It allows you to define how the updated content should be swapped into the DOM, such as replacing the existing content, appending it, or inserting it before or after the target element. This is particularly useful for scenarios where you want to control the animation or transition of the updated content. While Load can be used for any event, Swap is specifically designed for defining the swap behavior of content updates, making it more suitable for scenarios that require handling the visual presentation of updated content in the DOM.
			/// </summary>
			public const string Swap = "heimdall-content-swap";
			/// <summary>
			/// Disable is used to specify that the content update should be disabled for the element. This can be useful for temporarily preventing updates while a certain condition is met, such as during a loading state or when a form is being submitted. While Load can be used for any event, Disable is specifically designed for controlling whether content updates should occur, making it more effective for scenarios that require managing the state of content updates based on specific conditions.
			/// </summary>
			public const string Disable = "heimdall-content-disable";
			/// <summary>
			/// PreventDefault is used to specify that the default behavior of the event should be prevented when the content update is triggered. This can be useful for scenarios where you want to handle the event entirely with JavaScript and prevent any default actions, such as preventing a form submission or stopping a link from navigating. While Load can be used for any event, PreventDefault is specifically designed for controlling the default behavior of events during content updates, making it more suitable for scenarios that require handling events in a custom way without triggering default browser actions.
			/// </summary>
			public const string PreventDefault = "heimdall-prevent-default";
			/// <summary>
			/// Payload is used to specify the payload data for the content update. It allows you to define additional data that should be sent along with the content update request, which can be useful for providing context or parameters for the server-side processing of the update. This is particularly useful for scenarios where you want to pass specific information to the server when triggering a content update. While Load can be used for any event, Payload is specifically designed for defining additional data to be sent with content updates, making it more effective for scenarios that require passing custom data during content updates.
			/// </summary>
			public const string Payload = "heimdall-payload";
			/// <summary>
			/// PayloadFrom and PayloadRef are used to specify the source of the payload data for the content update. PayloadFrom allows you to specify an element from which to extract the payload data, while PayloadRef allows you to reference a specific value or variable as the payload. This can be useful for scenarios where you want to dynamically generate the payload data based on user interactions or other elements on the page. While Load can be used for any event, PayloadFrom and PayloadRef are specifically designed for defining the source of payload data during content updates, making them more suitable for scenarios that require dynamic generation of payload data based on user interactions or other elements in the DOM.
			/// </summary>
			public const string PayloadFrom = "heimdall-payload-from";
			/// <summary>
			/// PayloadRef is used to specify a reference to a specific value or variable as the payload for the content update. This allows you to define a dynamic payload that can be generated based on user interactions or other elements on the page, providing flexibility in how the payload data is constructed and sent with the content update request. While Load can be used for any event, PayloadRef is specifically designed for referencing dynamic values as payload data during content updates, making it more effective for scenarios that require flexible and dynamic payload generation based on user interactions or other elements in the DOM.
			/// </summary>
			public const string PayloadRef = "heimdall-payload-ref";
			/// <summary>
			/// Debounce is used to specify a debounce delay for the content update. This can be useful for scenarios where you want to prevent rapid firing of content updates, such as during user input or scroll events. By specifying a debounce delay, you can ensure that the content update is only triggered after a certain amount of time has passed since the last event, providing a smoother user experience and reducing unnecessary server requests. While Load can be used for any event, Debounce is specifically designed for controlling the frequency of content updates during rapid events, making it more suitable for scenarios that require managing the timing of content updates based on user interactions or other events in the DOM.
			/// </summary>
			public const string Debounce = "heimdall-debounce";
			/// <summary>
			/// Key is used to specify a unique key for the content update. This can be useful for scenarios where you want to group or identify specific content updates, allowing you to manage and track updates more effectively. By assigning a key to a content update, you can easily reference and manipulate that update in your JavaScript code, providing greater control over the behavior and state of your content updates. While Load can be used for any event, Key is specifically designed for identifying and managing content updates, making it more effective for scenarios that require tracking and controlling specific updates in the DOM.
			/// </summary>
			public const string Key = "heimdall-key";
			/// <summary>
			/// HoverDelay is used to specify a delay for the Hover trigger. This can be useful for scenarios where you want to control how long the user needs to hover over an element before the content update is triggered, providing a more intentional and user-friendly experience. By specifying a hover delay, you can prevent accidental triggers of content updates when users briefly hover over an element, ensuring that updates only occur when the user intentionally hovers for a certain amount of time. While Load can be used for any event, HoverDelay is specifically designed for controlling the timing of content updates triggered by hover interactions, making it more suitable for scenarios that require managing the user experience around hover-triggered updates in the DOM.
			/// </summary>
			public const string HoverDelay = "heimdall-hover-delay";
			/// <summary>
			/// VisibleOnce is used to specify that the Visible trigger should only fire once. This can be useful for scenarios where you want to trigger a content update the first time an element becomes visible in the viewport, but not on subsequent visibility changes. By using VisibleOnce, you can ensure that the content update is only triggered the first time the element is scrolled into view, providing a more efficient and user-friendly experience. While Load can be used for any event, VisibleOnce is specifically designed for controlling the behavior of visibility-triggered updates, making it more effective for scenarios that require managing one-time updates based on element visibility in the DOM.
			/// </summary>
			public const string VisibleOnce = "heimdall-visible-once";
			/// <summary>
			/// ScrollThreshold is used to specify a threshold for the Scroll trigger. This can be useful for scenarios where you want to trigger a content update when the user scrolls a certain distance, rather than on every scroll event. By specifying a scroll threshold, you can control how sensitive the Scroll trigger is and prevent excessive updates during rapid scrolling, providing a smoother user experience and reducing unnecessary server requests. While Load can be used for any event, ScrollThreshold is specifically designed for managing the sensitivity of scroll-triggered updates, making it more suitable for scenarios that require controlling the behavior of content updates based on user scroll interactions in the DOM.
			/// </summary>
			public const string ScrollThreshold = "heimdall-scroll-threshold";
			/// <summary>
			/// Poll is used to specify a polling interval for the content update. This can be useful for scenarios where you want to automatically refresh content at regular intervals, such as for live data updates or real-time dashboards. By specifying a poll interval, you can ensure that the content is updated automatically without requiring user interaction, providing a more dynamic and up-to-date user experience. While Load can be used for any event, Poll is specifically designed for managing automatic content updates at regular intervals, making it more effective for scenarios that require continuous updates based on time intervals in the DOM.
			/// </summary>
			public const string Poll = "heimdall-poll";
			/// <summary>
			/// SseTopic, SseTarget, SseSwap, SseEvent, and SseDisable are used to configure server-sent events (SSE) for content updates. These attributes allow you to specify the topic for SSE updates, the target element for the updates, the swap behavior for incoming SSE content, the event name to listen for on the server side, and whether to disable SSE updates for the element. This can be useful for scenarios where you want to receive real-time updates from the server without requiring a full page reload, providing a more dynamic and responsive user experience. While Load can be used for any event, these SSE-specific attributes are designed for managing real-time updates from the server using server-sent events, making them more suitable for scenarios that require handling live data and real-time interactions in the DOM.
			/// </summary>
			public const string SseTopic = "heimdall-sse";
			/// <summary>
			/// SseTopicAlias is an alias for SseTopic, providing an alternative attribute name for configuring server-sent events (SSE) for content updates. This allows developers to choose the attribute name that best fits their coding style or project conventions while still enabling the same functionality for receiving real-time updates from the server. By providing an alias, you can enhance the flexibility and readability of your code when working with SSE configurations in the DOM. While Load can be used for any event, SseTopicAlias serves as an alternative to SseTopic specifically for managing real-time updates from the server using server-sent events, making it more adaptable to different coding preferences and styles in the DOM.
			/// </summary>
			public const string SseTopicAlias = "heimdall-sse-topic";
			/// <summary>
			/// SseTarget is used to specify the target element for server-sent event (SSE) updates. This allows you to define where the incoming SSE content should be swapped in the DOM when an update is received from the server. By specifying a target for SSE updates, you can control the placement of real-time content updates on the page, providing a more dynamic and responsive user experience. While Load can be used for any event, SseTarget is specifically designed for managing the target element for real-time updates received through server-sent events, making it more effective for scenarios that require handling live data and real-time interactions in the DOM.
			/// </summary>
			public const string SseTarget = "heimdall-sse-target";
			/// <summary>
			/// SseSwap is used to specify the swap behavior for incoming server-sent event (SSE) content. This allows you to define how the updated content received from the server should be swapped into the DOM, such as replacing existing content, appending it, or inserting it before or after the target element. By controlling the swap behavior for SSE updates, you can manage the visual presentation of real-time content updates on the page, providing a more dynamic and engaging user experience. While Load can be used for any event, SseSwap is specifically designed for managing the swap behavior of real-time updates received through server-sent events, making it more suitable for scenarios that require handling the visual presentation of live data and real-time interactions in the DOM.
			/// </summary>
			public const string SseSwap = "heimdall-sse-swap";
			/// <summary>
			/// SseEvent is used to specify the event name to listen for on the server side when configuring server-sent events (SSE) for content updates. This allows you to define a specific event that the server will emit, and the client will listen for, to trigger content updates in real-time. By specifying an event name for SSE updates, you can manage and organize your server-side events more effectively, providing a more structured approach to handling real-time interactions between the client and server. While Load can be used for any event, SseEvent is specifically designed for managing the event name associated with real-time updates received through server-sent events, making it more effective for scenarios that require handling live data and real-time interactions in the DOM.
			/// </summary>
			public const string SseEvent = "heimdall-sse-event";
			/// <summary>
			/// SseDisable is used to specify that server-sent event (SSE) updates should be disabled for the element. This can be useful for scenarios where you want to temporarily prevent real-time updates from the server, such as during a loading state or when certain conditions are met. By using SseDisable, you can control whether SSE updates should occur for a specific element, providing greater flexibility in managing real-time interactions between the client and server. While Load can be used for any event, SseDisable is specifically designed for controlling the behavior of real-time updates received through server-sent events, making it more suitable for scenarios that require managing the state of SSE updates based on specific conditions in the DOM.
			/// </summary>
			public const string SseDisable = "heimdall-sse-disable";
			/// <summary>
			/// DataState is used to specify the state of the content update. This can be useful for scenarios where you want to track the state of content updates, such as whether an update is in progress, has completed successfully, or has encountered an error. By using DataState, you can manage and display the status of content updates in the user interface, providing a more informative and responsive user experience. While Load can be used for any event, DataState is specifically designed for managing the state of content updates, making it more effective for scenarios that require tracking and displaying the status of updates in the DOM.
			/// </summary>
			public const string DataState = "data-heimdall-state";
			/// <summary>
			/// DataStatePrefix is used as a prefix for custom state values related to content updates. This allows you to define specific state values that can be associated with content updates, such as "loading", "success", or "error", by appending the desired state value to the DataStatePrefix. By using a consistent prefix for state values, you can easily manage and reference these states in your JavaScript code, providing greater control over the behavior and presentation of content updates based on their current state. While Load can be used for any event, DataStatePrefix serves as a standardized way to define and manage custom state values for content updates in the DOM.
			/// </summary>
			public const string DataStatePrefix = "data-heimdall-state-";
		}
	}
}
