
using Microsoft.AspNetCore.Html;
using System.Buffers;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides a fluent, builder-based API for composing HTML content while preserving
	/// </summary>
	public static partial class FluentHtml
	{
		/// <summary>
		/// Builds a standard or void HTML element from a fluent builder callback.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="isVoid">Determines whether the element should be rendered as a void tag.</param>
		/// <param name="build">The builder callback used to populate the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		private static IHtmlContent BuildTag(string name, bool isVoid, Action<ElementBuilder> build)
		{
			using var b = new ElementBuilder(initialCapacity: 12);
			build(b);

			var parts = b.ToArray();
			return isVoid ? Html.VoidTag(name, parts) : Html.Tag(name, parts);
		}


		/// <summary>
		/// Creates a standard HTML element using a fluent builder callback.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="build">The builder callback used to populate attributes and children.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		public static IHtmlContent Tag(string name, Action<ElementBuilder> build)
			=> BuildTag(name, isVoid: false, build);

		/// <summary>
		/// Creates a void HTML element using a fluent builder callback.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="build">The builder callback used to populate element attributes.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered void element.</returns>
		public static IHtmlContent VoidTag(string name, Action<ElementBuilder> build)
			=> BuildTag(name, isVoid: true, build);

		/// <summary>
		/// Creates a standard HTML element from the provided parts.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="parts">The attributes and child content to merge into the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		public static IHtmlContent Tag(string name, params object?[] parts)
			=> Html.Tag(name, parts);

		/// <summary>
		/// Creates a void HTML element from the provided parts.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="parts">The attributes to merge into the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered void element.</returns>
		public static IHtmlContent VoidTag(string name, params object?[] parts)
			=> Html.VoidTag(name, parts);

		/// <summary>
		/// Creates an HTML fragment using a fluent builder callback.
		/// </summary>
		/// <param name="build">The builder callback used to add fragment parts.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered fragment.</returns>
		public static IHtmlContent Fragment(Action<FragmentBuilder> build)
		{
			using var fb = new FragmentBuilder(initialCapacity: 8);
			build(fb);
			return Html.Fragment(fb.ToArray());
		}

		/// <summary>
		/// Creates an HTML fragment from the provided parts.
		/// </summary>
		/// <param name="parts">The content parts to include in the fragment.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered fragment.</returns>
		public static IHtmlContent Fragment(params object?[] parts)
			=> Html.Fragment(parts);

		/// <summary>
		/// Encodes plain text as HTML content.
		/// </summary>
		/// <param name="text">The text content to encode.</param>
		/// <returns>An encoded text node.</returns>
		public static IHtmlContent Text(string? text) => Html.Text(text);

		/// <summary>
		/// Wraps raw HTML content without additional encoding.
		/// </summary>
		/// <param name="html">The raw HTML content to render.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance that writes the provided markup as-is.</returns>
		public static IHtmlContent Raw(string? html) => Html.Raw(html);

		/// <summary>
		/// Provides a lightweight pooled buffer used to collect builder parts with minimal allocations.
		/// </summary>
		/// <typeparam name="T">The buffer item type.</typeparam>
		private struct PooledBuffer<T>
		{
			private T[]? _arr;
			private int _count;

			/// <summary>
			/// Initializes the pooled buffer using the requested starting capacity.
			/// </summary>
			/// <param name="initialCapacity">The initial capacity to rent from the shared pool.</param>
			public void Init(int initialCapacity = 8)
			{
				_arr = ArrayPool<T>.Shared.Rent(initialCapacity);
				_count = 0;
			}

			/// <summary>
			/// Adds an item to the buffer, growing the rented array when needed.
			/// </summary>
			/// <param name="item">The item to append.</param>
			public void Add(T item)
			{
				if (_arr is null)
					Init();

				if (_count == _arr!.Length)
				{
					var old = _arr!;
					_arr = ArrayPool<T>.Shared.Rent(old.Length * 2);
					Array.Copy(old, 0, _arr, 0, old.Length);
					ArrayPool<T>.Shared.Return(old, clearArray: true);
				}

				_arr![_count++] = item;
			}

			/// <summary>
			/// Copies the buffered items into a compact array.
			/// </summary>
			/// <returns>A new array containing only the populated items.</returns>
			public T[] ToArray()
			{
				if (_arr is null || _count == 0)
					return Array.Empty<T>();

				var result = new T[_count];
				Array.Copy(_arr, 0, result, 0, _count);
				return result;
			}

			/// <summary>
			/// Returns the rented array to the shared pool and resets the buffer state.
			/// </summary>
			public void Dispose()
			{
				if (_arr is not null)
				{
					ArrayPool<T>.Shared.Return(_arr, clearArray: true);
					_arr = null;
					_count = 0;
				}
			}
		}

	}
}
