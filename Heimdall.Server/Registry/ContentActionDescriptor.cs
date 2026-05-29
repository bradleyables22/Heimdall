using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace Heimdall.Server
{
    internal sealed class ContentActionDescriptor
    {
        private readonly Func<object?, object?[], object?> _invoker;
        private readonly ObjectFactory? _instanceFactory;
        private readonly Type? _instanceType;

        public string ActionId { get; }

        public MethodInfo Method { get; }

        public IReadOnlyList<ContentActionParameterDescriptor> Parameters { get; }

        public ContentActionReturnKind ReturnKind { get; }

        public RequestTimeoutAttribute? RequestTimeout { get; }

        public bool DisableRequestTimeout { get; }

        public IReadOnlyList<IAuthorizeData> AuthorizeData { get; }

        public bool AllowAnonymous { get; }

        public bool RequiresAuthorization => AuthorizeData.Count > 0 && !AllowAnonymous;

        public bool HasPayload => PayloadParameter is not null;

        public ContentActionParameterDescriptor? PayloadParameter { get; }

        public Type? PayloadType => PayloadParameter?.ParameterType;

        public ContentActionDescriptor(
            string actionId,
            MethodInfo method,
            IReadOnlyList<ContentActionParameterDescriptor> parameters,
            ContentActionReturnKind returnKind,
            RequestTimeoutAttribute? requestTimeout,
            bool disableRequestTimeout,
            IReadOnlyList<IAuthorizeData> authorizeData,
            bool allowAnonymous)
        {
            ActionId = actionId;
            Method = method;
            Parameters = parameters;
            ReturnKind = returnKind;
            RequestTimeout = requestTimeout;
            DisableRequestTimeout = disableRequestTimeout;
            AuthorizeData = authorizeData;
            AllowAnonymous = allowAnonymous;
            PayloadParameter = parameters.FirstOrDefault(x => x.Kind == ContentActionParameterKind.Payload);
            _invoker = CompileInvoker(method);
            if (!method.IsStatic)
            {
                _instanceType = method.DeclaringType
                    ?? throw new InvalidOperationException(
                        $"Unable to resolve the declaring type for '{method.Name}'.");
                _instanceFactory = ActivatorUtilities.CreateFactory(_instanceType, Type.EmptyTypes);
            }
        }

        public async ValueTask<IHtmlContent?> InvokeAsync(IServiceProvider services, object?[] args)
        {
            var target = CreateTarget(services);
            var result = _invoker(target, args);

            if (result is null)
                return null;

            return ReturnKind switch
            {
                ContentActionReturnKind.Html => (IHtmlContent)result,
                ContentActionReturnKind.TaskHtml => await (Task<IHtmlContent>)result,
                ContentActionReturnKind.ValueTaskHtml => await (ValueTask<IHtmlContent>)result,
                _ => throw new InvalidOperationException(
                    $"Unsupported Heimdall return kind '{ReturnKind}' for '{Method.DeclaringType?.FullName}.{Method.Name}'.")
            };
        }

        private object? CreateTarget(IServiceProvider services)
        {
            if (_instanceType is null)
                return null;

            var registered = services.GetService(_instanceType);
            return registered ?? _instanceFactory!(services, null);
        }

        private static Func<object?, object?[], object?> CompileInvoker(MethodInfo method)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var argsParam = Expression.Parameter(typeof(object?[]), "args");

            var methodParams = method.GetParameters();
            var callArgs = new Expression[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                callArgs[i] = Expression.Convert(indexExpr, methodParams[i].ParameterType);
            }

            MethodCallExpression callExpr;
            if (method.IsStatic)
            {
                callExpr = Expression.Call(method, callArgs);
            }
            else
            {
                var declaringType = method.DeclaringType
                    ?? throw new InvalidOperationException(
                        $"Unable to resolve the declaring type for '{method.Name}'.");
                callExpr = Expression.Call(Expression.Convert(targetParam, declaringType), method, callArgs);
            }

            Expression body;

            if (method.ReturnType == typeof(void))
            {
                body = Expression.Block(callExpr, Expression.Constant(null, typeof(object)));
            }
            else if (method.ReturnType.IsValueType)
            {
                body = Expression.Convert(callExpr, typeof(object));
            }
            else
            {
                body = Expression.TypeAs(callExpr, typeof(object));
            }

            return Expression.Lambda<Func<object?, object?[], object?>>(body, targetParam, argsParam).Compile();
        }
    }
}
