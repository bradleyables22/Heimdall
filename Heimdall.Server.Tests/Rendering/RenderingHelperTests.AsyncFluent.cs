using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using System.Reflection;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
	[Fact]
	public async Task FluentHtmlAsyncCallbacks_RenderAsyncDescendants()
	{
		var content = await FluentHtml.Div(async div =>
		{
			await Task.Yield();
			div.Content(new AsyncTestHtmlContent("<em>sync</em>", "<em>async</em>"));
			await div.Section(async section =>
			{
				await Task.Yield();
				section.Text("nested");
			});
		});

		Assert.Equal(
			"<div><em>async</em><section>nested</section></div>",
			await content.ToHtmlStringAsync());
		Assert.Equal(
			"<div><em>sync</em><section>nested</section></div>",
			content.ToHtmlString());
	}

	[Fact]
	public async Task FluentHtmlAsyncCallbacks_ComposeThroughFragments()
	{
		var content = await FluentHtml.Fragment(async fragment =>
		{
			await Task.Yield();
			await fragment.Div(async div =>
			{
				await Task.Yield();
				div.Content(new AsyncTestHtmlContent("sync", "async"));
			});
		});

		Assert.Equal("<div>async</div>", await content.ToHtmlStringAsync());
		Assert.Equal("<div>sync</div>", content.ToHtmlString());
	}

	[Fact]
	public async Task FluentHtmlAsyncCallbacks_UseAsyncWriterAtEveryNestedLevel()
	{
		var content = await FluentHtml.Fragment(async fragment =>
		{
			await fragment.Div(async div =>
			{
				await div.Section(async section =>
				{
					section.Add(new AsyncOnlyTestHtmlContent("<strong>async-only</strong>"));
				});
			});
		});

		Assert.Equal(
			"<div><section><strong>async-only</strong></section></div>",
			await content.ToHtmlStringAsync());
	}

	[Fact]
	public async Task FluentHtmlAsyncCallbacks_HandleSpecialElementWrappers()
	{
		var code = await FluentHtml.CodeBlock("csharp", async builder =>
		{
			await Task.Yield();
			builder.Text("<value />");
		});

		var input = await FluentHtml.Input(Html.InputType.email, async builder =>
		{
			await Task.Yield();
			builder.Required();
		});

		Assert.Equal(
			"<pre><code class=\"language-csharp\">&lt;value /&gt;</code></pre>",
			await code.ToHtmlStringAsync());
		Assert.Equal("<input type=\"email\" required />", await input.ToHtmlStringAsync());
	}

	[Fact]
	public async Task FluentHtmlAsyncCallbackOverloads_CoverEveryStaticBuilderMethod()
	{
		var elementCallbackType = typeof(Func<FluentHtml.ElementBuilder, ValueTask>);
		var fragmentCallbackType = typeof(Func<FluentHtml.FragmentBuilder, ValueTask>);
		var methods = typeof(FluentHtml)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.DeclaringType == typeof(FluentHtml))
			.Where(method => method.ReturnType == typeof(ValueTask<IHtmlContent>))
			.Where(method => method.GetParameters().Any(parameter =>
				parameter.ParameterType == elementCallbackType ||
				parameter.ParameterType == fragmentCallbackType))
			.ToArray();

		Assert.NotEmpty(methods);

		foreach (var method in methods)
		{
			var parameters = method.GetParameters();
			var args = parameters
				.Select(parameter => CreateAsyncCallbackArgument(parameter.ParameterType, elementCallbackType, fragmentCallbackType))
				.ToArray();

			var pending = Assert.IsType<ValueTask<IHtmlContent>>(method.Invoke(null, args));
			var content = await pending;
			var html = await content.ToHtmlStringAsync();

			Assert.NotEmpty(html);
			Assert.Contains("async-callback", html, StringComparison.Ordinal);
		}
	}

	[Fact]
	public async Task FluentHtmlAsyncCallbackOverloads_CoverEveryNestedBuilderMethod()
	{
		await AssertNestedBuilderOverloadsAsync(
			type: typeof(FluentHtml.ElementBuilder),
			resultType: typeof(ValueTask<FluentHtml.ElementBuilder>));
		await AssertNestedBuilderOverloadsAsync(
			type: typeof(FluentHtml.FragmentBuilder),
			resultType: typeof(ValueTask<FluentHtml.FragmentBuilder>));
	}

	private static object CreateAsyncCallbackArgument(
		Type parameterType,
		Type elementCallbackType,
		Type fragmentCallbackType)
	{
		if (parameterType == typeof(string))
			return "csharp";

		if (parameterType == typeof(Html.InputType))
			return Html.InputType.text;

		if (parameterType == elementCallbackType)
			return (Func<FluentHtml.ElementBuilder, ValueTask>)BuildElementAsync;

		if (parameterType == fragmentCallbackType)
			return (Func<FluentHtml.FragmentBuilder, ValueTask>)BuildFragmentAsync;

		throw new InvalidOperationException($"Unexpected async callback parameter: {parameterType}.");
	}

	private static async ValueTask BuildElementAsync(FluentHtml.ElementBuilder builder)
	{
		await Task.Yield();
		builder.Class("async-callback");
	}

	private static async ValueTask BuildFragmentAsync(FluentHtml.FragmentBuilder builder)
	{
		await Task.Yield();
		builder.Add(Html.Text("async-callback"));
	}

	private static async Task AssertNestedBuilderOverloadsAsync(Type type, Type resultType)
	{
		var callbackType = typeof(Func<FluentHtml.ElementBuilder, ValueTask>);
		var methods = type
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(method => method.DeclaringType == type)
			.Where(method => method.ReturnType == resultType)
			.Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == callbackType))
			.ToArray();

		Assert.NotEmpty(methods);

		var builder = Activator.CreateInstance(
			type,
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			args: new object[] { 8 },
			culture: null);

		Assert.NotNull(builder);

		try
		{
			foreach (var method in methods)
			{
				var args = method.GetParameters()
					.Select(parameter => CreateAsyncCallbackArgument(
						parameter.ParameterType,
						callbackType,
						type == typeof(FluentHtml.FragmentBuilder)
							? typeof(Func<FluentHtml.FragmentBuilder, ValueTask>)
							: typeof(Func<FluentHtml.ElementBuilder, ValueTask>)))
					.ToArray();

				var pending = method.Invoke(builder, args);
				if (type == typeof(FluentHtml.ElementBuilder))
					Assert.Same(builder, await (ValueTask<FluentHtml.ElementBuilder>)pending!);
				else
					Assert.Same(builder, await (ValueTask<FluentHtml.FragmentBuilder>)pending!);
			}

			var toArray = type.GetMethod("ToArray", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(toArray);
			var parts = Assert.IsType<object?[]>(toArray!.Invoke(builder, null));
			var html = await Html.Fragment(parts).ToHtmlStringAsync();
			Assert.Contains("async-callback", html);
		}
		finally
		{
			((IDisposable)builder).Dispose();
		}
	}
}
