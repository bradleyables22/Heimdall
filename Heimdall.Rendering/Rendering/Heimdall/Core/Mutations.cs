using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	public static partial class HeimdallHtml
	{
		/// <summary>
		/// Describes which existing elements receive a Heimdall mutation.
		/// </summary>
		public readonly record struct MutationScope
		{
			private MutationScope(string mode, string? selector)
			{
				Mode = mode;
				Selector = selector;
			}

			internal string? Mode { get; }
			internal string? Selector { get; }

			/// <summary>Mutates only the resolved root target.</summary>
			public static MutationScope Self => new("self", null);

			/// <summary>Mutates the resolved root target and every descendant.</summary>
			public static MutationScope Subtree => new("subtree", null);

			/// <summary>Mutates descendants matching a selector scoped to the resolved root target.</summary>
			public static MutationScope Matching(string selector)
			{
				if (string.IsNullOrWhiteSpace(selector))
					throw new ArgumentException("Mutation descendant selector is required.", nameof(selector));

				return new MutationScope("select", selector.Trim());
			}
		}

		private static MutationScope NormalizeMutationScope(MutationScope scope)
			=> string.IsNullOrWhiteSpace(scope.Mode) ? MutationScope.Self : scope;

		private static string NormalizeMutationAttributeName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Mutation attribute name is required.", nameof(name));

			var normalized = name.Trim();
			if (normalized.Any(character =>
				char.IsWhiteSpace(character) ||
				char.IsControl(character) ||
				character is '"' or '\'' or '>' or '/' or '='))
			{
				throw new ArgumentException("Mutation attribute name contains invalid HTML name characters.", nameof(name));
			}

			return normalized;
		}

		/// <summary>Creates a mutation operation that sets an HTML attribute.</summary>
		public static IHtmlContent MutateAttribute(string name, string value)
		{
			var normalizedName = NormalizeMutationAttributeName(name);
			ArgumentNullException.ThrowIfNull(value);

			return Html.Tag("mutation-attr",
				Html.Attr("name", normalizedName),
				Html.Attr("value", value));
		}

		/// <summary>Creates a mutation operation that removes an HTML attribute.</summary>
		public static IHtmlContent RemoveMutatedAttribute(string name)
		{
			return Html.Tag("mutation-attr", Html.Attr("name", NormalizeMutationAttributeName(name)));
		}

		/// <summary>Creates a mutation operation that adds CSS class tokens.</summary>
		public static IHtmlContent AddMutatedClass(params string?[] classes)
			=> MutateClasses("add", classes);

		/// <summary>Creates a mutation operation that removes CSS class tokens.</summary>
		public static IHtmlContent RemoveMutatedClass(params string?[] classes)
			=> MutateClasses("remove", classes);

		private static IHtmlContent MutateClasses(string operation, string?[]? classes)
		{
			var value = Html.Class(classes ?? Array.Empty<string?>()).Value;
			return string.IsNullOrWhiteSpace(value)
				? HtmlString.Empty
				: Html.Tag("mutation-class", Html.Attr(operation, value));
		}

		/// <summary>
		/// Creates an out-of-band directive that mutates existing elements without replacing their nodes.
		/// </summary>
		public static IHtmlContent Mutate(
			string targetSelector,
			MutationScope scope,
			bool allTargets,
			params IHtmlContent[] operations)
		{
			if (string.IsNullOrWhiteSpace(targetSelector))
				throw new ArgumentException("Mutation target selector is required.", nameof(targetSelector));

			scope = NormalizeMutationScope(scope);
			var parts = new List<object?>
			{
				Html.Attr(Attrs.Target, targetSelector.Trim()),
				Html.Attr("scope", scope.Mode)
			};

			if (string.Equals(scope.Mode, "select", StringComparison.Ordinal))
				parts.Add(Html.Attr("selector", scope.Selector));

			if (allTargets)
				parts.Add(Html.Bool("all", true));

			if (operations is not null)
				parts.Add(operations);

			return Html.Tag("mutation", parts.ToArray());
		}

		/// <summary>Creates a single-root, self-scoped mutation directive.</summary>
		public static IHtmlContent Mutate(string targetSelector, params IHtmlContent[] operations)
			=> Mutate(targetSelector, MutationScope.Self, allTargets: false, operations);
	}
}
