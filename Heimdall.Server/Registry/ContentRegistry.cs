using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Heimdall.Server
{
    internal sealed class ContentRegistry
    {
        private readonly Dictionary<string, ContentActionDescriptor> _contentActions = new(StringComparer.Ordinal);

        internal void AddFromAssembly(Assembly assembly, IServiceProvider services)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    var attr = method.GetCustomAttribute<ContentInvocationAttribute>();
                    if (attr is null)
                        continue;

                    var actionId =
                        string.IsNullOrWhiteSpace(attr.Invocation)
                            ? $"{type.Name}.{method.Name}"
                            : attr.Invocation;

                    if (_contentActions.ContainsKey(actionId))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate ContentInvocation id '{actionId}'. " +
                            "Content action identifiers must be globally unique.");
                    }

                    var descriptor = CreateDescriptor(actionId, method, services);
                    _contentActions[actionId] = descriptor;
                }
            }
        }

        public bool TryGet(string actionId, out ContentActionDescriptor descriptor)
            => _contentActions.TryGetValue(actionId, out descriptor!);

        private static ContentActionDescriptor CreateDescriptor(
            string actionId,
            MethodInfo method,
            IServiceProvider services)
        {
            ValidateStatic(method);
            var returnKind = ValidateAndGetReturnKind(method);
            var parameters = BuildParameterPlan(method, services);

            return new ContentActionDescriptor(
                actionId,
                method,
                parameters,
                returnKind);
        }

        private static void ValidateStatic(MethodInfo method)
        {
            if (!method.IsStatic)
            {
                throw new InvalidOperationException(
                    $"[ContentInvocation] must be static: {method.DeclaringType?.FullName}.{method.Name}");
            }
        }

        private static ContentActionReturnKind ValidateAndGetReturnKind(MethodInfo method)
        {
            var rt = method.ReturnType;

            if (rt == typeof(IHtmlContent))
                return ContentActionReturnKind.Html;

            if (rt == typeof(Task<IHtmlContent>))
                return ContentActionReturnKind.TaskHtml;

            if (rt == typeof(ValueTask<IHtmlContent>))
                return ContentActionReturnKind.ValueTaskHtml;

            throw new InvalidOperationException(
                $"[ContentInvocation] must return IHtmlContent / Task<IHtmlContent> / ValueTask<IHtmlContent>: " +
                $"{method.DeclaringType?.FullName}.{method.Name} returns {rt.FullName}");
        }

        private static IReadOnlyList<ContentActionParameterDescriptor> BuildParameterPlan(
            MethodInfo method,
            IServiceProvider services)
        {
            var methodParams = method.GetParameters();

            if (methodParams.Length == 0)
                return Array.Empty<ContentActionParameterDescriptor>();

            var descriptors = new List<ContentActionParameterDescriptor>(methodParams.Length);

            var unresolved = new List<ParameterInfo>();

            // First pass: classify known framework params
            for (int i = 0; i < methodParams.Length; i++)
            {
                var p = methodParams[i];
                var pt = p.ParameterType;

                if (pt == typeof(HttpContext))
                {
                    descriptors.Add(new ContentActionParameterDescriptor(i, p, pt, ContentActionParameterKind.HttpContext));
                    continue;
                }

                if (pt == typeof(CancellationToken))
                {
                    descriptors.Add(new ContentActionParameterDescriptor(i, p, pt, ContentActionParameterKind.CancellationToken));
                    continue;
                }

                if (pt == typeof(System.Security.Claims.ClaimsPrincipal))
                {
                    descriptors.Add(new ContentActionParameterDescriptor(i, p, pt, ContentActionParameterKind.ClaimsPrincipal));
                    continue;
                }

                // unresolved for now
                unresolved.Add(p);
            }

            // Determine DI vs payload
            var serviceCandidates = new HashSet<ParameterInfo>();

            foreach (var p in unresolved)
            {
                if (IsServiceType(services, p.ParameterType))
                    serviceCandidates.Add(p);
            }

            var payloadCandidates = unresolved.Where(p => !serviceCandidates.Contains(p)).ToArray();

            if (payloadCandidates.Length > 1)
            {
                var names = string.Join(", ", payloadCandidates.Select(x => $"{x.ParameterType.Name} {x.Name}"));

                throw new InvalidOperationException(
                    $"[ContentInvocation] supports at most one payload parameter. " +
                    $"Method '{method.DeclaringType?.FullName}.{method.Name}' has multiple non-DI parameters: {names}");
            }

            var payloadParam = payloadCandidates.FirstOrDefault();

            // Second pass: finalize descriptors in correct order
            for (int i = 0; i < methodParams.Length; i++)
            {
                var p = methodParams[i];
                var pt = p.ParameterType;

                // Already handled framework params
                if (pt == typeof(HttpContext) ||
                    pt == typeof(CancellationToken) ||
                    pt == typeof(System.Security.Claims.ClaimsPrincipal))
                {
                    continue;
                }

                if (serviceCandidates.Contains(p))
                {
                    descriptors.Add(new ContentActionParameterDescriptor(i, p, pt, ContentActionParameterKind.Service));
                }
                else if (payloadParam == p)
                {
                    descriptors.Add(new ContentActionParameterDescriptor(i, p, pt, ContentActionParameterKind.Payload));
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unable to classify parameter '{p.Name}' on method '{method.DeclaringType?.FullName}.{method.Name}'.");
                }
            }

            // Ensure ordering is correct
            return descriptors.OrderBy(x => x.Index).ToArray();
        }

        private static bool IsServiceType(IServiceProvider services, Type type)
        {
            if (type == typeof(IServiceProvider))
                return true;

            return services.GetService(type) is not null;
        }
    }
}