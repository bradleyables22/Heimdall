using System.Text.Json;

namespace Heimdall.Server.Rendering
{
	public static partial class FluentHeimdall
	{
		/// <summary>Builds ordered operations for an in-place Heimdall mutation directive.</summary>
		public sealed class MutationBuilder
		{
			private readonly List<Microsoft.AspNetCore.Html.IHtmlContent> _operations = [];

			internal Microsoft.AspNetCore.Html.IHtmlContent[] ToArray() => [.. _operations];

			/// <summary>Sets an attribute to the supplied value. An empty string remains an explicit value.</summary>
			public MutationBuilder Attr(string name, string value)
			{
				_operations.Add(HeimdallHtml.MutateAttribute(name, value));
				return this;
			}

			/// <summary>Sets an attribute using an existing typed HTML attribute helper.</summary>
			public MutationBuilder Set(Html.HtmlAttr attribute)
			{
				if (!attribute.IsEmpty)
					_operations.Add(HeimdallHtml.MutateAttribute(
						attribute.Name,
						attribute.Kind == Html.AttrKind.Boolean ? string.Empty : attribute.Value));
				return this;
			}

			/// <summary>Removes an attribute.</summary>
			public MutationBuilder RemoveAttr(string name)
			{
				_operations.Add(HeimdallHtml.RemoveMutatedAttribute(name));
				return this;
			}

			/// <summary>Adds one or more CSS class tokens.</summary>
			public MutationBuilder AddClass(params string?[] classes)
			{
				_operations.Add(HeimdallHtml.AddMutatedClass(classes));
				return this;
			}

			/// <summary>Removes one or more CSS class tokens.</summary>
			public MutationBuilder RemoveClass(params string?[] classes)
			{
				_operations.Add(HeimdallHtml.RemoveMutatedClass(classes));
				return this;
			}

			/// <summary>Serializes and sets unkeyed Heimdall state.</summary>
			public MutationBuilder State(object state, JsonSerializerOptions? options = null)
			{
				ArgumentNullException.ThrowIfNull(state);
				return Set(HeimdallHtml.State(state, options));
			}

			/// <summary>Serializes and sets keyed Heimdall state.</summary>
			public MutationBuilder State(string key, object state, JsonSerializerOptions? options = null)
			{
				ArgumentNullException.ThrowIfNull(state);
				return Set(HeimdallHtml.State(key, state, options));
			}

			/// <summary>Sets raw unkeyed JSON state.</summary>
			public MutationBuilder StateJson(string json) => Set(HeimdallHtml.StateJson(json));

			/// <summary>Sets raw keyed JSON state.</summary>
			public MutationBuilder StateJson(string key, string json) => Set(HeimdallHtml.StateJson(key, json));

			/// <summary>Removes unkeyed Heimdall state.</summary>
			public MutationBuilder RemoveState() => RemoveAttr(HeimdallHtml.Attrs.DataState);

			/// <summary>Removes keyed Heimdall state.</summary>
			public MutationBuilder RemoveState(string key)
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("State key is required.", nameof(key));
				return RemoveAttr($"{HeimdallHtml.Attrs.DataStatePrefix}{key.Trim()}");
			}
		}
	}
}
