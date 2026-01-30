using Microsoft.AspNetCore.Html;
using System.Reflection;

namespace Heimdall.Server
{
	internal sealed class ContentRegistry
	{
		private Dictionary<string, MethodInfo> _contentActions = new();

		internal void AddFromAssembly(Assembly assembly)
		{
			foreach (var type in assembly.GetTypes())
			{
				foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
				{
					var attr = method.GetCustomAttribute<ContentInvocationAttribute>();
					if (attr is null)
						continue;

					ValidateMethod(method);

					var actionId =
						string.IsNullOrWhiteSpace(attr.Invocation)
							? $"{type.Name}.{method.Name}"
							: attr.Invocation;

					if (_contentActions.ContainsKey(actionId))
					{
						throw new InvalidOperationException(
							$"Duplicate ContentInvocation id '{actionId}'. " +
							"Conent Action identifiers must be globally unique.");
					}

					_contentActions[actionId] = method;
				}
			}
		}

		public bool TryGet(string actionId, out MethodInfo method) => _contentActions.TryGetValue(actionId, out method!);

		private static void ValidateMethod(MethodInfo method)
		{
			if (!method.IsStatic)
				throw new InvalidOperationException(
					$"[ContentInvocation] must be static: {method.DeclaringType?.FullName}.{method.Name}");

			var rt = method.ReturnType;

			var valid =
				rt == typeof(IHtmlContent) ||
				rt == typeof(Task<IHtmlContent>) ||
				rt == typeof(ValueTask<IHtmlContent>);

			if (!valid)
				throw new InvalidOperationException(
					$"[ContentInvocation] must return IHtmlContent / Task<IHtmlContent> / ValueTask<IHtmlContent>: " +
					$"{method.DeclaringType?.FullName}.{method.Name} returns {rt.FullName}");
		}
	}
}
