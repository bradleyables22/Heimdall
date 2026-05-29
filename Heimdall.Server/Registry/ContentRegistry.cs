using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection;
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
                foreach (var method in type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly))
                {
                    var attr = method.GetCustomAttribute<ContentInvocationAttribute>();
                    if (attr is null)
                        continue;

                    var actionId = ResolveActionId(type, method, attr);

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

        private static string ResolveActionId(
            Type type,
            MethodInfo method,
            ContentInvocationAttribute attr)
        {
            var prefix = type.GetCustomAttribute<ContentInvocationPrefixAttribute>(inherit: true)?.Prefix;
            var invocation = string.IsNullOrWhiteSpace(attr.Invocation)
                ? method.Name
                : attr.Invocation;

            invocation = NormalizeSegment(invocation!, nameof(ContentInvocationAttribute.Invocation));

            if (string.IsNullOrWhiteSpace(prefix))
            {
                return string.IsNullOrWhiteSpace(attr.Invocation)
                    ? $"{type.Name}.{invocation}"
                    : invocation;
            }

            return $"{NormalizeSegment(prefix, nameof(ContentInvocationPrefixAttribute.Prefix))}.{invocation}";
        }

        private static string NormalizeSegment(string value, string name)
        {
            var normalized = value.Trim().Trim('.');

            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException($"Content invocation {name} cannot be empty.");

            return normalized;
        }

        private static ContentActionDescriptor CreateDescriptor(
            string actionId,
            MethodInfo method,
            IServiceProvider services)
        {
            ValidateCallable(method);
            var returnKind = ValidateAndGetReturnKind(method);
            var parameters = BuildParameterPlan(method, services);
            var timeoutMetadata = ResolveRequestTimeoutMetadata(method);
            var authorizationMetadata = ResolveAuthorizationMetadata(method);

            return new ContentActionDescriptor(
                actionId,
                method,
                parameters,
                returnKind,
                timeoutMetadata.RequestTimeout,
                timeoutMetadata.DisableRequestTimeout,
                authorizationMetadata.AuthorizeData,
                authorizationMetadata.AllowAnonymous);
        }

        private static void ValidateCallable(MethodInfo method)
        {
            if (method.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"[ContentInvocation] cannot be generic: {method.DeclaringType?.FullName}.{method.Name}");
            }

            if (method.IsStatic)
                return;

            var declaringType = method.DeclaringType
                ?? throw new InvalidOperationException(
                    $"[ContentInvocation] instance method '{method.Name}' does not have a declaring type.");

            if (declaringType.IsAbstract || declaringType.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"[ContentInvocation] instance methods must be declared on a concrete, closed type: " +
                    $"{declaringType.FullName}.{method.Name}");
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
            var serviceInspector = services.GetService<IServiceProviderIsService>();
            var serviceCandidates = new HashSet<ParameterInfo>();

            foreach (var p in unresolved)
            {
                var explicitlyService = IsExplicitServiceParameter(p);
                var explicitlyPayload = IsExplicitPayloadParameter(p);

                if (explicitlyService && explicitlyPayload)
                {
                    throw new InvalidOperationException(
                        $"[ContentInvocation] parameter '{p.Name}' on method " +
                        $"'{method.DeclaringType?.FullName}.{method.Name}' cannot be both " +
                        "[FromServices] and [ContentPayload].");
                }

                if (explicitlyService ||
                    (!explicitlyPayload && IsServiceType(serviceInspector, p.ParameterType)))
                {
                    serviceCandidates.Add(p);
                }
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

        private static bool IsExplicitServiceParameter(ParameterInfo parameter)
            => parameter.GetCustomAttributes(inherit: true).OfType<IFromServiceMetadata>().Any();

        private static bool IsExplicitPayloadParameter(ParameterInfo parameter)
            => parameter.GetCustomAttribute<ContentPayloadAttribute>(inherit: true) is not null;

        private static bool IsServiceType(IServiceProviderIsService? serviceInspector, Type type)
        {
            if (type == typeof(IServiceProvider))
                return true;

            return serviceInspector?.IsService(type) == true;
        }

        private static RequestTimeoutMetadata ResolveRequestTimeoutMetadata(MethodInfo method)
        {
            var methodTimeout = method.GetCustomAttribute<RequestTimeoutAttribute>(inherit: false);
            var methodDisable = method.GetCustomAttribute<DisableRequestTimeoutAttribute>(inherit: false) is not null;

            if (methodTimeout is not null && methodDisable)
            {
                throw new InvalidOperationException(
                    $"[ContentInvocation] cannot combine [RequestTimeout] and [DisableRequestTimeout] on " +
                    $"{method.DeclaringType?.FullName}.{method.Name}.");
            }

            if (methodTimeout is not null)
                return new RequestTimeoutMetadata(methodTimeout, DisableRequestTimeout: false);

            if (methodDisable)
                return new RequestTimeoutMetadata(RequestTimeout: null, DisableRequestTimeout: true);

            var declaringType = method.DeclaringType;
            if (declaringType is null)
                return RequestTimeoutMetadata.None;

            var typeTimeout = declaringType.GetCustomAttribute<RequestTimeoutAttribute>(inherit: true);
            var typeDisable = declaringType.GetCustomAttribute<DisableRequestTimeoutAttribute>(inherit: true) is not null;

            if (typeTimeout is not null && typeDisable)
            {
                throw new InvalidOperationException(
                    $"[ContentInvocation] cannot combine [RequestTimeout] and [DisableRequestTimeout] on " +
                    $"{declaringType.FullName}.");
            }

            if (typeTimeout is not null)
                return new RequestTimeoutMetadata(typeTimeout, DisableRequestTimeout: false);

            if (typeDisable)
                return new RequestTimeoutMetadata(RequestTimeout: null, DisableRequestTimeout: true);

            return RequestTimeoutMetadata.None;
        }

        private static AuthorizationMetadata ResolveAuthorizationMetadata(MethodInfo method)
        {
            var authorizeData = new List<IAuthorizeData>();
            var allowAnonymous = false;

            var declaringType = method.DeclaringType;
            if (declaringType is not null)
            {
                var typeAttributes = declaringType.GetCustomAttributes(inherit: true);
                authorizeData.AddRange(typeAttributes.OfType<IAuthorizeData>());
                allowAnonymous |= typeAttributes.OfType<IAllowAnonymous>().Any();
            }

            var methodAttributes = method.GetCustomAttributes(inherit: true);
            authorizeData.AddRange(methodAttributes.OfType<IAuthorizeData>());
            allowAnonymous |= methodAttributes.OfType<IAllowAnonymous>().Any();

            return new AuthorizationMetadata(authorizeData.ToArray(), allowAnonymous);
        }

        private readonly record struct RequestTimeoutMetadata(
            RequestTimeoutAttribute? RequestTimeout,
            bool DisableRequestTimeout)
        {
            public static RequestTimeoutMetadata None { get; } = new(null, false);
        }

        private readonly record struct AuthorizationMetadata(
            IReadOnlyList<IAuthorizeData> AuthorizeData,
            bool AllowAnonymous);
    }
}
